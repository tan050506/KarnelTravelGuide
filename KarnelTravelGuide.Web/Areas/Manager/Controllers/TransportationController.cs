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

        // THÊM Ổ KHÓA CHỐNG SPAM CLICK ĐÚP
        private static readonly ConcurrentDictionary<string, bool> _inFlightRequests = new();

        public TransportationController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
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
                fallbackBranch = new Branch { BranchName = "Central Branch" };
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

            var transportations = _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .Where(t => t.FromBranchId == currentBranch.BranchId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                transportations = transportations.Where(s => 
                    (s.TransportType != null && s.TransportType.Contains(searchString)) || 
                    (s.TransportName != null && s.TransportName.Contains(searchString)) ||
                    (s.ToSpot != null && s.ToSpot.SpotName!.Contains(searchString)));
            }

            switch (sortOrder)
            {
                case "id_asc": 
                    transportations = transportations.OrderBy(t => t.TransportationId); 
                    break;
                default: 
                    transportations = transportations.OrderByDescending(t => t.TransportationId); // MẶC ĐỊNH LUÔN LÀ DESCENDING
                    break;
            }

            var allTransportations = await transportations.ToListAsync();

            // XỬ LÝ PHÂN TRANG
            int pageSize = 10;
            int totalItems = allTransportations.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedTransportations = allTransportations.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Truyền dữ liệu phân trang ra View
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedTransportations);
        }

        public async Task<IActionResult> Create()
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.FromBranchId = currentBranch.BranchId;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TransportName,TransportType,FromBranchId,ToSpotId,DepartureTime,PriceTransport,SeatCapacity,Description")] Transportation transportation, IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) ModelState.AddModelError(string.Empty, "Transport image is required.");
            if (transportation.ToSpotId == null) ModelState.AddModelError("ToSpotId", "Please select a destination.");
            
            ModelState.Remove("FromBranch"); ModelState.Remove("ToSpot"); ModelState.Remove("TicketBookings");

            if (ModelState.IsValid)
            {
                // KHÓA YÊU CẦU: Chặn click đúp cùng 1 Tuyến Xe
                string requestKey = $"Trans_{transportation.TransportName}_{transportation.FromBranchId}_{transportation.ToSpotId}";
                if (!_inFlightRequests.TryAdd(requestKey, true))
                {
                    TempData["ErrorMessage"] = "Processing your request... Please avoid double-clicking.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    // Kiểm tra trùng lặp trong DB
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
                    // Mở khóa sau khi xử lý xong
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TransportationId,TransportName,TransportType,FromBranchId,ToSpotId,DepartureTime,PriceTransport,SeatCapacity,Description,ImageUrl")] Transportation transportation, IFormFile? imageFile)
        {
            if (id != transportation.TransportationId) return NotFound();

            ModelState.Remove("FromBranch"); ModelState.Remove("ToSpot"); ModelState.Remove("TicketBookings");

            if (ModelState.IsValid)
            {
                try
                {
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

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var transportation = await _context.Transportations
                .Include(t => t.TicketBookings)
                .FirstOrDefaultAsync(t => t.TransportationId == id);

            if (transportation != null)
            {
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