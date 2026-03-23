using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

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

        // GET: /Admin/Account/Index
        public async Task<IActionResult> Index()
        {
            var accounts = await _context.Accounts
                .Include(a => a.Branch)
                .Include(a => a.Role) // Thêm dòng này để lấy tên Quyền
                .ToListAsync();
            return View(accounts);
        }

        // GET: /Admin/Account/Create
        public IActionResult Create()
        {
            ViewBag.BranchId = new SelectList(_context.Branches, "BranchId", "BranchName");
            ViewBag.RoleId = new SelectList(_context.Roles, "RoleId", "RoleName"); // Lấy danh sách Role
            return View();
        }

        // POST: /Admin/Account/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Account account)
        {
            if (ModelState.IsValid)
            {
                _context.Add(account);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.BranchId = new SelectList(_context.Branches, "BranchId", "BranchName", account.BranchId);
            ViewBag.RoleId = new SelectList(_context.Roles, "RoleId", "RoleName", account.RoleId);
            return View(account);
        }

        // GET: /Admin/Account/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound();

            ViewBag.BranchId = new SelectList(_context.Branches, "BranchId", "BranchName", account.BranchId);
            ViewBag.RoleId = new SelectList(_context.Roles, "RoleId", "RoleName", account.RoleId); // Lấy danh sách Role
            return View(account);
        }

        // POST: /Admin/Account/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Account account)
        {
            if (id != account.AccountId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(account);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountExists(account.AccountId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.BranchId = new SelectList(_context.Branches, "BranchId", "BranchName", account.BranchId);
            ViewBag.RoleId = new SelectList(_context.Roles, "RoleId", "RoleName", account.RoleId);
            return View(account);
        }

        // POST: /Admin/Account/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool AccountExists(int id)
        {
            return _context.Accounts.Any(e => e.AccountId == id);
        }
    }
}