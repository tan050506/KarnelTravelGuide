using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using KarnelTravelGuide.Web.Attributes;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    [RoleAuthorize("Manager")]
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoiceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. TRANG PENDING PAYMENTS (ĐÃ THÊM PHÂN TRANG)
        public async Task<IActionResult> Index(string? searchString, string? sortOrder, int page = 1)
        {
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "date_asc" : "";

            var query = _context.Invoices
                .Include(i => i.Account)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!)
                .Where(i => i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Submitted");

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(i => 
                    i.Account != null && (
                        (i.Account.FullName != null && i.Account.FullName.Contains(searchString)) ||
                        (i.Account.PhoneNumber != null && i.Account.PhoneNumber.Contains(searchString))
                    ));
            }

            var rawInvoices = await query.ToListAsync();
            var uniqueInvoices = rawInvoices.GroupBy(i => i.InvoiceId).Select(g => g.First()).ToList();

            var grouped = uniqueInvoices
                .GroupBy(i => new { 
                    i.AccountId, 
                    TimeKey = i.CreatedDate.HasValue ? i.CreatedDate.Value.ToString("yyyy-MM-dd HH:mm") : "" 
                })
                .Select(g => new PendingBillViewModel
                {
                    Account = g.First().Account,
                    TotalServices = g.Count(), 
                    SubTotal = g.Sum(i => i.SubTotal ?? 0),
                    OrderDate = g.First().CreatedDate
                });

            if (sortOrder == "date_asc") grouped = grouped.OrderBy(g => g.OrderDate);
            else grouped = grouped.OrderByDescending(g => g.OrderDate);

            var sortedGroups = grouped.ToList();

            // PHÂN TRANG
            int pageSize = 10;
            int totalItems = sortedGroups.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedPending = sortedGroups.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedPending);
        }

        // 2. TRANG PAYMENT HISTORY
        public async Task<IActionResult> History(string? searchString, string? sortOrder, int page = 1)
        {
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "date_asc" : "";

            var query = _context.Invoices
                .Include(i => i.Account)
                .Include(i => i.Order)
                .Where(i => i.CreatedDate.HasValue && (i.PaymentStatus != "Unpaid" || (i.Order != null && i.Order.Status == "Canceled")))
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(i => 
                    i.Account != null && (
                        (i.Account.FullName != null && i.Account.FullName.Contains(searchString)) ||
                        (i.Account.PhoneNumber != null && i.Account.PhoneNumber.Contains(searchString))
                    ));
            }

            var rawHistory = await query.ToListAsync();
            var uniqueHistory = rawHistory.GroupBy(i => i.InvoiceId).Select(g => g.First()).ToList();

            var historyGroups = uniqueHistory
                .GroupBy(i => i.CreatedDate!.Value.Date)
                .Select(g => new HistoryGroupViewModel
                {
                    Date = g.Key,
                    TotalCustomers = g.Select(i => i.AccountId).Distinct().Count(),
                    TotalInvoices = g.Count(),
                    TotalRevenue = g.Where(i => i.PaymentStatus == "Paid").Sum(i => i.FinalTotal ?? 0)
                });

            if (sortOrder == "date_asc") historyGroups = historyGroups.OrderBy(g => g.Date);
            else historyGroups = historyGroups.OrderByDescending(g => g.Date);

            var sortedGroups = historyGroups.ToList();

            int pageSize = 10;
            int totalItems = sortedGroups.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedHistory = sortedGroups.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedHistory);
        }

        // 3. THANH TOÁN (CUSTOMER BILL)
        public async Task<IActionResult> CustomerBill(int accountId, string orderDateStr)
        {
            if (!DateTime.TryParseExact(orderDateStr, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime exactTimeMinute)) return RedirectToAction(nameof(Index));

            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null) return NotFound();

            var rawInvoices = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Submitted" && i.CreatedDate.HasValue
                            && i.CreatedDate.Value.Year == exactTimeMinute.Year
                            && i.CreatedDate.Value.Month == exactTimeMinute.Month
                            && i.CreatedDate.Value.Day == exactTimeMinute.Day
                            && i.CreatedDate.Value.Hour == exactTimeMinute.Hour
                            && i.CreatedDate.Value.Minute == exactTimeMinute.Minute)
                .ToListAsync();

            var invoices = rawInvoices.GroupBy(i => i.InvoiceId).Select(g => g.First()).ToList();

            if (!invoices.Any())
            {
                TempData["ErrorMessage"] = "No pending bills found for this customer.";
                return RedirectToAction(nameof(Index));
            }

            int distinctServices = invoices.Count;
            decimal subTotal = invoices.Sum(i => i.SubTotal ?? 0);
            
            decimal discountPercent = distinctServices >= 2 ? 0.1m : 0m;
            decimal discountAmount = subTotal * discountPercent;
            decimal finalTotal = subTotal - discountAmount;

            ViewBag.Account = account;
            ViewBag.TotalServices = distinctServices;
            ViewBag.SubTotal = subTotal;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.FinalTotal = finalTotal;
            ViewBag.HasDiscount = distinctServices >= 2;
            ViewBag.OrderDateStr = orderDateStr;

            return View(invoices);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(int accountId, string orderDateStr)
        {
            if (!DateTime.TryParseExact(orderDateStr, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime exactTimeMinute)) return RedirectToAction(nameof(Index));

            var rawInvoices = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Submitted" && i.CreatedDate.HasValue
                            && i.CreatedDate.Value.Year == exactTimeMinute.Year
                            && i.CreatedDate.Value.Month == exactTimeMinute.Month
                            && i.CreatedDate.Value.Day == exactTimeMinute.Day
                            && i.CreatedDate.Value.Hour == exactTimeMinute.Hour
                            && i.CreatedDate.Value.Minute == exactTimeMinute.Minute)
                .ToListAsync();

            var invoices = rawInvoices.GroupBy(i => i.InvoiceId).Select(g => g.First()).ToList();
            if (!invoices.Any()) return RedirectToAction(nameof(Index));

            int distinctServices = invoices.Count;
            bool applyDiscount = distinctServices >= 2;

            foreach (var inv in invoices)
            {
                if (applyDiscount)
                {
                    inv.DiscountAmount = (inv.SubTotal ?? 0) * 0.1m;
                    inv.FinalTotal = (inv.SubTotal ?? 0) - inv.DiscountAmount;
                }
                else
                {
                    inv.DiscountAmount = 0;
                    inv.FinalTotal = inv.SubTotal;
                }

                inv.PaymentStatus = "Paid";
                if (inv.Order != null) inv.Order.Status = "Confirmed";
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Payment successful! {(applyDiscount ? "10% multi-service discount applied." : "")}";
            return RedirectToAction(nameof(History));
        }

        // TÍNH NĂNG MỚI: HỦY ĐƠN CHỜ THANH TOÁN (Tại trang Checkout)
        [HttpPost]
        public async Task<IActionResult> CancelPendingPayment(int accountId, string orderDateStr)
        {
            if (!DateTime.TryParseExact(orderDateStr, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime exactTimeMinute)) return RedirectToAction(nameof(Index));

            var rawInvoices = await _context.Invoices
                .Include(i => i.Order)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Submitted" && i.CreatedDate.HasValue
                            && i.CreatedDate.Value.Year == exactTimeMinute.Year
                            && i.CreatedDate.Value.Month == exactTimeMinute.Month
                            && i.CreatedDate.Value.Day == exactTimeMinute.Day
                            && i.CreatedDate.Value.Hour == exactTimeMinute.Hour
                            && i.CreatedDate.Value.Minute == exactTimeMinute.Minute)
                .ToListAsync();

            if (rawInvoices.Any())
            {
                foreach (var inv in rawInvoices)
                {
                    inv.PaymentStatus = "Canceled";
                    if (inv.Order != null) inv.Order.Status = "Canceled";
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Pending payment canceled successfully! All services have been released.";
            }
            return RedirectToAction(nameof(Index));
        }


        // 4. CHI TIẾT NGÀY (DAILY DETAILS)
        public async Task<IActionResult> DailyDetails(string dateStr, string? searchString, string? sortOrder, int page = 1)
        {
            if (!DateTime.TryParse(dateStr, out DateTime date)) return RedirectToAction(nameof(History));

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";

            var rawInvoices = _context.Invoices
                .Include(i => i.Account)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .Where(i => i.CreatedDate.HasValue && i.CreatedDate.Value.Date == date.Date && (i.PaymentStatus != "Unpaid" || (i.Order != null && i.Order.Status == "Canceled")))
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                rawInvoices = rawInvoices.Where(i => 
                    (i.Account != null && i.Account.FullName != null && i.Account.FullName.Contains(searchString)) ||
                    (i.Account != null && i.Account.PhoneNumber != null && i.Account.PhoneNumber.Contains(searchString)));
            }

            var invoicesList = await rawInvoices.ToListAsync();
            var uniqueInvoices = invoicesList.GroupBy(i => i.InvoiceId).Select(g => g.First());

            var customerGroups = uniqueInvoices
                .GroupBy(i => new { 
                    i.AccountId, 
                    i.PaymentStatus, 
                    TimeKey = i.CreatedDate.HasValue ? i.CreatedDate.Value.ToString("yyyy-MM-dd HH:mm") : "" 
                })
                .Select(g => new DailyCustomerInvoiceViewModel
                {
                    Account = g.First().Account,
                    PaymentStatus = g.Key.PaymentStatus ?? "",
                    CheckoutTime = g.First().CreatedDate,
                    Invoices = g.ToList(),
                    FinalTotal = g.Sum(i => i.FinalTotal ?? 0),
                    TotalItems = g.Count()
                });

            if (sortOrder == "name_desc") customerGroups = customerGroups.OrderByDescending(g => g.Account?.FullName);
            else customerGroups = customerGroups.OrderBy(g => g.Account?.FullName);

            var sortedGroups = customerGroups.ToList();

            int pageSize = 10;
            int totalItems = sortedGroups.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedGroups = sortedGroups.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.SelectedDate = date;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedGroups);
        }

        // 5. TRANG CHI TIẾT CỦA 1 KHÁCH (INVOICE RECEIPT)
        public async Task<IActionResult> CustomerDailyInvoice(int accountId, string exactTimeStr)
        {
            if (!DateTime.TryParseExact(exactTimeStr, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime exactTimeMinute)) return RedirectToAction(nameof(History));

            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null) return NotFound();

            var rawInvoices = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .Where(i => i.AccountId == accountId && i.CreatedDate.HasValue 
                            && i.CreatedDate.Value.Year == exactTimeMinute.Year
                            && i.CreatedDate.Value.Month == exactTimeMinute.Month
                            && i.CreatedDate.Value.Day == exactTimeMinute.Day
                            && i.CreatedDate.Value.Hour == exactTimeMinute.Hour
                            && i.CreatedDate.Value.Minute == exactTimeMinute.Minute
                            && (i.PaymentStatus != "Unpaid" || (i.Order != null && i.Order.Status == "Canceled")))
                .ToListAsync();

            var uniqueInvoices = rawInvoices.GroupBy(i => i.InvoiceId).Select(g => g.First()).ToList();

            ViewBag.Account = account;
            ViewBag.SelectedDate = exactTimeMinute;
            return View(uniqueInvoices);
        }

        [HttpPost]
        public async Task<IActionResult> CancelCustomerInvoices(int accountId, string exactTimeStr)
        {
            if (!DateTime.TryParseExact(exactTimeStr, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime exactTimeMinute)) return RedirectToAction(nameof(History));

            var invoices = await _context.Invoices.Include(i => i.Order)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Paid" && i.CreatedDate.HasValue
                            && i.CreatedDate.Value.Year == exactTimeMinute.Year
                            && i.CreatedDate.Value.Month == exactTimeMinute.Month
                            && i.CreatedDate.Value.Day == exactTimeMinute.Day
                            && i.CreatedDate.Value.Hour == exactTimeMinute.Hour
                            && i.CreatedDate.Value.Minute == exactTimeMinute.Minute)
                .ToListAsync();

            if (invoices.Any())
            {
                foreach (var inv in invoices)
                {
                    inv.PaymentStatus = "Canceled";
                    if (inv.Order != null) inv.Order.Status = "Canceled";
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "All invoices for this checkout have been canceled and services released.";
            }
            
            return RedirectToAction(nameof(DailyDetails), new { dateStr = exactTimeMinute.ToString("yyyy-MM-dd") });
        }
    }

    public class PendingBillViewModel
    {
        public Account? Account { get; set; }
        public int TotalServices { get; set; }
        public decimal SubTotal { get; set; }
        public DateTime? OrderDate { get; set; }
    }

    public class HistoryGroupViewModel
    {
        public DateTime Date { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class DailyCustomerInvoiceViewModel
    {
        public Account? Account { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? CheckoutTime { get; set; }
        public List<Invoice> Invoices { get; set; } = new();
        public decimal FinalTotal { get; set; }
        public int TotalItems { get; set; }
    }
}