using KarnelTravelGuide.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Attributes;
using KarnelTravelGuide.Web.Models.Entities;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? filter = "day", string? detail = "")
        {
            // Aggregate system metrics for the dashboard overview
            ViewBag.TotalBranches = await _context.Branches.CountAsync();
            ViewBag.TotalAccounts = await _context.Accounts.CountAsync();
            var invoices = await _context.Invoices
                .Include(i => i.Order)
                .Where(i => i.CreatedDate != null && i.Order != null && i.Order.Status != "Pending")
                .ToListAsync();

            ViewBag.TotalInvoices = invoices.Count;
            List<string> labels = new List<string>();
            List<decimal> dataPoints = new List<decimal>();

            // Group revenue data by Month/Year if the monthly filter is applied
            if (filter == "month")
            {
                var revenueData = invoices
                    .GroupBy(i => new { i.CreatedDate.Value.Year, i.CreatedDate.Value.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new {
                        Label = $"{g.Key.Month}/{g.Key.Year}",
                        Revenue = g.Sum(i => i.FinalTotal ?? 0)
                    }).ToList();

                labels = revenueData.Select(x => x.Label).ToList();
                dataPoints = revenueData.Select(x => x.Revenue).ToList();
            }
            else
            {
                // Group revenue data daily for the last 30 days by default
                var thirtyDaysAgo = DateTime.Now.AddDays(-30);
                var revenueData = invoices
                    .Where(i => i.CreatedDate >= thirtyDaysAgo)
                    .GroupBy(i => new { i.CreatedDate.Value.Year, i.CreatedDate.Value.Month, i.CreatedDate.Value.Day })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ThenBy(g => g.Key.Day)
                    .Select(g => new {
                        Label = $"{g.Key.Day}/{g.Key.Month}",
                        Revenue = g.Sum(i => i.FinalTotal ?? 0)
                    }).ToList();

                labels = revenueData.Select(x => x.Label).ToList();
                dataPoints = revenueData.Select(x => x.Revenue).ToList();
            }

            ViewBag.ChartLabels = labels;
            ViewBag.ChartData = dataPoints;

            // Load specific entity datasets for detailed UI tables based on user interaction
            if (detail == "branches")
            {
                ViewBag.DetailBranches = await _context.Branches.ToListAsync();
            }
            else if (detail == "accounts")
            {
                ViewBag.DetailAccounts = await _context.Accounts
                    .Include(a => a.Role)
                    .Include(a => a.Branch)
                    .ToListAsync();
            }
            else if (detail == "invoices")
            {
                // Fetch the 50 most recent valid invoices for the detailed view
                ViewBag.DetailInvoices = await _context.Invoices
                    .Include(i => i.Account)
                    .Include(i => i.Order)
                    .Where(i => i.Order != null && i.Order.Status != "Pending")
                    .OrderByDescending(i => i.CreatedDate)
                    .Take(50)
                    .ToListAsync();
            }

            ViewBag.CurrentFilter = filter;
            ViewBag.CurrentDetail = detail;

            return View();
        }
    }
}