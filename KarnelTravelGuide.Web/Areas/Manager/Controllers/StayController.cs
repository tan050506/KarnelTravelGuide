using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using KarnelTravelGuide.Web.Attributes;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    [RoleAuthorize("Manager")]
    public class StayController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        private static readonly ConcurrentDictionary<string, bool> _inFlightRequests = new();

        public StayController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // Retrieves a paginated, filtered, and sorted list of stays including their rooms and feedbacks
        public async Task<IActionResult> Index(string? searchString, string? sortOrder, int page = 1)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_asc" : "";

            var stays = _context.Stays
                .Include(s => s.Spot)
                .Include(s => s.Rooms)
                .Include(s => s.Feedbacks) 
                .AsQueryable();

            // Apply search filter based on stay name or tourist spot name
            if (!string.IsNullOrEmpty(searchString))
            {
                stays = stays.Where(s => 
                    (s.Name != null && s.Name.Contains(searchString)) || 
                    (s.Spot != null && s.Spot.SpotName != null && s.Spot.SpotName.Contains(searchString)));
            }

            // Apply sorting logic
            switch (sortOrder)
            {
                case "id_asc": stays = stays.OrderBy(s => s.StayId); break;
                default: stays = stays.OrderByDescending(s => s.StayId); break;
            }

            var allStays = await stays.ToListAsync();

            // Calculate pagination metrics
            int pageSize = 10;
            int totalItems = allStays.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedStays = allStays.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedStays);
        }

        // Retrieves the detailed view of a specific stay and its associated rooms
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var stay = await _context.Stays.Include(s => s.Spot).Include(s => s.Rooms).FirstOrDefaultAsync(m => m.StayId == id);
            if (stay == null) return NotFound();
            return View(stay);
        }

        // Loads initial form data for creating a new stay
        public async Task<IActionResult> Create()
        {
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(new Stay { Rooms = new List<Room>() }); 
        }

        // Handles the creation of a new stay, its image upload, and associated room types
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Stay stay, IFormFile? imageFile)
        {
            ModelState.Remove("Spot"); 
            if (stay.Rooms != null) for (int i = 0; i < stay.Rooms.Count; i++) ModelState.Remove($"Rooms[{i}].Stay");

            if (ModelState.IsValid)
            {
                
                string requestKey = $"Stay_{stay.Name}_{stay.SpotId}";
                // Prevent duplicate form submissions using a concurrent dictionary
                if (!_inFlightRequests.TryAdd(requestKey, true))
                {
                    TempData["ErrorMessage"] = "Processing your request... Please avoid double-clicking.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    
                    // Check if a stay with the same name already exists at the selected spot
                    bool isDuplicate = await _context.Stays.AnyAsync(s => s.Name == stay.Name && s.SpotId == stay.SpotId);
                    if (isDuplicate)
                    {
                        TempData["ErrorMessage"] = "A stay with this name already exists! Please avoid double-clicking.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (imageFile != null && imageFile.Length > 0) stay.ImageUrl = await UploadFileAsync(imageFile);

                    _context.Add(stay); 
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Stay and room types created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                finally
                {
                    
                    _inFlightRequests.TryRemove(requestKey, out _);
                }
            }

            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(stay);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var stay = await _context.Stays.Include(s => s.Rooms).FirstOrDefaultAsync(s => s.StayId == id);
            if (stay == null) return NotFound();

            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(stay);
        }

        // Updates stay details, manages room inventory (add/edit/delete), and handles image replacement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Stay stay, IFormFile? imageFile)
        {
            if (id != stay.StayId) return NotFound();
            ModelState.Remove("Spot");
            if (stay.Rooms != null) for (int i = 0; i < stay.Rooms.Count; i++) ModelState.Remove($"Rooms[{i}].Stay");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingStay = await _context.Stays.Include(s => s.Rooms).FirstOrDefaultAsync(s => s.StayId == id);
                    if (existingStay == null) return NotFound();

                    existingStay.Name = stay.Name;
                    existingStay.SpotId = stay.SpotId;
                    existingStay.StayType = stay.StayType;
                    existingStay.Address = stay.Address;
                    existingStay.Description = stay.Description;

                    // Replace the old image file if a new one is uploaded
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        DeletePhysicalFile(existingStay.ImageUrl);
                        existingStay.ImageUrl = await UploadFileAsync(imageFile); 
                    }

                    var submittedRooms = stay.Rooms ?? new List<Room>();
                    var submittedRoomIds = submittedRooms.Select(r => r.RoomId).ToList();
                    
                    // Identify and remove room types that were deleted in the form
                    var roomsToRemove = existingStay.Rooms.Where(r => !submittedRoomIds.Contains(r.RoomId)).ToList();
                    _context.Rooms.RemoveRange(roomsToRemove);

                    // Process updates to existing room types or additions of new ones
                    foreach (var submittedRoom in submittedRooms)
                    {
                        if (submittedRoom.RoomId == 0) 
                            existingStay.Rooms.Add(new Room { RoomType = submittedRoom.RoomType, PriceRoom = submittedRoom.PriceRoom, Quantity = submittedRoom.Quantity });
                        else 
                        {
                            var existingRoom = existingStay.Rooms.FirstOrDefault(r => r.RoomId == submittedRoom.RoomId);
                            if (existingRoom != null)
                            {
                                existingRoom.RoomType = submittedRoom.RoomType;
                                existingRoom.PriceRoom = submittedRoom.PriceRoom;
                                existingRoom.Quantity = submittedRoom.Quantity;
                            }
                        }
                    }

                    _context.Update(existingStay);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Stay and room types updated successfully!";
                }
                catch (DbUpdateException)
                {
                    TempData["ErrorMessage"] = "Unable to save changes. A room you deleted might be linked to an active booking.";
                    return RedirectToAction(nameof(Edit), new { id = stay.StayId });
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(stay);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var stay = await _context.Stays
                .Include(s => s.Spot)
                .Include(s => s.Rooms)
                .FirstOrDefaultAsync(m => m.StayId == id);
            
            if (stay == null) return NotFound();
            return View(stay);
        }

        // Permanently deletes a stay, its room types, and image, verifying no active/past bookings exist
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            
            // Data integrity check: Prevent deletion if there are associated room booking records
            bool hasBookings = await _context.RoomBookings.AnyAsync(rb => rb.Room != null && rb.Room.StayId == id);
            if (hasBookings)
            {
                TempData["ErrorMessage"] = "Cannot delete this stay because it has active or past booking records. Deleting it would corrupt invoice data.";
                return RedirectToAction(nameof(Index));
            }

            var stay = await _context.Stays
                .Include(s => s.Rooms)
                .FirstOrDefaultAsync(s => s.StayId == id);

            if (stay != null)
            {
                
                if (stay.Rooms != null && stay.Rooms.Any())
                {
                    _context.Rooms.RemoveRange(stay.Rooms);
                }

                DeletePhysicalFile(stay.ImageUrl); 
                _context.Stays.Remove(stay);
                
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Stay and all its room types deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        // Helper method to assign GUIDs and save uploaded image files
        private async Task<string> UploadFileAsync(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "stays");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(fileStream); }
            return "/images/stays/" + uniqueFileName;
        }

        // Helper method to safely delete unused physical image files from the server
        private void DeletePhysicalFile(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            try
            {
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }
            catch (Exception) { }
        }
    }
}