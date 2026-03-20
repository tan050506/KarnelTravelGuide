using Microsoft.AspNetCore.Mvc;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models;
using KarnelTravelGuide.Web.Models.Entities;
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
                    else if (user.Role == "Manager")
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Manager" });
                    }
                    
                    return RedirectToAction("Index", "Home");
                }
                
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            }
            return View(model);
        }
        
        // POST: Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra email đã tồn tại chưa
                if (_context.Accounts.Any(a => a.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                    return View(model);
                }

                var newAccount = new Account
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password, // Lưu ý: Thực tế nên Hash password (BCrypt)
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address,
                    Role = "Customer" // Mặc định là khách hàng
                };

                _context.Accounts.Add(newAccount);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đăng ký thành công! Hãy đăng nhập.";
                return RedirectToAction("Login");
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