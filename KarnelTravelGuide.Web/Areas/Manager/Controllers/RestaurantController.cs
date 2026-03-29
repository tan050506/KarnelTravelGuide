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

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class RestaurantController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public RestaurantController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index(string searchString, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_desc" : "";

            var restaurants = _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables) 
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                restaurants = restaurants.Where(r => 
                    (r.RestaurantName != null && r.RestaurantName.Contains(searchString)) || 
                    (r.Spot != null && r.Spot.SpotName != null && r.Spot.SpotName.Contains(searchString)));
            }

            switch (sortOrder)
            {
                case "id_desc": restaurants = restaurants.OrderByDescending(s => s.RestaurantId); break;
                default: restaurants = restaurants.OrderBy(s => s.RestaurantId); break;
            }

            return View(await restaurants.ToListAsync());
        }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Restaurant restaurant, IFormFile? imageFile, string[] TableTypes, decimal[] Prices, int[] Quantities)
        {
            ModelState.Remove("Spot");
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                    restaurant.ImageUrl = await UploadFileAsync(imageFile);

                _context.Add(restaurant);
                await _context.SaveChangesAsync();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        // ĐÃ BỔ SUNG TableIds để nhận diện Bàn nào đang bị sửa, Bàn nào bị xóa
        public async Task<IActionResult> Edit(int id, Restaurant restaurant, IFormFile? imageFile, int[] TableIds, string[] TableTypes, decimal[] Prices, int[] Quantities)
        {
            if (id != restaurant.RestaurantId) return NotFound();
            ModelState.Remove("Spot");

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        DeletePhysicalFile(restaurant.ImageUrl);
                        restaurant.ImageUrl = await UploadFileAsync(imageFile);
                    }

                    _context.Update(restaurant);

                    // 1. KIỂM TRA BÀN BỊ XÓA CÓ ĐANG ĐƯỢC ĐẶT KHÔNG
                    var existingTables = await _context.RestaurantTables.Where(t => t.RestaurantId == id).ToListAsync();
                    var submittedIds = TableIds != null ? TableIds.Where(tid => tid > 0).ToList() : new List<int>();
                    var tablesToDelete = existingTables.Where(t => !submittedIds.Contains(t.TableId)).ToList();

                    foreach (var t in tablesToDelete)
                    {
                        bool isBooked = await _context.RestaurantBookings.AnyAsync(b => b.TableId == t.TableId);
                        if (isBooked)
                        {
                            TempData["ErrorMessage"] = $"Update failed! Cannot delete table type '{t.TableType}' because it has active/past bookings.";
                            return RedirectToAction(nameof(Edit), new { id = id });
                        }
                        _context.RestaurantTables.Remove(t);
                    }

                    // 2. CẬP NHẬT HOẶC THÊM MỚI BÀN
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

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
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

        private async Task<string> UploadFileAsync(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "restaurants");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(fileStream); }
            return "/images/restaurants/" + uniqueFileName;
        }

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