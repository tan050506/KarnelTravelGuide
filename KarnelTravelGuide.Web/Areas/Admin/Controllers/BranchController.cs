using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using KarnelTravelGuide.Web.Attributes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin")]
    public class BranchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BranchController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Branches.ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Branch branch, IFormFile? imageFile)
        {
            ValidateBranch(branch, isEdit: false);

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "branches");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    branch.ImageUrl = "/images/branches/" + uniqueFileName;
                }

                _context.Add(branch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(branch);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            return View(branch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Branch branch, IFormFile? imageFile)
        {
            if (id != branch.BranchId) return NotFound();

            ValidateBranch(branch, isEdit: true);

            if (ModelState.IsValid)
            {
                var existing = await _context.Branches.FindAsync(id);
                if (existing == null) return NotFound();

                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "branches");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    existing.ImageUrl = "/images/branches/" + uniqueFileName;
                }

                existing.BranchName = branch.BranchName;
                existing.Address = branch.Address;
                existing.PhoneBranch = branch.PhoneBranch;
                existing.EmailBranch = branch.EmailBranch;
                // Không cập nhật ImageUrl ở đây nều không có file mới, vì đã cập nhật bên trên nếu có file

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(branch);
        }

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

        private void ValidateBranch(Branch branch, bool isEdit)
        {
            if (_context.Branches.Any(b => b.BranchName == branch.BranchName &&
                (!isEdit || b.BranchId != branch.BranchId)))
            {
                ModelState.AddModelError("BranchName", "Tên chi nhánh đã tồn tại.");
            }

            if (!string.IsNullOrEmpty(branch.PhoneBranch) &&
                !Regex.IsMatch(branch.PhoneBranch, @"^(0[3|5|7|8|9])[0-9]{8}$"))
            {
                ModelState.AddModelError("PhoneBranch", "SĐT không hợp lệ.");
            }

            if (!string.IsNullOrEmpty(branch.EmailBranch) &&
                !Regex.IsMatch(branch.EmailBranch, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ModelState.AddModelError("EmailBranch", "Email không hợp lệ.");
            }
        }
    }
}