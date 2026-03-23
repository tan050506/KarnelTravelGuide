using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class TransportationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TransportationController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // LẤY CHI NHÁNH CỦA MANAGER ĐANG ĐĂNG NHẬP
        private async Task<Branch> GetCurrentManagerBranchAsync()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                var currentManager = await _context.Accounts
                    .Include(a => a.Branch)
                    .FirstOrDefaultAsync(a => a.AccountId == userId.Value);

                if (currentManager != null && currentManager.Branch != null)
                {
                    return currentManager.Branch;
                }
            }
            // Fallback an toàn
            var fallbackBranch = await _context.Branches.FirstOrDefaultAsync();
            if (fallbackBranch == null)
            {
                fallbackBranch = new Branch { BranchName = "Central Branch (Auto-generated)" };
                _context.Branches.Add(fallbackBranch);
                await _context.SaveChangesAsync();
            }
            return fallbackBranch;
        }

        // 1. GET: Index
        public async Task<IActionResult> Index(string searchString)
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewData["CurrentFilter"] = searchString;

            var transportations = _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .Where(t => t.FromBranchId == currentBranch.BranchId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                transportations = transportations.Where(s => 
                    s.TransportType!.Contains(searchString) || 
                    s.TransportName!.Contains(searchString) ||
                    s.ToSpot!.SpotName.Contains(searchString));
            }

            return View(await transportations.ToListAsync());
        }

        // 2. GET: Create
        public async Task<IActionResult> Create()
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.FromBranchId = currentBranch.BranchId;
            
            // Truyền nguyên List để lấy được Address xử lý bằng JavaScript
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();

            return View();
        }

        // 3. POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TransportName,TransportType,FromBranchId,ToSpotId,DepartureTime,PriceTransport,Description")] Transportation transportation, IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) ModelState.AddModelError(string.Empty, "Transport image is required.");
            if (transportation.ToSpotId == null) ModelState.AddModelError("ToSpotId", "Please select a destination.");
            
            ModelState.Remove("FromBranch");
            ModelState.Remove("ToSpot");
            ModelState.Remove("TicketBookings");

            if (ModelState.IsValid)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "transportations");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile!.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                transportation.ImageUrl = "/images/transportations/" + uniqueFileName;
                _context.Add(transportation);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transportation route created successfully!";
                return RedirectToAction(nameof(Index));
            }

            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(transportation);
        }

        // 4. GET: Edit
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

        // 5. POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TransportationId,TransportName,TransportType,FromBranchId,ToSpotId,DepartureTime,PriceTransport,Description,ImageUrl")] Transportation transportation, IFormFile? imageFile)
        {
            if (id != transportation.TransportationId) return NotFound();

            ModelState.Remove("FromBranch");
            ModelState.Remove("ToSpot");
            ModelState.Remove("TicketBookings");

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(transportation.ImageUrl))
                        {
                            string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, transportation.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
                        }

                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "transportations");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        transportation.ImageUrl = "/images/transportations/" + uniqueFileName;
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

        // 6. GET: Details
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

        // 7. GET: Delete
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

        // 8. POST: Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var transportation = await _context.Transportations.FindAsync(id);
            if (transportation != null)
            {
                if (!string.IsNullOrEmpty(transportation.ImageUrl))
                {
                    string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, transportation.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
                }
                _context.Transportations.Remove(transportation);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transportation route deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TransportationExists(int id)
        {
            return _context.Transportations.Any(e => e.TransportationId == id);
        }
    }
}