using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class TouristSpotController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TouristSpotController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // HÀM GIẢ LẬP: Lấy Branch hiện tại của Manager (Sau này sẽ thay bằng lấy từ Session/User.Identity)
        private async Task<Branch> GetCurrentManagerBranchAsync()
        {
            var branch = await _context.Branches.FirstOrDefaultAsync();
            if (branch == null)
            {
                // Nếu chưa có Branch nào trong DB, tự tạo 1 cái để test không bị lỗi
                branch = new Branch { BranchName = "Central Branch (Auto-generated)", PhoneBranch = "1900-0000" };
                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();
            }
            return branch;
        }

        // 1. GET: Danh sách & Tìm kiếm
        public async Task<IActionResult> Index(string searchString)
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewData["CurrentFilter"] = searchString;

            // Chỉ lấy điểm du lịch thuộc Branch của Manager này
            var spots = _context.TouristSpots
                .Where(t => t.BranchId == currentBranch.BranchId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                spots = spots.Where(s => s.SpotName.Contains(searchString) || s.Address.Contains(searchString));
            }

            return View(await spots.ToListAsync());
        }

        // 2. GET: Create
        public async Task<IActionResult> Create()
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            // Truyền thông tin Branch ra View để hiển thị Read-only
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchId = currentBranch.BranchId;
            
            return View();
        }

        // 3. POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SpotName,Address,Description,BranchId")] TouristSpot touristSpot, IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(touristSpot.SpotName)) ModelState.AddModelError("SpotName", "Spot Name is required.");
            if (imageFile == null || imageFile.Length == 0) ModelState.AddModelError(string.Empty, "Image is required.");

            ModelState.Remove("Branch"); // Bỏ qua validate khóa ngoại
            ModelState.Remove("Restaurants");
            ModelState.Remove("Stays");
            ModelState.Remove("Transportations");

            if (ModelState.IsValid)
            {
                // Upload ảnh
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "touristspots");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile!.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                touristSpot.ImageUrl = "/images/touristspots/" + uniqueFileName;
                _context.Add(touristSpot);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tourist spot added successfully!";
                return RedirectToAction(nameof(Index));
            }

            // Nếu lỗi, load lại BranchName
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            return View(touristSpot);
        }

        // 4. GET: Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots.FindAsync(id);
            if (touristSpot == null) return NotFound();

            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            return View(touristSpot);
        }

        // 5. POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SpotId,SpotName,Address,Description,ImageUrl,BranchId")] TouristSpot touristSpot, IFormFile? imageFile)
        {
            if (id != touristSpot.SpotId) return NotFound();

            ModelState.Remove("Branch");
            ModelState.Remove("Restaurants");
            ModelState.Remove("Stays");
            ModelState.Remove("Transportations");

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        // Xóa ảnh cũ
                        if (!string.IsNullOrEmpty(touristSpot.ImageUrl))
                        {
                            string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, touristSpot.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
                        }

                        // Lưu ảnh mới
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "touristspots");
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        touristSpot.ImageUrl = "/images/touristspots/" + uniqueFileName;
                    }

                    _context.Update(touristSpot);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tourist spot updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TouristSpotExists(touristSpot.SpotId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            return View(touristSpot);
        }

        // 6. GET: Details (Trang Read)
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots
                .Include(t => t.Branch) // Load thêm tên Branch để hiển thị
                .FirstOrDefaultAsync(m => m.SpotId == id);

            if (touristSpot == null) return NotFound();

            return View(touristSpot);
        }

        // 7. GET: Delete (Trang Hỏi xác nhận Xóa)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots
                .Include(t => t.Branch)
                .FirstOrDefaultAsync(m => m.SpotId == id);

            if (touristSpot == null) return NotFound();

            return View(touristSpot);
        }

        // 8. POST: Delete (Xử lý Xóa thật trong Database)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var touristSpot = await _context.TouristSpots.FindAsync(id);
            if (touristSpot != null)
            {
                // Xóa file ảnh trong thư mục
                if (!string.IsNullOrEmpty(touristSpot.ImageUrl))
                {
                    string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, touristSpot.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
                }
                _context.TouristSpots.Remove(touristSpot);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tourist spot deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TouristSpotExists(int id)
        {
            return _context.TouristSpots.Any(e => e.SpotId == id);
        }
    }
}