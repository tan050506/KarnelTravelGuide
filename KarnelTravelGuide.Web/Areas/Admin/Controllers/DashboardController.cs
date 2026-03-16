using Microsoft.AspNetCore.Mvc;
// using KarnelTravelGuide.Web.Data; // Bỏ comment dòng này khi bạn đã kết nối Entity Framework

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")] // <-- Rất quan trọng: Xác định Controller này thuộc phân hệ Admin
    public class DashboardController : Controller
    {
        // Khai báo DbContext để lấy dữ liệu từ SQL Server (Giữ comment nếu chưa tạo Models)
        // private readonly ApplicationDbContext _context;

        // public DashboardController(ApplicationDbContext context)
        // {
        //     _context = context;
        // }

        public IActionResult Index()
        {
            // TODO: Sau khi có Entity Framework, bạn thay bằng code query DB như sau:
            // ViewBag.TotalUsers = _context.Accounts.Count();
            // ViewBag.TotalRevenue = _context.Invoices.Sum(i => i.TotalAmount);
            // ViewBag.TotalBookings = _context.Invoices.Count();
            
            // Dữ liệu mẫu (Mock data) để hiển thị giao diện trước
            ViewBag.TotalUsers = 125;
            ViewBag.TotalRevenue = 45500000; // 45.5 triệu VNĐ
            ViewBag.TotalBookings = 42;
            ViewBag.TotalFeedback = 18;

            return View();
        }
    }
}