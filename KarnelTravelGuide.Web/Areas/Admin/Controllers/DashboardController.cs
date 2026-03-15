using Microsoft.AspNetCore.Mvc;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")] // Bắt buộc phải có dòng này
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return Content("Đây là trang quản trị của Admin!");
        }
    }
}