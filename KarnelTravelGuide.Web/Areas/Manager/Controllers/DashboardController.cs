using Microsoft.AspNetCore.Mvc;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")] 
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}