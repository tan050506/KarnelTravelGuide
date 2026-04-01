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

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class TouristSpotController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        // THÊM Ổ KHÓA CHỐNG SPAM CLICK ĐÚP
        private static readonly ConcurrentDictionary<string, bool> _inFlightRequests = new();

        public TouristSpotController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

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
                fallbackBranch = new Branch { BranchName = "Central Branch", PhoneBranch = "1900-0000" };
                _context.Branches.Add(fallbackBranch);
                await _context.SaveChangesAsync();
            }
            return fallbackBranch;
        }

        // 1. GET: Index (ĐÃ THÊM PHÂN TRANG VÀ MẶC ĐỊNH MỚI NHẤT LÊN ĐẦU)
        public async Task<IActionResult> Index(string? searchString, string? sortOrder, int page = 1)
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            
            // Mặc định là hiển thị MỚI NHẤT (desc). Bấm vào link sẽ đổi thành id_asc
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_asc" : "";

            var spots = _context.TouristSpots
                .Include(t => t.Branch)
                .Include(t => t.TouristSpotImages)
                .Where(t => t.BranchId == currentBranch.BranchId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                spots = spots.Where(s => 
                    (s.SpotName != null && s.SpotName.Contains(searchString)) || 
                    (s.Address != null && s.Address.Contains(searchString)));
            }

            switch (sortOrder)
            {
                case "id_asc":
                    spots = spots.OrderBy(t => t.SpotId);
                    break;
                default:
                    spots = spots.OrderByDescending(t => t.SpotId); // MẶC ĐỊNH LUÔN LÀ DESCENDING
                    break;
            }

            var allSpots = await spots.ToListAsync();

            // XỬ LÝ PHÂN TRANG
            int pageSize = 10;
            int totalItems = allSpots.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedSpots = allSpots.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Truyền dữ liệu phân trang ra View
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedSpots);
        }

        public async Task<IActionResult> Create()
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchId = currentBranch.BranchId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SpotName,Address,Description,BranchId")] TouristSpot spot, IFormFile? CoverFile, List<IFormFile>? GalleryFiles)
        {
            ModelState.Remove("Branch"); ModelState.Remove("TouristSpotImages");

            if (ModelState.IsValid)
            {
                // KHÓA YÊU CẦU: Chặn click đúp cùng 1 Tên Địa Điểm
                string requestKey = $"Spot_{spot.SpotName}_{spot.BranchId}";
                if (!_inFlightRequests.TryAdd(requestKey, true))
                {
                    TempData["ErrorMessage"] = "Processing your request... Please avoid double-clicking.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    bool isDuplicate = await _context.TouristSpots.AnyAsync(ts => ts.SpotName == spot.SpotName && ts.BranchId == spot.BranchId);
                    if (isDuplicate)
                    {
                        TempData["ErrorMessage"] = "A tourist spot with this name already exists! Please avoid double-clicking.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (CoverFile != null && CoverFile.Length > 0)
                        spot.ImageUrl = await UploadFileAsync(CoverFile);

                    _context.TouristSpots.Add(spot);
                    await _context.SaveChangesAsync(); 

                    if (GalleryFiles != null && GalleryFiles.Count > 0)
                    {
                        foreach (var file in GalleryFiles)
                        {
                            if (file.Length > 0)
                            {
                                var imgUrl = await UploadFileAsync(file);
                                _context.TouristSpotImages.Add(new TouristSpotImage { SpotId = spot.SpotId, ImageUrl = imgUrl });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    TempData["SuccessMessage"] = "Spot created successfully! You can now add captions to your gallery images below.";
                    return RedirectToAction(nameof(Edit), new { id = spot.SpotId });
                }
                finally
                {
                    // Mở khóa sau khi xử lý xong
                    _inFlightRequests.TryRemove(requestKey, out _);
                }
            }
            
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            return View(spot);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var spot = await _context.TouristSpots
                .Include(t => t.TouristSpotImages)
                .FirstOrDefaultAsync(m => m.SpotId == id);
                
            if (spot == null) return NotFound();

            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            return View(spot);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TouristSpot spot, IFormFile? CoverFile, List<IFormFile>? GalleryFiles)
        {
            if (id != spot.SpotId) return NotFound();
            ModelState.Remove("Branch"); ModelState.Remove("TouristSpotImages");

            if (ModelState.IsValid)
            {
                var existingSpot = await _context.TouristSpots.FindAsync(id);
                if (existingSpot == null) return NotFound();

                existingSpot.SpotName = spot.SpotName;
                existingSpot.Address = spot.Address;
                existingSpot.Description = spot.Description;
                existingSpot.BranchId = spot.BranchId;

                if (CoverFile != null && CoverFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(existingSpot.ImageUrl))
                        DeletePhysicalFile(existingSpot.ImageUrl);

                    existingSpot.ImageUrl = await UploadFileAsync(CoverFile);
                }

                if (GalleryFiles != null && GalleryFiles.Count > 0)
                {
                    foreach (var file in GalleryFiles)
                    {
                        if (file.Length > 0)
                        {
                            var imgUrl = await UploadFileAsync(file);
                            _context.TouristSpotImages.Add(new TouristSpotImage { SpotId = id, ImageUrl = imgUrl });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tourist spot updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            return View(spot);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var spot = await _context.TouristSpots
                .Include(t => t.Branch)
                .Include(t => t.TouristSpotImages)
                .FirstOrDefaultAsync(m => m.SpotId == id);

            if (spot == null) return NotFound();
            return View(spot);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var spot = await _context.TouristSpots.Include(t => t.Branch).FirstOrDefaultAsync(m => m.SpotId == id);
            if (spot == null) return NotFound();
            return View(spot);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var spot = await _context.TouristSpots
                .Include(t => t.TouristSpotImages)
                .Include(t => t.Stays)
                .Include(t => t.Restaurants)
                .Include(t => t.Transportations)
                .FirstOrDefaultAsync(m => m.SpotId == id);

            if (spot != null)
            {
                if (spot.Stays.Any() || spot.Restaurants.Any() || spot.Transportations.Any())
                {
                    TempData["ErrorMessage"] = "Cannot delete! This destination is linked to active Stays, Restaurants, or Transportations. Please remove them first.";
                    return RedirectToAction(nameof(Index));
                }

                if (!string.IsNullOrEmpty(spot.ImageUrl)) DeletePhysicalFile(spot.ImageUrl);

                if (spot.TouristSpotImages != null && spot.TouristSpotImages.Any())
                {
                    foreach (var img in spot.TouristSpotImages)
                    {
                        DeletePhysicalFile(img.ImageUrl);
                    }
                }

                _context.TouristSpots.Remove(spot);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Destination and all associated image files have been permanently deleted!";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteGalleryImage(int imageId)
        {
            var img = await _context.TouristSpotImages.FindAsync(imageId);
            if (img != null)
            {
                DeletePhysicalFile(img.ImageUrl);
                _context.TouristSpotImages.Remove(img);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateGalleryImageCaption(int imageId, string? caption)
        {
            var img = await _context.TouristSpotImages.FindAsync(imageId);
            if (img != null)
            {
                img.Caption = caption;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        private async Task<string> UploadFileAsync(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "touristspots");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            return "/images/touristspots/" + uniqueFileName;
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