using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class BranchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BranchController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===================== INDEX =====================
        public async Task<IActionResult> Index()
        {
            return View(await _context.Branches.ToListAsync());
        }

        // ===================== CREATE =====================
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Branch branch)
        {
            ValidateBranch(branch, isEdit: false);

            if (ModelState.IsValid)
            {
                _context.Add(branch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(branch);
        }

        // ===================== EDIT =====================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            return View(branch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Branch branch)
        {
            if (id != branch.BranchId) return NotFound();

            ValidateBranch(branch, isEdit: true);

            if (ModelState.IsValid)
            {
                var existing = await _context.Branches.FindAsync(id);
                if (existing == null) return NotFound();

                // update an toàn
                existing.BranchName = branch.BranchName;
                existing.Address = branch.Address;
                existing.PhoneBranch = branch.PhoneBranch;
                existing.EmailBranch = branch.EmailBranch;
                existing.ImageUrl = branch.ImageUrl;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(branch);
        }

        // ===================== DELETE =====================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return RedirectToAction(nameof(Index));

            bool isUsed =
                _context.Accounts.Any(a => a.BranchId == id) ||
                _context.TouristSpots.Any(s => s.BranchId == id) ||
                _context.Transportations.Any(t => t.FromBranchId == id);

            if (isUsed)
            {
                TempData["Error"] = "Chi nhánh đang được sử dụng, không thể xoá.";
                return RedirectToAction(nameof(Index));
            }

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xoá chi nhánh thành công.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== VALIDATION =====================
        private void ValidateBranch(Branch branch, bool isEdit)
        {
            // 🔥 Tên không được trùng
            if (_context.Branches.Any(b => b.BranchName == branch.BranchName &&
                (!isEdit || b.BranchId != branch.BranchId)))
            {
                ModelState.AddModelError("BranchName", "Tên chi nhánh đã tồn tại.");
            }

            // 🔥 Validate phone
            if (!string.IsNullOrEmpty(branch.PhoneBranch) &&
                !Regex.IsMatch(branch.PhoneBranch, @"^(0[3|5|7|8|9])[0-9]{8}$"))
            {
                ModelState.AddModelError("PhoneBranch", "SĐT không hợp lệ.");
            }

            // 🔥 Validate email
            if (!string.IsNullOrEmpty(branch.EmailBranch) &&
                !Regex.IsMatch(branch.EmailBranch, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ModelState.AddModelError("EmailBranch", "Email không hợp lệ.");
            }
        }
    }
}