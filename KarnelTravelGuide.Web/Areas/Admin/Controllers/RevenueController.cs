using System.Linq;
using System.Threading.Tasks;
using KarnelTravelGuide.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Attributes;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin")]
    public class RevenueController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RevenueController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var invoices = await _context.Invoices
                .Include(i => i.Account)
                .Include(i => i.Order)
                .Where(i => i.Order != null && i.Order.Status != "Pending")
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();

            decimal totalRevenue = invoices.Where(i => i.PaymentStatus == "Paid").Sum(i => i.FinalTotal ?? 0);
            int totalOrders = invoices.Count;

            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalOrders = totalOrders;

            return View(invoices);
        }
    }
}
