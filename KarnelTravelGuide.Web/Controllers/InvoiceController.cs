using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;
using KarnelTravelGuide.Web.Models.Entities;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Controllers
{
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoiceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. TRANG PENDING (Giỏ hàng của Khách)
        // ==========================================
        public async Task<IActionResult> MyInvoices(string? sortOrder, int page = 1)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null)
            {
                TempData["ErrorMessage"] = "Please log in to view your bookings.";
                return RedirectToAction("Login", "Account");
            }

            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "date_asc" : "";

            var pendingInvoices = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending")
                .ToListAsync();

            int distinctServices = pendingInvoices.SelectMany(i => i.Order?.OrderDetails ?? new List<OrderDetail>()).Count();
            decimal subTotal = pendingInvoices.Sum(i => i.SubTotal ?? 0);
            decimal discountAmount = distinctServices >= 2 ? subTotal * 0.1m : 0m;
            decimal finalTotal = subTotal - discountAmount;

            ViewBag.TotalServices = distinctServices;
            ViewBag.SubTotal = subTotal;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.FinalTotal = finalTotal;
            ViewBag.HasDiscount = distinctServices >= 2;

            var sortedInvoices = string.IsNullOrEmpty(sortOrder) ? pendingInvoices.OrderByDescending(i => i.InvoiceId).ToList() : pendingInvoices.OrderBy(i => i.InvoiceId).ToList();
            
            int pageSize = 10;
            int totalItems = sortedInvoices.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;
            
            var pagedInvoices = sortedInvoices.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedInvoices);
        }

        // ==========================================
        // 2. TRANG LỊCH SỬ (Gộp theo Lần Checkout)
        // ==========================================
        public async Task<IActionResult> History(string? searchString, string? sortOrder, int page = 1)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "date_asc" : "";

            var query = _context.Invoices
                .Include(i => i.Order)
                .Where(i => i.AccountId == accountId && i.CreatedDate.HasValue && (i.PaymentStatus != "Unpaid" || (i.Order != null && (i.Order.Status == "Canceled" || i.Order.Status == "Submitted"))))
                .AsQueryable();

            var rawHistory = await query.ToListAsync();
            var uniqueHistory = rawHistory.GroupBy(i => i.InvoiceId).Select(g => g.First()).ToList();

            // Nhóm theo thời gian Checkout (CreatedDate chính xác đến phút)
            var historyGroups = uniqueHistory
                .GroupBy(i => i.CreatedDate.HasValue ? i.CreatedDate.Value.ToString("yyyy-MM-dd HH:mm") : "")
                .Select(g => new CustomerCheckoutBatchViewModel
                {
                    TransactionTime = g.First().CreatedDate ?? DateTime.MinValue,
                    TotalServices = g.Count(),
                    TotalPaid = g.Sum(i => i.FinalTotal ?? 0),
                    Status = g.FirstOrDefault()?.PaymentStatus == "Paid" ? "Paid" : 
                             g.FirstOrDefault()?.Order?.Status == "Canceled" ? "Canceled" : "Processing"
                });

            if (sortOrder == "date_asc") historyGroups = historyGroups.OrderBy(g => g.TransactionTime);
            else historyGroups = historyGroups.OrderByDescending(g => g.TransactionTime);

            var sortedList = historyGroups.ToList();

            if (!string.IsNullOrEmpty(searchString))
            {
                sortedList = sortedList.Where(g => g.TransactionTime.ToString("dd/MM/yyyy HH:mm").Contains(searchString)).ToList();
            }

            int pageSize = 10;
            int totalItems = sortedList.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedHistory = sortedList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedHistory);
        }

        // ==========================================
        // 3. TRANG CHI TIẾT CỦA 1 LẦN CHECKOUT
        // ==========================================
        public async Task<IActionResult> HistoryDetails(string exactTimeStr, string? searchString, string? sortOrder, int page = 1)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            // Format used when generating link: yyyy-MM-dd HH:mm
            if (!DateTime.TryParseExact(exactTimeStr, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime exactTimeMinute)) return RedirectToAction(nameof(History));

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_asc" : "";

            var rawInvoices = _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .Where(i => i.AccountId == accountId && i.CreatedDate.HasValue
                            && i.CreatedDate.Value.Year == exactTimeMinute.Year
                            && i.CreatedDate.Value.Month == exactTimeMinute.Month
                            && i.CreatedDate.Value.Day == exactTimeMinute.Day
                            && i.CreatedDate.Value.Hour == exactTimeMinute.Hour
                            && i.CreatedDate.Value.Minute == exactTimeMinute.Minute)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                rawInvoices = rawInvoices.Where(i => i.InvoiceId.ToString().Contains(searchString));
            }

            var invoicesList = await rawInvoices.ToListAsync();
            var uniqueInvoices = invoicesList.GroupBy(i => i.InvoiceId).Select(g => g.First());

            if (sortOrder == "id_asc") uniqueInvoices = uniqueInvoices.OrderBy(i => i.InvoiceId);
            else uniqueInvoices = uniqueInvoices.OrderByDescending(i => i.InvoiceId);

            var sortedInvoices = uniqueInvoices.ToList();

            int pageSize = 10;
            int totalItems = sortedInvoices.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedInvoices = sortedInvoices.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.SelectedDate = exactTimeMinute;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedInvoices);
        }

        // ==========================================
        // 4. TRANG BIÊN LAI (KIỂM TRA HỦY TRƯỚC 3 NGÀY)
        // ==========================================
        public async Task<IActionResult> Receipt(int id)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            var invoice = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .FirstOrDefaultAsync(i => i.InvoiceId == id && i.AccountId == accountId);

            if (invoice == null) return NotFound();

            // Tìm ngày bắt đầu sớm nhất trong hóa đơn
            DateTime? earliestDate = null;
            if (invoice.Order?.OrderDetails != null)
            {
                foreach (var od in invoice.Order.OrderDetails)
                {
                    if (od.RoomBooking != null && od.RoomBooking.CheckInDate.HasValue)
                    {
                        var d = od.RoomBooking.CheckInDate.Value.ToDateTime(TimeOnly.MinValue);
                        if (earliestDate == null || d < earliestDate) earliestDate = d;
                    }
                    if (od.TicketBooking != null && od.TicketBooking.TravelDate.HasValue)
                    {
                        var d = od.TicketBooking.TravelDate.Value.ToDateTime(TimeOnly.MinValue);
                        if (earliestDate == null || d < earliestDate) earliestDate = d;
                    }
                    if (od.ResBooking != null && od.ResBooking.ReservationDateTime.HasValue)
                    {
                        var d = od.ResBooking.ReservationDateTime.Value;
                        if (earliestDate == null || d < earliestDate) earliestDate = d;
                    }
                }
            }

            // Kiểm tra điều kiện 3 ngày (72 giờ)
            bool canCancel = false;
            if (earliestDate.HasValue)
            {
                var timeDiff = earliestDate.Value - DateTime.Now;
                canCancel = timeDiff.TotalDays >= 3;
            }

            ViewBag.EarliestDate = earliestDate;
            ViewBag.CanCancel = canCancel;

            return View(invoice);
        }

        // ==========================================
        // 5. API: XỬ LÝ HỦY HÓA ĐƠN TRONG LỊCH SỬ
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> CancelPaidInvoice(int invoiceId)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            var invoice = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.AccountId == accountId);

            if (invoice != null && invoice.Order?.Status == "Submitted")
            {
                DateTime? earliestDate = null;
                if (invoice.Order?.OrderDetails != null)
                {
                    foreach (var od in invoice.Order.OrderDetails)
                    {
                        if (od.RoomBooking != null && od.RoomBooking.CheckInDate.HasValue)
                        {
                            var d = od.RoomBooking.CheckInDate.Value.ToDateTime(TimeOnly.MinValue);
                            if (earliestDate == null || d < earliestDate) earliestDate = d;
                        }
                        if (od.TicketBooking != null && od.TicketBooking.TravelDate.HasValue)
                        {
                            var d = od.TicketBooking.TravelDate.Value.ToDateTime(TimeOnly.MinValue);
                            if (earliestDate == null || d < earliestDate) earliestDate = d;
                        }
                        if (od.ResBooking != null && od.ResBooking.ReservationDateTime.HasValue)
                        {
                            var d = od.ResBooking.ReservationDateTime.Value;
                            if (earliestDate == null || d < earliestDate) earliestDate = d;
                        }
                    }
                }

                if (earliestDate.HasValue && (earliestDate.Value - DateTime.Now).TotalDays >= 3)
                {
                    invoice.PaymentStatus = "Canceled";
                    if (invoice.Order != null) invoice.Order.Status = "Canceled";
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Invoice canceled successfully. Your services have been released and a refund is being processed.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Cannot cancel. The service starts in less than 3 days.";
                }
            }
            return RedirectToAction(nameof(Receipt), new { id = invoiceId });
        }

        // ==========================================
        // 6. GỬI & XÓA GIỎ HÀNG (PENDING)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> ConfirmAllInvoices()
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            var pendingInvoices = await _context.Invoices
                .Include(i => i.Order)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending")
                .ToListAsync();

            if (!pendingInvoices.Any()) return RedirectToAction(nameof(MyInvoices));

            DateTime submissionTime = DateTime.Now;
            foreach (var inv in pendingInvoices)
            {
                if (inv.Order != null) inv.Order.Status = "Submitted";
                inv.CreatedDate = submissionTime;
            }
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Your booking has been successfully submitted! Please wait for the manager to confirm and collect payment.";
            return RedirectToAction(nameof(History));
        }

        [HttpPost]
        public async Task<IActionResult> CancelAllInvoices()
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            var pendingInvoices = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending")
                .ToListAsync();

            if (!pendingInvoices.Any()) return RedirectToAction(nameof(MyInvoices));

            foreach (var inv in pendingInvoices)
            {
                if (inv.Order != null)
                {
                    foreach (var od in inv.Order.OrderDetails)
                    {
                        if (od.RoomBooking != null) _context.RoomBookings.Remove(od.RoomBooking);
                        if (od.TicketBooking != null) _context.TicketBookings.Remove(od.TicketBooking);
                        if (od.ResBooking != null) _context.RestaurantBookings.Remove(od.ResBooking);
                        _context.OrderDetails.Remove(od);
                    }
                    _context.Orders.Remove(inv.Order);
                }
                _context.Invoices.Remove(inv);
            }
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Successfully canceled all pending drafts. Cart cleared.";
            return RedirectToAction(nameof(MyInvoices));
        }
    }

    public class CustomerHistoryGroupViewModel
    {
        public DateTime Date { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class CustomerCheckoutBatchViewModel
    {
        public DateTime TransactionTime { get; set; }
        public int TotalServices { get; set; }
        public decimal TotalPaid { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}