using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace KarnelTravelGuide.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Bơm DbContext vào để lấy dữ liệu
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy 3 địa điểm du lịch mới nhất kèm theo danh sách ảnh của nó
            var topSpots = await _context.TouristSpots
                .Include(t => t.TouristSpotImages)
                .OrderByDescending(t => t.SpotId)
                .Take(3)
                .ToListAsync();

            // Truyền dữ liệu sang View
            return View(topSpots);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }
    }
}