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
    public class TicketBookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TicketBookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. INDEX
        public async Task<IActionResult> Index(string searchString, string travelDate)
        {
            var query = _context.Orders
                .Include(o => o.Account)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation!).ThenInclude(t => t.ToSpot)
                .Where(o => o.OrderDetails!.Any(od => od.TicketBookingId != null))
                .AsQueryable();

#pragma warning disable CS8602
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => 
                    (o.Account.PhoneNumber != null && o.Account.PhoneNumber.Contains(searchString)) ||
                    (o.Account.FullName != null && o.Account.FullName.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(travelDate) && DateTime.TryParse(travelDate, out DateTime parsedDate))
            {
                DateOnly date = DateOnly.FromDateTime(parsedDate);
                query = query.Where(o => o.OrderDetails!.Any(od => od.TicketBooking.TravelDate == date));
            }
#pragma warning restore CS8602

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentDate = travelDate;

            return View(await query.OrderByDescending(o => o.OrderId).ToListAsync());
        }

        // 2. XÁC NHẬN ĐƠN HÀNG (Nhấn dấu tích xanh ngoài Index)
        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = "Confirmed";
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId);
                if (invoice != null) invoice.PaymentStatus = "Paid"; 

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xác nhận đơn hàng thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // 3. HỦY ĐƠN HÀNG (Chỉ đổi trạng thái, KHÔNG XÓA DATA để giữ lịch sử và tránh lỗi SQL)
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order != null)
            {
                order.Status = "Canceled"; // Đánh dấu hủy đơn
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId);
                if (invoice != null) invoice.PaymentStatus = "Canceled"; // Đánh dấu hủy hóa đơn

                // Bỏ đoạn code xóa vé đi. SQL sẽ không báo lỗi nữa.
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã hủy đơn hàng thành công! Ghế đã được tự động giải phóng.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. XEM CHI TIẾT
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Account)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation!).ThenInclude(t => t.FromBranch)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation!).ThenInclude(t => t.ToSpot)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // 5. GET: Create (TRANG TÌM KIẾM VÀ NHẬP THÔNG TIN)
        public async Task<IActionResult> Create()
        {
            ViewBag.Customers = await _context.Accounts.Where(a => a.RoleId == 3).ToListAsync();
            ViewBag.Routes = await _context.Transportations.Include(t => t.FromBranch).Include(t => t.ToSpot).ToListAsync();
            ViewBag.Branches = await _context.Branches.ToListAsync();
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();
            return View();
        }

        // 6. GET: SelectSeat (TRANG CHỌN GHẾ - CHUYỂN HƯỚNG TỪ CREATE)
        public async Task<IActionResult> SelectSeat(int transportationId, string travelDate, string customerType, int? accountId, string walkInName, string walkInPhone)
        {
            var transport = await _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .FirstOrDefaultAsync(t => t.TransportationId == transportationId);

            if (transport == null) return NotFound();

            // Lưu trữ thông tin khách hàng để đẩy tiếp sang trang chọn ghế
            ViewBag.CustomerType = customerType;
            ViewBag.AccountId = accountId;
            ViewBag.WalkInName = walkInName;
            ViewBag.WalkInPhone = walkInPhone;
            ViewBag.TravelDate = travelDate;

            return View(transport);
        }

        // 7. POST: Create (XỬ LÝ ĐẶT VÉ VÀ LƯU VÀO DATABASE VỚI TRẠNG THÁI PENDING)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? AccountId, string CustomerType, string WalkInName, string WalkInPhone, int TransportationId, DateTime TravelDate, string Seat)
        {
            int finalAccountId = 0;

            if (CustomerType == "WalkIn")
            {
                if (string.IsNullOrEmpty(WalkInName) || string.IsNullOrEmpty(WalkInPhone))
                {
                    TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ Tên và Số điện thoại khách vãng lai.";
                    return RedirectToAction(nameof(Create));
                }

                var existingAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.PhoneNumber == WalkInPhone);
                if (existingAccount != null)
                {
                    TempData["ErrorMessage"] = $"Số điện thoại {WalkInPhone} đã được đăng ký cho khách hàng '{existingAccount.FullName}'. Vui lòng chọn 'Khách có tài khoản' hoặc nhập SĐT khác.";
                    return RedirectToAction(nameof(Create));
                }
                else
                {
                    var newGuest = new Account { FullName = WalkInName, PhoneNumber = WalkInPhone, Email = Guid.NewGuid().ToString().Substring(0, 8) + "@walkin.com", RoleId = 3 };
                    _context.Accounts.Add(newGuest);
                    await _context.SaveChangesAsync();
                    finalAccountId = newGuest.AccountId;
                }
            }
            else
            {
                if (AccountId == null) { TempData["ErrorMessage"] = "Vui lòng chọn khách hàng."; return RedirectToAction(nameof(Create)); }
                finalAccountId = AccountId.Value;
            }

            var transport = await _context.Transportations.FindAsync(TransportationId);
            if (transport == null || string.IsNullOrEmpty(Seat)) return NotFound();

            string[] selectedSeats = Seat.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            decimal unitPrice = transport.PriceTransport ?? 0;
            decimal totalAmount = unitPrice * selectedSeats.Length;
            
            // TRẠNG THÁI ĐƠN HÀNG LÀ PENDING
            var order = new Order { AccountId = finalAccountId, CreateDate = DateTime.Now, TotalAmount = totalAmount, Status = "Pending" };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var seatId in selectedSeats)
            {
                var ticket = new TicketBooking { TransportationId = TransportationId, TravelDate = DateOnly.FromDateTime(TravelDate), Seat = seatId.Trim(), TotalAmount = unitPrice };
                _context.TicketBookings.Add(ticket);
                await _context.SaveChangesAsync();

                _context.OrderDetails.Add(new OrderDetail { OrderId = order.OrderId, TicketBookingId = ticket.TicketBookingId, Price = unitPrice, Quantity = 1 });
            }

            _context.Invoices.Add(new Invoice { AccountId = finalAccountId, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, FinalTotal = totalAmount, PaymentStatus = "Unpaid" });
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Đã tạo đơn hàng {selectedSeats.Length} vé! Vui lòng xác nhận trên danh sách.";
            return RedirectToAction(nameof(Index));
        }

        // 8. API LẤY DANH SÁCH GHẾ (Thông minh: Bỏ qua các ghế thuộc đơn hàng Canceled)
        [HttpGet]
        public async Task<IActionResult> GetBookedSeats(int transportationId, string date)
        {
            if (!DateTime.TryParse(date, out DateTime parsedDate)) return Json(new List<string>());

            DateOnly travelDate = DateOnly.FromDateTime(parsedDate);
            
            // Tìm qua OrderDetail để check được trạng thái của Order
            var bookedSeats = await _context.OrderDetails
                .Where(od => od.TicketBooking != null 
                          && od.TicketBooking.TransportationId == transportationId 
                          && od.TicketBooking.TravelDate == travelDate
                          && od.Order != null 
                          && od.Order.Status != "Canceled") // QUAN TRỌNG: Loại bỏ ghế của đơn đã hủy
                .Select(od => od.TicketBooking!.Seat)
                .ToListAsync();

            return Json(bookedSeats);
        }
    }
}