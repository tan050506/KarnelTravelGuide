using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Concurrent;

using KarnelTravelGuide.Web.Attributes;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    [RoleAuthorize("Manager")]
    public class TransportationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        private static readonly ConcurrentDictionary<string, bool> _inFlightRequests = new();

        public TransportationController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // Retrieves the branch associated with the currently logged-in manager, or creates a fallback "Central Branch"
        private async Task<Branch> GetCurrentManagerBranchAsync()
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId.HasValue)
            {
                var currentManager = await _context.Accounts
                    .Include(a => a.Branch)
                    .FirstOrDefaultAsync(a => a.AccountId == accountId.Value);

                if (currentManager?.Branch != null) return currentManager.Branch;
            }
            
            var fallbackBranch = await _context.Branches.FirstOrDefaultAsync();
            if (fallbackBranch == null)
            {
                fallbackBranch = new Branch { BranchName = "Central Branch" };
                _context.Branches.Add(fallbackBranch);
                await _context.SaveChangesAsync();
            }
            return fallbackBranch;
        }

        // Retrieves a paginated, filtered, and sorted list of transportation routes managed by the current branch
        public async Task<IActionResult> Index(string? searchString, string? sortOrder, int page = 1)
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;

            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_asc" : "";

            var transportations = _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .Where(t => t.FromBranchId == currentBranch.BranchId)
                .AsQueryable();

            // Apply search filter based on transport type, name, or destination spot
            if (!string.IsNullOrEmpty(searchString))
            {
                transportations = transportations.Where(s => 
                    (s.TransportType != null && s.TransportType.Contains(searchString)) || 
                    (s.TransportName != null && s.TransportName.Contains(searchString)) ||
                    (s.ToSpot != null && s.ToSpot.SpotName!.Contains(searchString)));
            }

            // Apply sorting logic
            switch (sortOrder)
            {
                case "id_asc": 
                    transportations = transportations.OrderBy(t => t.TransportationId); 
                    break;
                default: 
                    transportations = transportations.OrderByDescending(t => t.TransportationId); 
                    break;
            }

            var allTransportations = await transportations.ToListAsync();

            // Calculate pagination metrics
            int pageSize = 10;
            int totalItems = allTransportations.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedTransportations = allTransportations.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedTransportations);
        }

        // Loads initial form data for creating a new transportation route
        public async Task<IActionResult> Create()
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.FromBranchId = currentBranch.BranchId;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();

            return View();
        }

        // Validates and processes the creation of a new transport route, enforcing seat limits and preventing duplicate submissions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TransportName,TransportType,FromBranchId,ToSpotId,DepartureTime,PriceTransport,SeatCapacity,Description")] Transportation transportation, IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) ModelState.AddModelError(string.Empty, "Transport image is required.");
            if (transportation.ToSpotId == null) ModelState.AddModelError("ToSpotId", "Please select a destination.");
            
            // Enforce logical seat capacity limits based on the transport type
            int maxSeats = transportation.TransportType == "Land" ? 100 : 300;
            if (transportation.SeatCapacity < 1 || transportation.SeatCapacity > maxSeats) ModelState.AddModelError("SeatCapacity", $"Seats must be between 1 and {maxSeats}.");
            
            ModelState.Remove("FromBranch"); ModelState.Remove("ToSpot"); ModelState.Remove("TicketBookings");

            if (ModelState.IsValid)
            {
                
                string requestKey = $"Trans_{transportation.TransportName}_{transportation.FromBranchId}_{transportation.ToSpotId}";
                // Prevent duplicate form submissions during processing
                if (!_inFlightRequests.TryAdd(requestKey, true))
                {
                    TempData["ErrorMessage"] = "Processing your request... Please avoid double-clicking.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    
                    // Verify that no identical route already exists to prevent duplicates
                    bool isDuplicate = await _context.Transportations.AnyAsync(t => t.TransportName == transportation.TransportName && t.FromBranchId == transportation.FromBranchId && t.ToSpotId == transportation.ToSpotId);
                    if (isDuplicate)
                    {
                        TempData["ErrorMessage"] = "A route with this name already exists! Please avoid double-clicking.";
                        return RedirectToAction(nameof(Index));
                    }

                    transportation.ImageUrl = await UploadFileAsync(imageFile!);
                    
                    _context.Add(transportation);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Transportation route created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                finally
                {
                    
                    _inFlightRequests.TryRemove(requestKey, out _);
                }
            }

            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(transportation);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var transportation = await _context.Transportations.FindAsync(id);
            if (transportation == null) return NotFound();

            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            
            return View(transportation);
        }

        // Updates an existing transportation route, validates seat capacity, and securely replaces the old image file
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TransportationId,TransportName,TransportType,FromBranchId,ToSpotId,DepartureTime,PriceTransport,SeatCapacity,Description,ImageUrl")] Transportation transportation, IFormFile? imageFile)
        {
            if (id != transportation.TransportationId) return NotFound();

            int maxSeats = transportation.TransportType == "Land" ? 100 : 300;
            if (transportation.SeatCapacity < 1 || transportation.SeatCapacity > maxSeats) ModelState.AddModelError("SeatCapacity", $"Seats must be between 1 and {maxSeats}.");

            ModelState.Remove("FromBranch"); ModelState.Remove("ToSpot"); ModelState.Remove("TicketBookings");

            if (ModelState.IsValid)
            {
                try
                {
                    // Replace the old physical image file if a new one is provided
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        DeletePhysicalFile(transportation.ImageUrl);
                        transportation.ImageUrl = await UploadFileAsync(imageFile);
                    }

                    _context.Update(transportation);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Transportation route updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TransportationExists(transportation.TransportationId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(transportation);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var transportation = await _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .FirstOrDefaultAsync(m => m.TransportationId == id);

            if (transportation == null) return NotFound();
            return View(transportation);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var transportation = await _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .FirstOrDefaultAsync(m => m.TransportationId == id);

            if (transportation == null) return NotFound();
            return View(transportation);
        }

        // Permanently removes a transportation route and its image file, ensuring no active ticket bookings are affected
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var transportation = await _context.Transportations
                .Include(t => t.TicketBookings)
                .FirstOrDefaultAsync(t => t.TransportationId == id);

            if (transportation != null)
            {
                // Data integrity check to prevent deletion of routes with existing bookings
                if (transportation.TicketBookings != null && transportation.TicketBookings.Any())
                {
                    TempData["ErrorMessage"] = "Cannot delete! This route has active ticket bookings.";
                    return RedirectToAction(nameof(Index));
                }

                DeletePhysicalFile(transportation.ImageUrl);
                
                _context.Transportations.Remove(transportation);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transportation route and its image deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TransportationExists(int id) => _context.Transportations.Any(e => e.TransportationId == id);

        // Helper method to generate a unique GUID filename and save the uploaded image to the server
        private async Task<string> UploadFileAsync(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "transportations");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            return "/images/transportations/" + uniqueFileName;
        }

        // Helper method to safely delete an unused physical image file from the server
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