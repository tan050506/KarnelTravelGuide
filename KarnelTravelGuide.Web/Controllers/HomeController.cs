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

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Retrieves the top 3 latest tourist spots to display on the landing page
        public async Task<IActionResult> Index()
        {
            
            var topSpots = await _context.TouristSpots
                .Include(t => t.TouristSpotImages)
                .OrderByDescending(t => t.SpotId)
                .Take(3)
                .ToListAsync();

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