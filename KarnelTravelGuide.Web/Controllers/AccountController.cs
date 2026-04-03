using Microsoft.AspNetCore.Mvc;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;
using Microsoft.EntityFrameworkCore;

namespace KarnelTravelGuide.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AccountController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = _context.Accounts
                    .Include(a => a.Role) 
                    .FirstOrDefault(a => a.Email == model.Email && a.Password == model.Password);
                
                if (user != null)
                {
                    HttpContext.Session.SetString("UserName", user.FullName ?? "");
                    
                    string userRoleName = user.Role?.RoleName ?? "Customer";
                    HttpContext.Session.SetString("UserRole", userRoleName);

                    if (!string.IsNullOrEmpty(user.AvatarUrl))
                    {
                        HttpContext.Session.SetString("UserAvatar", user.AvatarUrl);
                    }

                    HttpContext.Session.SetInt32("AccountId", user.AccountId);

                    if (userRoleName == "Admin")
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                    }
                    else if (userRoleName == "Manager")
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Manager" });
                    }
                    
                    return RedirectToAction("Index", "Home");
                }
                
                ModelState.AddModelError("", "Incorrect email or password.");
            }
            return View(model);
        }
        
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
                if (_context.Accounts.Any(a => a.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "This email is already in use.");
                    return View(model);
                }

                var customerRole = _context.Roles.FirstOrDefault(r => r.RoleName == "Customer");

                var newAccount = new Account
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password,
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address,
                    RoleId = customerRole != null ? customerRole.RoleId : 3 
                };

                _context.Accounts.Add(newAccount);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registration successful! Please login.";
                return RedirectToAction("Login");
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            
            var accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login");

            var user = await _context.Accounts.FindAsync(accountId);
            if (user == null) return RedirectToAction("Login");

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(Account model, IFormFile? avatarFile, string? newPassword)
        {
            
            var accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login");

            var user = await _context.Accounts.FindAsync(accountId);
            if (user == null) return RedirectToAction("Login");

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                user.Password = newPassword;
            }

            if (avatarFile != null && avatarFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + avatarFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }
                user.AvatarUrl = "/images/avatars/" + uniqueFileName;
            }

            _context.Update(user);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("UserName", user.FullName ?? "");
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                HttpContext.Session.SetString("UserAvatar", user.AvatarUrl);
            }
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}