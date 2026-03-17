using Microsoft.AspNetCore.Mvc;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace KarnelTravelGuide.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hiển thị trang Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // Xử lý khi nhấn nút Đăng nhập
        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // So sánh Email và Password (tạm thời so sánh chuỗi gốc theo DB của bạn)
                var user = _context.Accounts.FirstOrDefault(a => a.Email == model.Email && a.Password == model.Password);
                
                if (user != null)
                {
                    // Lưu thông tin người dùng vào Session
                    HttpContext.Session.SetString("UserName", user.FullName);
                    HttpContext.Session.SetString("UserRole", user.Role ?? "Customer");
                    HttpContext.Session.SetInt32("UserId", user.AccountId);

                    // Phân quyền: Nếu là Admin thì vào trang quản trị
                    if (user.Role == "Admin")
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                    }
                    
                    return RedirectToAction("Index", "Home");
                }
                
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            }
            return View(model);
        }

        // Đăng xuất
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}