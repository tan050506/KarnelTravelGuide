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

        public async Task<IActionResult> Index(string? searchString)
        {
            ViewBag.CurrentFilter = searchString;

            // Initialize the base query for branches
            var branches = _context.Branches.AsQueryable();

            // Apply multi-field search filter
            if (!string.IsNullOrEmpty(searchString))
            {
                var lowerSearch = searchString.ToLower();
                branches = branches.Where(b => 
                    (b.BranchName != null && b.BranchName.ToLower().Contains(lowerSearch)) ||
                    (b.Address != null && b.Address.ToLower().Contains(lowerSearch)) ||
                    (b.PhoneBranch != null && b.PhoneBranch.Contains(lowerSearch)) ||
                    (b.EmailBranch != null && b.EmailBranch.ToLower().Contains(lowerSearch))
                );
            }

            return View(await branches.ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Branch branch, IFormFile? imageFile)
        {
            // Validate branch uniqueness and format constraints
            ValidateBranch(branch, isEdit: false);

            if (ModelState.IsValid)
            {
                // Process and save uploaded branch image with a unique GUID filename
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

                // Process new image file if uploaded, replacing the old reference
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

                // Update core details
                existing.BranchName = branch.BranchName;
                existing.Address = branch.Address;
                existing.PhoneBranch = branch.PhoneBranch;
                existing.EmailBranch = branch.EmailBranch;

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

            // Referential integrity check: Prevent deletion if branch is tied to existing accounts, spots, or routes
            bool isUsed =
                _context.Accounts.Any(a => a.BranchId == id) ||
                _context.TouristSpots.Any(s => s.BranchId == id) ||
                _context.Transportations.Any(t => t.FromBranchId == id);

            if (isUsed)
            {
                TempData["Error"] = "Branch is in use, cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Branch deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateBranch(Branch branch, bool isEdit)
        {
            // Ensure branch names are unique across the system
            if (_context.Branches.Any(b => b.BranchName == branch.BranchName &&
                (!isEdit || b.BranchId != branch.BranchId)))
            {
                ModelState.AddModelError("BranchName", "Branch name already exists.");
            }

            // Validate phone number format
            if (!string.IsNullOrEmpty(branch.PhoneBranch) &&
                !Regex.IsMatch(branch.PhoneBranch, @"^(0[3|5|7|8|9])[0-9]{8}$"))
            {
                ModelState.AddModelError("PhoneBranch", "Invalid phone number.");
            }

            // Validate email format
            if (!string.IsNullOrEmpty(branch.EmailBranch) &&
                !Regex.IsMatch(branch.EmailBranch, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ModelState.AddModelError("EmailBranch", "Invalid email address.");
            }
        }
    }
}