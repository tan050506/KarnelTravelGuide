using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public class TransportationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TransportationController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private async Task<Branch> GetCurrentManagerBranchAsync()
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId.HasValue)
            {
                var currentManager = await _context.Accounts
                    .Include(a => a.Branch)
                    .FirstOrDefaultAsync(a => a.AccountId == accountId.Value);

                if (currentManager?.Branch != null) return currentManager.Branch;
            }
            
            var fallbackBranch = await _context.Branches.FirstOrDefaultAsync();
            if (fallbackBranch == null)
            {
                fallbackBranch = new Branch { BranchName = "Central Branch" };
                _context.Branches.Add(fallbackBranch);
                await _context.SaveChangesAsync();
            }
            return fallbackBranch;
        }

        public async Task<IActionResult> Index(string searchString, string sortOrder)
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_desc" : "";

            var transportations = _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .Where(t => t.FromBranchId == currentBranch.BranchId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                transportations = transportations.Where(s => 
                    (s.TransportType != null && s.TransportType.Contains(searchString)) || 
                    (s.TransportName != null && s.TransportName.Contains(searchString)) ||
                    (s.ToSpot != null && s.ToSpot.SpotName!.Contains(searchString)));
            }

            switch (sortOrder)
            {
                case "id_desc": transportations = transportations.OrderByDescending(t => t.TransportationId); break;
                default: transportations = transportations.OrderBy(t => t.TransportationId); break;
            }

            return View(await transportations.ToListAsync());
        }

        public async Task<IActionResult> Create()
        {
            var currentBranch = await GetCurrentManagerBranchAsync();
            
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.FromBranchId = currentBranch.BranchId;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TransportName,TransportType,FromBranchId,ToSpotId,DepartureTime,PriceTransport,SeatCapacity,Description")] Transportation transportation, IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) ModelState.AddModelError(string.Empty, "Transport image is required.");
            if (transportation.ToSpotId == null) ModelState.AddModelError("ToSpotId", "Please select a destination.");
            
            ModelState.Remove("FromBranch"); ModelState.Remove("ToSpot"); ModelState.Remove("TicketBookings");

            if (ModelState.IsValid)
            {
                transportation.ImageUrl = await UploadFileAsync(imageFile!);
                
                _context.Add(transportation);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transportation route created successfully!";
                return RedirectToAction(nameof(Index));
            }

            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(transportation);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var transportation = await _context.Transportations.FindAsync(id);
            if (transportation == null) return NotFound();

            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            
            return View(transportation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TransportationId,TransportName,TransportType,FromBranchId,ToSpotId,DepartureTime,PriceTransport,SeatCapacity,Description,ImageUrl")] Transportation transportation, IFormFile? imageFile)
        {
            if (id != transportation.TransportationId) return NotFound();

            ModelState.Remove("FromBranch"); ModelState.Remove("ToSpot"); ModelState.Remove("TicketBookings");

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        DeletePhysicalFile(transportation.ImageUrl);
                        transportation.ImageUrl = await UploadFileAsync(imageFile);
                    }

                    _context.Update(transportation);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Transportation route updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TransportationExists(transportation.TransportationId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            var currentBranch = await GetCurrentManagerBranchAsync();
            ViewBag.BranchName = currentBranch.BranchName;
            ViewBag.BranchAddress = currentBranch.Address;
            ViewBag.TouristSpots = await _context.TouristSpots.ToListAsync();
            return View(transportation);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var transportation = await _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .FirstOrDefaultAsync(m => m.TransportationId == id);

            if (transportation == null) return NotFound();
            return View(transportation);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var transportation = await _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .FirstOrDefaultAsync(m => m.TransportationId == id);

            if (transportation == null) return NotFound();
            return View(transportation);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var transportation = await _context.Transportations
                .Include(t => t.TicketBookings)
                .FirstOrDefaultAsync(t => t.TransportationId == id);

            if (transportation != null)
            {
                if (transportation.TicketBookings != null && transportation.TicketBookings.Any())
                {
                    TempData["ErrorMessage"] = "Cannot delete! This route has active ticket bookings.";
                    return RedirectToAction(nameof(Index));
                }

                DeletePhysicalFile(transportation.ImageUrl);
                
                _context.Transportations.Remove(transportation);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transportation route and its image deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TransportationExists(int id) => _context.Transportations.Any(e => e.TransportationId == id);

        private async Task<string> UploadFileAsync(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "transportations");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            return "/images/transportations/" + uniqueFileName;
        }

        private void DeletePhysicalFile(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            try
            {
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }
            catch (Exception) { }
        }
    }
}