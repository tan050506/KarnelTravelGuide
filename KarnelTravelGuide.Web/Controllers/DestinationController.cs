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

        // Retrieves and displays a list of tourist destinations, optionally filtered by a search query
        public async Task<IActionResult> Index(string? searchString)
        {
            ViewBag.CurrentSearch = searchString;

            var spots = _context.TouristSpots
                .Include(t => t.TouristSpotImages)
                .AsQueryable();

            // Apply search filter based on destination name or address
            if (!string.IsNullOrEmpty(searchString))
            {
                spots = spots.Where(s => 
                    (s.SpotName != null && s.SpotName.Contains(searchString)) || 
                    (s.Address != null && s.Address.Contains(searchString)));
            }

            return View(await spots.OrderByDescending(t => t.SpotId).ToListAsync());
        }

        // Retrieves the full details of a specific destination, including its image gallery and associated branch
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var spot = await _context.TouristSpots
                .Include(t => t.TouristSpotImages)
                .Include(t => t.Branch) 
                .FirstOrDefaultAsync(m => m.SpotId == id);

            if (spot == null) return NotFound();

            return View(spot);
        }
    }
}