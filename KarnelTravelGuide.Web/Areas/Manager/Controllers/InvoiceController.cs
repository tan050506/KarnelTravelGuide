using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoiceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. INDEX: Hiển thị Khách hàng đang có hóa đơn chờ (Pending) và Lịch sử
        public async Task<IActionResult> Index(string? searchString, string? tab = "pending")
        {
            ViewData["ActiveTab"] = tab;
            ViewData["CurrentSearch"] = searchString;

            if (tab == "pending")
            {
                // Thêm dấu ! để báo với C# rằng Order và OrderDetails sẽ không null ở đây
                var query = _context.Invoices
                    .Include(i => i.Account)
                    .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!)
                    .Where(i => i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending");

                if (!string.IsNullOrEmpty(searchString))
                {
                    // Kiểm tra i.Account != null trước khi .FullName để tránh CS8602
                    query = query.Where(i => 
                        i.Account != null && (
                            (i.Account.FullName != null && i.Account.FullName.Contains(searchString)) ||
                            (i.Account.PhoneNumber != null && i.Account.PhoneNumber.Contains(searchString))
                        ));
                }

                var unpaidInvoices = await query.ToListAsync();

                var grouped = unpaidInvoices
                    .GroupBy(i => i.Account)
                    .Select(g => new PendingBillViewModel
                    {
                        Account = g.Key,
                        TotalInvoices = g.Count(),
                        // Dùng ?. và ?? 0 để đảm bảo an toàn tuyệt đối
                        TotalServices = g.Sum(i => i.Order?.OrderDetails?.Sum(od => od.Quantity ?? 1) ?? 0),
                        SubTotal = g.Sum(i => i.SubTotal ?? 0)
                    }).ToList();

                ViewBag.PendingGroups = grouped;
                return View();
            }
            else
            {
                var query = _context.Invoices
                    .Include(i => i.Account)
                    .Include(i => i.Order)
                    .Where(i => i.PaymentStatus != "Unpaid" || (i.Order != null && i.Order.Status == "Canceled"))
                    .OrderByDescending(i => i.CreatedDate)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchString))
                {
                    query = query.Where(i => 
                        (i.Account != null && i.Account.FullName != null && i.Account.FullName.Contains(searchString)) ||
                        (i.Account != null && i.Account.PhoneNumber != null && i.Account.PhoneNumber.Contains(searchString)) ||
                        i.InvoiceId.ToString() == searchString);
                }

                ViewBag.HistoryInvoices = await query.ToListAsync();
                return View();
            }
        }

        // 2. CUSTOMER BILL: Xem chi tiết hóa đơn tổng hợp của 1 khách
        public async Task<IActionResult> CustomerBill(int accountId)
        {
            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null) return NotFound();

            var invoices = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending")
                .ToListAsync();

            if (!invoices.Any())
            {
                TempData["ErrorMessage"] = "No pending bills found for this customer.";
                return RedirectToAction(nameof(Index));
            }

            // Dùng toán tử an toàn ?. để đếm tổng dịch vụ
            int totalServices = invoices.Sum(i => i.Order?.OrderDetails?.Sum(od => od.Quantity ?? 1) ?? 0);
            decimal subTotal = invoices.Sum(i => i.SubTotal ?? 0);
            
            // LOGIC: >= 2 dịch vụ thì giảm 10%
            decimal discountPercent = totalServices >= 2 ? 0.1m : 0m;
            decimal discountAmount = subTotal * discountPercent;
            decimal finalTotal = subTotal - discountAmount;

            ViewBag.Account = account;
            ViewBag.TotalServices = totalServices;
            ViewBag.SubTotal = subTotal;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.FinalTotal = finalTotal;
            ViewBag.HasDiscount = totalServices >= 2;

            return View(invoices);
        }

        // 3. XÁC NHẬN THANH TOÁN (TỰ ĐỘNG CONFIRM TẤT CẢ)
        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(int accountId)
        {
            var invoices = await _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending")
                .ToListAsync();

            if (!invoices.Any()) return RedirectToAction(nameof(Index));

            int totalServices = invoices.Sum(i => i.Order?.OrderDetails?.Sum(od => od.Quantity ?? 1) ?? 0);
            bool applyDiscount = totalServices >= 2;

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
                
                // TỰ ĐỘNG XÁC NHẬN CÁC DỊCH VỤ KIA NGAY LẬP TỨC
                if (inv.Order != null)
                {
                    inv.Order.Status = "Confirmed";
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Payment successful! {(applyDiscount ? "A 10% multi-service discount was applied. " : "")}All associated services have been automatically confirmed.";
            return RedirectToAction(nameof(Index), new { tab = "history" });
        }

        // 4. DETAILS: Lịch sử 1 hóa đơn
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Account)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null) return NotFound();
            return View(invoice);
        }
    }

    public class PendingBillViewModel
    {
        public Account? Account { get; set; }
        public int TotalInvoices { get; set; }
        public int TotalServices { get; set; }
        public decimal SubTotal { get; set; }
    }
}