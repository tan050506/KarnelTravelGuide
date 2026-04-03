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
            // 1. Đếm hóa đơn chờ duyệt (Submitted & Unpaid)
            ViewBag.PendingInvoicesCount = await _context.Invoices
                .Include(i => i.Order)
                .CountAsync(i => i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Submitted");

            // 2. Đếm tổng số tuyến đường/phương tiện (Transportation)
            ViewBag.ActiveRoutesCount = await _context.Transportations.CountAsync();

            // 3. Đếm số lượng phòng đã được xác nhận (Confirmed)
            ViewBag.RoomsBookedCount = await _context.OrderDetails
                .Include(od => od.Order)
                .CountAsync(od => od.RoomBookingId != null && od.Order!.Status == "Confirmed");

            // 4. MỚI: Đếm số lượng đặt bàn đã được xác nhận (Confirmed)
            ViewBag.TablesBookedCount = await _context.OrderDetails
                .Include(od => od.Order)
                .CountAsync(od => od.ResBookingId != null && od.Order!.Status == "Confirmed");

            // 5. Đếm tổng số feedback của khách hàng
            ViewBag.FeedbackCount = await _context.Feedbacks.CountAsync();

            // 6. Đếm số lượng vé xe đã được xác nhận (Confirmed)
            var ticketsBookedCount = await _context.OrderDetails
                .Include(od => od.Order)
                .CountAsync(od => od.TicketBookingId != null && od.Order!.Status == "Confirmed");
            ViewBag.TicketsBookedCount = ticketsBookedCount;

            // Truyền dữ liệu sang View để vẽ biểu đồ
            ViewBag.ChartJsonData = System.Text.Json.JsonSerializer.Serialize(new
            {
                labels = new[] { "Stays (Rooms)", "Restaurants (Tables)", "Transport (Tickets)" },
                data = new[] { ViewBag.RoomsBookedCount, ViewBag.TablesBookedCount, ticketsBookedCount }
            });

            return View();
        }
    }
}