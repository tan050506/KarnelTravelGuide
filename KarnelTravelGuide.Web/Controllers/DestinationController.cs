using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using System.Linq;
using System.Threading.Tasks;

namespace KarnelTravelGuide.Web.Controllers
{
    public class DestinationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DestinationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. TRANG DANH SÁCH ĐỊA ĐIỂM
        public async Task<IActionResult> Index(string searchString)
        {
            ViewBag.CurrentSearch = searchString;

            var spots = _context.TouristSpots
                .Include(t => t.TouristSpotImages)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                spots = spots.Where(s => 
                    (s.SpotName != null && s.SpotName.Contains(searchString)) || 
                    (s.Address != null && s.Address.Contains(searchString)));
            }

            // Mới nhất lên trước
            return View(await spots.OrderByDescending(t => t.SpotId).ToListAsync());
        }

        // 2. TRANG CHI TIẾT ĐỊA ĐIỂM
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var spot = await _context.TouristSpots
                .Include(t => t.TouristSpotImages)
                .Include(t => t.Branch) // Thông tin chi nhánh quản lý
                .FirstOrDefaultAsync(m => m.SpotId == id);

            if (spot == null) return NotFound();

            return View(spot);
        }
    }
}