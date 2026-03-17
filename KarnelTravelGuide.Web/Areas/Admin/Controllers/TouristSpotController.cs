using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data; // Thay bằng namespace chứa ApplicationDbContext của bạn nếu khác
using KarnelTravelGuide.Web.Models.Entities;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class TouristSpotController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TouristSpotController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/TouristSpot
        public async Task<IActionResult> Index()
        {
            var spots = await _context.TouristSpots.ToListAsync();
            return View(spots);
        }

        // GET: Admin/TouristSpot/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots
                .FirstOrDefaultAsync(m => m.SpotId == id);
            
            if (touristSpot == null) return NotFound();

            return View(touristSpot);
        }

        // GET: Admin/TouristSpot/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/TouristSpot/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SpotName,Address,Description")] TouristSpot touristSpot, IFormFile? imageFile)
        {
            // 1. Kiểm tra bắt buộc nhập liệu thủ công
            if (string.IsNullOrWhiteSpace(touristSpot.Address))
                ModelState.AddModelError("Address", "Address cannot be empty.");
            
            if (string.IsNullOrWhiteSpace(touristSpot.Description))
                ModelState.AddModelError("Description", "Description cannot be empty.");
            
            // Bắt buộc phải có ảnh khi tạo mới
            if (imageFile == null || imageFile.Length == 0)
                ModelState.AddModelError(string.Empty, "You must upload an image for the tourist spot.");

            ModelState.Remove("Hotels");
            ModelState.Remove("Resorts");
            ModelState.Remove("Restaurants");
            ModelState.Remove("TransportationDepartureSpots");
            ModelState.Remove("TransportationDestinationSpots");

            if (ModelState.IsValid)
            {
                if (imageFile != null) 
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "touristspots");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    touristSpot.ImageUrl = "/images/touristspots/" + uniqueFileName;
                }

                _context.Add(touristSpot);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tourist spot added successfully!";
                return RedirectToAction(nameof(Index));
            }
            
            // DÒNG NÀY RẤT QUAN TRỌNG ĐỂ KHẮC PHỤC LỖI CS0161
            // Nếu có lỗi nhập liệu, trả lại chính Form đó kèm theo thông báo lỗi
            return View(touristSpot);
        }

        // GET: Admin/TouristSpot/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots.FindAsync(id);
            if (touristSpot == null) return NotFound();

            return View(touristSpot);
        }

        // POST: Admin/TouristSpot/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SpotId,SpotName,Address,Description,ImageUrl")] TouristSpot touristSpot, IFormFile? imageFile)
        {
            if (id != touristSpot.SpotId) return NotFound();

            // Kiểm tra bắt buộc Address và Description
            if (string.IsNullOrWhiteSpace(touristSpot.Address))
                ModelState.AddModelError("Address", "Address cannot be empty.");
            
            if (string.IsNullOrWhiteSpace(touristSpot.Description))
                ModelState.AddModelError("Description", "Description cannot be empty.");

            // Lưu ý ở trang Edit: Không bắt buộc chọn file ảnh nếu đã có ảnh cũ (ImageUrl không rỗng)
            if ((imageFile == null || imageFile.Length == 0) && string.IsNullOrWhiteSpace(touristSpot.ImageUrl))
                ModelState.AddModelError(string.Empty, "You must have an image for this spot.");

            ModelState.Remove("Hotels");
            ModelState.Remove("Resorts");
            ModelState.Remove("Restaurants");
            ModelState.Remove("TransportationDepartureSpots");
            ModelState.Remove("TransportationDestinationSpots");

            if (ModelState.IsValid)
            {
                // (Giữ nguyên logic cập nhật ảnh và DB của bạn ở đây...)
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(touristSpot.ImageUrl))
                        {
                            string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, touristSpot.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
                        }

                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "touristspots");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

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
            return View(touristSpot);
        }

        // GET: Admin/TouristSpot/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots
                .FirstOrDefaultAsync(m => m.SpotId == id);
                
            if (touristSpot == null) return NotFound();

            return View(touristSpot);
        }

        // POST: Admin/TouristSpot/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var touristSpot = await _context.TouristSpots.FindAsync(id);
            if (touristSpot != null)
            {
                // XÓA ẢNH VẬT LÝ TRONG WWWROOT KHI XÓA ĐỊA ĐIỂM
                if (!string.IsNullOrEmpty(touristSpot.ImageUrl))
                {
                    string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, touristSpot.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
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