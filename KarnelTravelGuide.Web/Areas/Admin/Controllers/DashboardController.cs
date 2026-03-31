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
            ViewBag.TotalBranches = await _context.Branches.CountAsync();
            ViewBag.TotalAccounts = await _context.Accounts.CountAsync();
            ViewBag.TotalInvoices = await _context.Invoices.CountAsync();

            var invoices = await _context.Invoices.Where(i => i.CreatedDate != null).ToListAsync();
            List<string> labels = new List<string>();
            List<decimal> dataPoints = new List<decimal>();

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
                ViewBag.DetailInvoices = await _context.Invoices
                    .Include(i => i.Account)
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