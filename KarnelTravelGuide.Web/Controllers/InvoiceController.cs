using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace KarnelTravelGuide.Web.Controllers
{
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoiceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hiển thị Giỏ hàng (Pending) và Lịch sử Đặt chỗ (History) của Khách hàng
        public async Task<IActionResult> MyInvoices(string? tab = "pending")
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null)
            {
                TempData["ErrorMessage"] = "Please log in to view your bookings.";
                return RedirectToAction("Login", "Auth");
            }

            ViewData["ActiveTab"] = tab;

            var query = _context.Invoices
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation)
                .Include(i => i.Order!).ThenInclude(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .Where(i => i.AccountId == accountId);

            if (tab == "pending")
            {
                // Tab 1: Các dịch vụ đang chờ thanh toán
                var pendingInvoices = await query
                    .Where(i => i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending")
                    .ToListAsync();

                int totalServices = pendingInvoices.Sum(i => i.Order?.OrderDetails?.Sum(od => od.Quantity ?? 1) ?? 0);
                decimal subTotal = pendingInvoices.Sum(i => i.SubTotal ?? 0);
                
                // Logic giảm 10% nếu >= 2 dịch vụ
                decimal discountAmount = totalServices >= 2 ? subTotal * 0.1m : 0m;
                decimal finalTotal = subTotal - discountAmount;

                ViewBag.TotalServices = totalServices;
                ViewBag.SubTotal = subTotal;
                ViewBag.DiscountAmount = discountAmount;
                ViewBag.FinalTotal = finalTotal;
                ViewBag.HasDiscount = totalServices >= 2;

                return View(pendingInvoices);
            }
            else
            {
                // Tab 2: Lịch sử dịch vụ đã thanh toán hoặc đã hủy
                var historyInvoices = await query
                    .Where(i => i.PaymentStatus != "Unpaid" || (i.Order != null && (i.Order.Status == "Canceled" || i.Order.Status == "Submitted")))
                    .OrderByDescending(i => i.CreatedDate)
                    .ToListAsync();

                return View(historyInvoices);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmAllInvoices()
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Auth");

            var pendingInvoices = await _context.Invoices
                .Include(i => i.Order)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending")
                .ToListAsync();

            if (!pendingInvoices.Any()) return RedirectToAction(nameof(MyInvoices));

            foreach (var inv in pendingInvoices)
            {
                if (inv.Order != null)
                {
                    inv.Order.Status = "Submitted";
                }
            }
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Your booking has been successfully submitted to the branch manager! Please visit the counter to confirm and pay.";
            return RedirectToAction(nameof(MyInvoices), new { tab = "history" });
        }

        [HttpPost]
        public async Task<IActionResult> CancelAllInvoices()
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Auth");

            var pendingInvoices = await _context.Invoices
                .Include(i => i.Order)
                .Where(i => i.AccountId == accountId && i.PaymentStatus == "Unpaid" && i.Order != null && i.Order.Status == "Pending")
                .ToListAsync();

            if (!pendingInvoices.Any()) return RedirectToAction(nameof(MyInvoices));

            foreach (var inv in pendingInvoices)
            {
                inv.PaymentStatus = "Canceled";
                if (inv.Order != null)
                {
                    inv.Order.Status = "Canceled";
                }
            }
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Successfully canceled all pending bookings. Cart cleared.";
            return RedirectToAction(nameof(MyInvoices));
        }
    }
}