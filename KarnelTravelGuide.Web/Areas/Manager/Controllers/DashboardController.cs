using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using System.Linq;
using System.Threading.Tasks;

using KarnelTravelGuide.Web.Attributes;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    [RoleAuthorize("Manager")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            
            ViewBag.PendingInvoicesCount = await _context.Invoices
                .Include(i => i.Order)
                .CountAsync(i => i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Submitted");

            ViewBag.ActiveRoutesCount = await _context.Transportations.CountAsync();

            ViewBag.RoomsBookedCount = await _context.OrderDetails
                .Include(od => od.Order)
                .CountAsync(od => od.RoomBookingId != null && od.Order!.Status == "Confirmed");

            ViewBag.TablesBookedCount = await _context.OrderDetails
                .Include(od => od.Order)
                .CountAsync(od => od.ResBookingId != null && od.Order!.Status == "Confirmed");

            ViewBag.FeedbackCount = await _context.Feedbacks.CountAsync();

            var ticketsBookedCount = await _context.OrderDetails
                .Include(od => od.Order)
                .CountAsync(od => od.TicketBookingId != null && od.Order!.Status == "Confirmed");
            ViewBag.TicketsBookedCount = ticketsBookedCount;

            ViewBag.ChartJsonData = System.Text.Json.JsonSerializer.Serialize(new
            {
                labels = new[] { "Stays (Rooms)", "Restaurants (Tables)", "Transport (Tickets)" },
                data = new[] { ViewBag.RoomsBookedCount, ViewBag.TablesBookedCount, ticketsBookedCount }
            });

            return View();
        }
    }
}