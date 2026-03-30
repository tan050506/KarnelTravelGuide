using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using KarnelTravelGuide.Web.Attributes;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin")]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, int? roleId)
        {
            var query = _context.Accounts
                .Include(a => a.Branch)
                .Include(a => a.Role)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(a =>
                    (a.FullName != null && a.FullName.Contains(searchString)) ||
                    (a.Email != null && a.Email.Contains(searchString)) ||
                    (a.PhoneNumber != null && a.PhoneNumber.Contains(searchString)));
            }

            if (roleId.HasValue)
            {
                query = query.Where(a => a.RoleId == roleId);
            }

            ViewBag.RoleList = new SelectList(_context.Roles, "RoleId", "RoleName");

            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentRole = roleId;

            ViewBag.FirstAdminId = await GetFirstAdminId();

            return View(await query.ToListAsync());
        }

        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Account account)
        {
            ValidateAccount(account, isEdit: false);

            if (ModelState.IsValid)
            {
                if (account.RoleId != 2)
                    account.BranchId = null;

                _context.Add(account);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            LoadDropdowns(account);
            return View(account);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.Include(a => a.Role).FirstOrDefaultAsync(a => a.AccountId == id);
            if (account == null) return NotFound();

            var firstAdminId = await GetFirstAdminId();
            var currentUserId = HttpContext.Session.GetInt32("AccountId");

            if (account.Role?.RoleName == "Admin" && currentUserId != firstAdminId)
            {
                if (currentUserId != id)
                {
                    TempData["Error"] = "Bạn không có quyền sửa các Admin khác. Bạn chỉ có thể sửa bản thân.";
                    return RedirectToAction(nameof(Index));
                }
            }

            LoadDropdowns(account);
            return View(account);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Account account)
        {
            if (id != account.AccountId) return NotFound();

            var firstAdminId = await GetFirstAdminId();
            var currentUserId = HttpContext.Session.GetInt32("AccountId");

            var existingAuthCheck = await _context.Accounts.AsNoTracking().Include(a => a.Role).FirstOrDefaultAsync(a => a.AccountId == id);
            if (existingAuthCheck == null) return NotFound();

            if (existingAuthCheck.Role?.RoleName == "Admin" && currentUserId != firstAdminId)
            {
                if (currentUserId != id)
                {
                    TempData["Error"] = "Bạn không có quyền sửa các Admin khác. Bạn chỉ có thể sửa bản thân.";
                    return RedirectToAction(nameof(Index));
                }
            }

            ValidateAccount(account, isEdit: true);

            if (ModelState.IsValid)
            {
                var existing = await _context.Accounts.FindAsync(id);
                if (existing == null) return NotFound();

                existing.FullName = account.FullName;
                existing.Email = account.Email;
                existing.PhoneNumber = account.PhoneNumber;
                existing.Address = account.Address;
                existing.RoleId = account.RoleId;
                existing.BranchId = account.RoleId == 2 ? account.BranchId : null;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            LoadDropdowns(account);
            return View(account);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var firstAdminId = await GetFirstAdminId();
            var currentUserId = HttpContext.Session.GetInt32("AccountId");

            if (id == firstAdminId)
            {
                TempData["Error"] = "Không thể xoá Admin gốc của hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            if (id == currentUserId)
            {
                TempData["Error"] = "Bạn không thể tự xoá chính mình.";
                return RedirectToAction(nameof(Index));
            }

            var account = await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Orders)
                .FirstOrDefaultAsync(a => a.AccountId == id);

            if (account != null)
            {
                if (account.Orders.Any())
                {
                    TempData["Error"] = "Không thể xoá tài khoản này vì đã tồn tại đơn hàng (Order).";
                    return RedirectToAction(nameof(Index));
                }

                if (account.Role?.RoleName == "Admin" && currentUserId != firstAdminId)
                {
                    TempData["Error"] = "Bạn không có quyền xoá các Admin khác. Chỉ Admin gốc mới được phép.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Remove(account);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xoá thành công.";
            }

            return RedirectToAction(nameof(Index));
        }

        private void ValidateAccount(Account account, bool isEdit)
        {
            if (_context.Accounts.Any(a => a.Email == account.Email &&
                (!isEdit || a.AccountId != account.AccountId)))
            {
                ModelState.AddModelError("Email", "Email đã tồn tại.");
            }

            if (_context.Accounts.Any(a => a.PhoneNumber == account.PhoneNumber &&
                (!isEdit || a.AccountId != account.AccountId)))
            {
                ModelState.AddModelError("PhoneNumber", "SĐT đã tồn tại.");
            }

            if (!Regex.IsMatch(account.PhoneNumber ?? "", @"^(0[3|5|7|8|9])[0-9]{8}$"))
            {
                ModelState.AddModelError("PhoneNumber", "SĐT không hợp lệ.");
            }

            if (account.RoleId == 2 && account.BranchId == null)
            {
                ModelState.AddModelError("BranchId", "Manager phải có chi nhánh.");
            }
        }

        private void LoadDropdowns(Account account = null)
        {
            ViewBag.BranchId = new SelectList(_context.Branches, "BranchId", "BranchName", account?.BranchId);
            ViewBag.RoleId = new SelectList(_context.Roles, "RoleId", "RoleName", account?.RoleId);
        }

        private async Task<int> GetFirstAdminId()
        {
            return await _context.Accounts
                .Where(a => a.Role.RoleName == "Admin")
                .OrderBy(a => a.AccountId)
                .Select(a => a.AccountId)
                .FirstOrDefaultAsync();
        }
    }
}