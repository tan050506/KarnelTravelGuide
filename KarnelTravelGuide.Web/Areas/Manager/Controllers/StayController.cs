using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class StayController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public StayController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Manager/Stay
        // Thay thế hàm Index cũ bằng hàm này
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;
            
            var stays = _context.Stays.Include(s => s.Spot).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                stays = stays.Where(s => s.Name.Contains(searchString) 
                                    || (s.Spot != null && s.Spot.SpotName.Contains(searchString)));
            }

            return View(await stays.ToListAsync());
        }

        // GET: Manager/Stay/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var stay = await _context.Stays
                .Include(s => s.Spot)
                .FirstOrDefaultAsync(m => m.StayId == id);
                
            if (stay == null) return NotFound();

            return View(stay);
        }

        // GET: Manager/Stay/Create
        public IActionResult Create()
        {
            ViewData["SpotId"] = new SelectList(_context.TouristSpots, "SpotId", "SpotName");
            return View();
        }

        // POST: Manager/Stay/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Stay stay, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "stays");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    stay.ImageUrl = "/images/stays/" + uniqueFileName;
                }

                _context.Add(stay);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["SpotId"] = new SelectList(_context.TouristSpots, "SpotId", "SpotName", stay.SpotId);
            return View(stay);
        }

        // GET: Manager/Stay/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var stay = await _context.Stays.FindAsync(id);
            if (stay == null) return NotFound();

            ViewData["SpotId"] = new SelectList(_context.TouristSpots, "SpotId", "SpotName", stay.SpotId);
            return View(stay);
        }

        // POST: Manager/Stay/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Stay stay, IFormFile? imageFile)
        {
            if (id != stay.StayId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null)
                    {
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "stays");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        stay.ImageUrl = "/images/stays/" + uniqueFileName;
                    }

                    _context.Update(stay);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StayExists(stay.StayId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["SpotId"] = new SelectList(_context.TouristSpots, "SpotId", "SpotName", stay.SpotId);
            return View(stay);
        }

        // POST: Manager/Stay/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var stay = await _context.Stays.FindAsync(id);
            if (stay != null)
            {
                _context.Stays.Remove(stay);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool StayExists(int id) => _context.Stays.Any(e => e.StayId == id);
    }
}