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
    public class RestaurantController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        private static readonly ConcurrentDictionary<string, bool> _inFlightRequests = new();

        public RestaurantController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // Retrieves a paginated, filtered, and sorted list of restaurants including table and feedback data
        public async Task<IActionResult> Index(string? searchString, string? sortOrder, int page = 1)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;

            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_asc" : "";

            var restaurants = _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables) 
                .Include(r => r.Feedbacks) 
                .AsQueryable();

            // Apply search filter for restaurant name or spot name
            if (!string.IsNullOrEmpty(searchString))
            {
                restaurants = restaurants.Where(r => 
                    (r.RestaurantName != null && r.RestaurantName.Contains(searchString)) || 
                    (r.Spot != null && r.Spot.SpotName != null && r.Spot.SpotName.Contains(searchString)));
            }

            // Apply sorting logic
            switch (sortOrder)
            {
                case "id_asc": 
                    restaurants = restaurants.OrderBy(s => s.RestaurantId); 
                    break;
                default: 
                    restaurants = restaurants.OrderByDescending(s => s.RestaurantId); 
                    break;
            }

            var allRestaurants = await restaurants.ToListAsync();

            // Calculate pagination metrics
            int pageSize = 10;
            int totalItems = allRestaurants.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedRestaurants = allRestaurants.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedRestaurants);
        }

        // Retrieves the detailed view of a specific restaurant and its tables
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var restaurant = await _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables)
                .FirstOrDefaultAsync(m => m.RestaurantId == id);
            if (restaurant == null) return NotFound();
            return View(restaurant);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View();
        }

        // Handles the creation of a new restaurant, its image upload, and associated table configurations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Restaurant restaurant, IFormFile? imageFile, string[] TableTypes, decimal[] Prices, int[] Quantities)
        {
            ModelState.Remove("Spot");
            if (ModelState.IsValid)
            {
                
                string requestKey = $"Res_{restaurant.RestaurantName}_{restaurant.SpotId}";
                // Prevent duplicate form submissions using a concurrent dictionary
                if (!_inFlightRequests.TryAdd(requestKey, true))
                {
                    TempData["ErrorMessage"] = "Processing your request... Please avoid double-clicking.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    
                    // Check if a restaurant with the same name already exists at the selected spot
                    bool isDuplicate = await _context.Restaurants.AnyAsync(r => r.RestaurantName == restaurant.RestaurantName && r.SpotId == restaurant.SpotId);
                    if (isDuplicate)
                    {
                        TempData["ErrorMessage"] = "A restaurant with this name already exists! Please avoid double-clicking.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (imageFile != null && imageFile.Length > 0)
                        restaurant.ImageUrl = await UploadFileAsync(imageFile);

                    _context.Add(restaurant);
                    await _context.SaveChangesAsync();

                    // Process and add the dynamically submitted table types
                    for (int i = 0; i < TableTypes.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(TableTypes[i]))
                        {
                            _context.RestaurantTables.Add(new RestaurantTable
                            {
                                RestaurantId = restaurant.RestaurantId,
                                TableType = TableTypes[i],
                                PriceRes = Prices.Length > i ? Prices[i] : 0,
                                Quantity = Quantities.Length > i ? Quantities[i] : 1
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Restaurant created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                finally
                {
                    
                    _inFlightRequests.TryRemove(requestKey, out _);
                }
            }

            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(restaurant);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var restaurant = await _context.Restaurants
                .Include(r => r.RestaurantTables)
                .FirstOrDefaultAsync(m => m.RestaurantId == id);
                
            if (restaurant == null) return NotFound();

            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(restaurant);
        }

        // Updates restaurant details, manages table inventory (add/edit/delete), and enforces booking constraints
        [HttpPost]
        [ValidateAntiForgeryToken]
        
        public async Task<IActionResult> Edit(int id, Restaurant restaurant, IFormFile? imageFile, int[] TableIds, string[] TableTypes, decimal[] Prices, int[] Quantities)
        {
            if (id != restaurant.RestaurantId) return NotFound();
            ModelState.Remove("Spot");

            if (ModelState.IsValid)
            {
                try
                {
                    // Replace the old image file if a new one is uploaded
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        DeletePhysicalFile(restaurant.ImageUrl);
                        restaurant.ImageUrl = await UploadFileAsync(imageFile);
                    }

                    _context.Update(restaurant);

                    // Identify tables marked for deletion
                    var existingTables = await _context.RestaurantTables.Where(t => t.RestaurantId == id).ToListAsync();
                    var submittedIds = TableIds != null ? TableIds.Where(tid => tid > 0).ToList() : new List<int>();
                    var tablesToDelete = existingTables.Where(t => !submittedIds.Contains(t.TableId)).ToList();

                    foreach (var t in tablesToDelete)
                    {
                        // Prevent deletion of tables that have existing booking records
                        bool isBooked = await _context.RestaurantBookings.AnyAsync(b => b.TableId == t.TableId);
                        if (isBooked)
                        {
                            TempData["ErrorMessage"] = $"Update failed! Cannot delete table type '{t.TableType}' because it has active/past bookings.";
                            return RedirectToAction(nameof(Edit), new { id = id });
                        }
                        _context.RestaurantTables.Remove(t);
                    }

                    // Process updates to existing tables or additions of new table types
                    for (int i = 0; i < TableTypes.Length; i++)
                    {
                        if (string.IsNullOrEmpty(TableTypes[i])) continue;
                        
                        int currentTid = (TableIds != null && TableIds.Length > i) ? TableIds[i] : 0;
                        if (currentTid > 0)
                        {
                            var tableToUpdate = existingTables.FirstOrDefault(t => t.TableId == currentTid);
                            if (tableToUpdate != null)
                            {
                                tableToUpdate.TableType = TableTypes[i];
                                tableToUpdate.PriceRes = Prices[i];
                                tableToUpdate.Quantity = Quantities[i];
                                _context.Update(tableToUpdate);
                            }
                        }
                        else
                        {
                            _context.RestaurantTables.Add(new RestaurantTable
                            {
                                RestaurantId = restaurant.RestaurantId,
                                TableType = TableTypes[i],
                                PriceRes = Prices[i],
                                Quantity = Quantities[i]
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Restaurant updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RestaurantExists(restaurant.RestaurantId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(restaurant);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var restaurant = await _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables)
                .FirstOrDefaultAsync(m => m.RestaurantId == id);
            if (restaurant == null) return NotFound();
            return View(restaurant);
        }

        // Permanently deletes a restaurant and its image, verifying no active/past bookings exist
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Data integrity check: Prevent deletion if there are associated booking records
            bool hasBookings = await _context.RestaurantBookings
                .Include(rb => rb.RestaurantTable)
                .AnyAsync(rb => rb.RestaurantTable != null && rb.RestaurantTable.RestaurantId == id);

            if (hasBookings)
            {
                TempData["ErrorMessage"] = "Cannot delete this restaurant because it has active or past booking records. Deleting it would corrupt invoice data.";
                return RedirectToAction(nameof(Index));
            }

            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant != null)
            {
                DeletePhysicalFile(restaurant.ImageUrl);
                _context.Restaurants.Remove(restaurant);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Restaurant deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool RestaurantExists(int id) => _context.Restaurants.Any(e => e.RestaurantId == id);

        // Helper method to assign GUIDs and save uploaded image files
        private async Task<string> UploadFileAsync(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "restaurants");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(fileStream); }
            return "/images/restaurants/" + uniqueFileName;
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