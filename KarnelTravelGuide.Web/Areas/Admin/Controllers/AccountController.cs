using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===================== INDEX =====================
        public async Task<IActionResult> Index(string searchString)
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

            ViewBag.FirstAdminId = await GetFirstAdminId();
            ViewBag.CurrentFilter = searchString;

            return View(await query.ToListAsync());
        }

        // ===================== CREATE =====================
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

        // ===================== EDIT =====================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound();

            if (id == await GetFirstAdminId())
            {
                TempData["Error"] = "Không thể sửa Admin đầu tiên.";
                return RedirectToAction(nameof(Index));
            }

            LoadDropdowns(account);
            return View(account);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Account account)
        {
            if (id != account.AccountId) return NotFound();

            if (id == await GetFirstAdminId())
            {
                TempData["Error"] = "Không thể sửa Admin đầu tiên.";
                return RedirectToAction(nameof(Index));
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

        // ===================== DELETE =====================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id == await GetFirstAdminId())
            {
                TempData["Error"] = "Không thể xoá Admin đầu tiên.";
                return RedirectToAction(nameof(Index));
            }

            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                _context.Remove(account);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xoá thành công.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS =====================

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

            // Regex phone VN
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