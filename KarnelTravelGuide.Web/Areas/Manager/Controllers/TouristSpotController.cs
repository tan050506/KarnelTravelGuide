using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Http; 
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class TouristSpotController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TouristSpotController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET THE BRANCH OF THE CURRENTLY LOGGED-IN MANAGER (From Session)
        private async Task<Branch> GetCurrentManagerBranchAsync()
        {
            // 1. Get the logged-in user's AccountId from the Session
            int? accountId = HttpContext.Session.GetInt32("AccountId");

            if (accountId.HasValue)
            {
                // 2. Find the account in the Database and include the Branch info
                var currentManager = await _context.Accounts
                    .Include(a => a.Branch)
                    .FirstOrDefaultAsync(a => a.AccountId == accountId.Value);

                // 3. If found and assigned to a branch, return that branch
                if (currentManager != null && currentManager.Branch != null)
                {
                    return currentManager.Branch;
                }
            }

            // Fallback: In case the session drops during testing or manager has no branch,
            // grab the first branch or create a dummy one to prevent crash pages.
            var fallbackBranch = await _context.Branches.FirstOrDefaultAsync();
            if (fallbackBranch == null)
            {
                fallbackBranch = new Branch { BranchName = "Central Branch (Auto-generated)", PhoneBranch = "1900-0000" };
                _context.Branches.Add(fallbackBranch);
                await _context.SaveChangesAsync();
            }
            return fallbackBranch;
        }

        // 1. GET: Index & Search
        public async Task<IActionResult> Index(string searchString)
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewData["CurrentFilter"] = searchString;

            // Only fetch tourist spots belonging to this Manager's Branch
            var spots = _context.TouristSpots
                .Include(t => t.Branch) 
                .Where(t => t.BranchId == currentBranch.BranchId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                // Added null checks to prevent CS8602 warnings
                spots = spots.Where(s => 
                    (s.SpotName != null && s.SpotName.Contains(searchString)) || 
                    (s.Address != null && s.Address.Contains(searchString)));
            }

            return View(await spots.ToListAsync());
        }

        // 2. GET: Create
        public async Task<IActionResult> Create()
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            
            // Pass Branch info to View for read-only display
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchId = currentBranch.BranchId;
            
            return View();
        }

        // 3. POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SpotName,Address,Description,BranchId")] TouristSpot touristSpot, IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(touristSpot.SpotName)) ModelState.AddModelError("SpotName", "Spot Name is required.");
            if (imageFile == null || imageFile.Length == 0) ModelState.AddModelError(string.Empty, "Image is required.");

            // Remove navigation properties from validation
            ModelState.Remove("Branch"); 
            ModelState.Remove("Restaurants");
            ModelState.Remove("Stays");
            ModelState.Remove("Transportations");

            if (ModelState.IsValid)
            {
                // Upload Image
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "touristspots");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile!.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                touristSpot.ImageUrl = "/images/touristspots/" + uniqueFileName;
                _context.Add(touristSpot);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Tourist spot added successfully!";
                return RedirectToAction(nameof(Index));
            }

            // Reload BranchName if validation fails
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            return View(touristSpot);
        }

        // 4. GET: Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots.FindAsync(id);
            if (touristSpot == null) return NotFound();

            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            
            return View(touristSpot);
        }

        // 5. POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SpotId,SpotName,Address,Description,ImageUrl,BranchId")] TouristSpot touristSpot, IFormFile? imageFile)
        {
            if (id != touristSpot.SpotId) return NotFound();

            ModelState.Remove("Branch");
            ModelState.Remove("Restaurants");
            ModelState.Remove("Stays");
            ModelState.Remove("Transportations");

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        // Delete old image
                        if (!string.IsNullOrEmpty(touristSpot.ImageUrl))
                        {
                            string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, touristSpot.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
                        }

                        // Save new image
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "touristspots");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        touristSpot.ImageUrl = "/images/touristspots/" + uniqueFileName;
                    }

                    _context.Update(touristSpot);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tourist spot updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TouristSpotExists(touristSpot.SpotId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            return View(touristSpot);
        }

        // 6. GET: Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots
                .Include(t => t.Branch) 
                .FirstOrDefaultAsync(m => m.SpotId == id);

            if (touristSpot == null) return NotFound();

            return View(touristSpot);
        }

        // 7. GET: Delete (Confirmation Page)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var touristSpot = await _context.TouristSpots
                .Include(t => t.Branch)
                .FirstOrDefaultAsync(m => m.SpotId == id);

            if (touristSpot == null) return NotFound();

            return View(touristSpot);
        }

        // 8. POST: Delete (Process deletion in DB)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var touristSpot = await _context.TouristSpots.FindAsync(id);
            if (touristSpot != null)
            {
                // Delete image file from server
                if (!string.IsNullOrEmpty(touristSpot.ImageUrl))
                {
                    string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, touristSpot.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
                }
                
                _context.TouristSpots.Remove(touristSpot);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tourist spot deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TouristSpotExists(int id)
        {
            return _context.TouristSpots.Any(e => e.SpotId == id);
        }
    }
}