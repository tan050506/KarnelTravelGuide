using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using KarnelTravelGuide.Web.Attributes;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    [RoleAuthorize("Manager")]
    public class TicketBookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        // THÊM Ổ KHÓA CHỐNG SPAM CLICK ĐÚP
        private static readonly ConcurrentDictionary<string, bool> _inFlightRequests = new();

        public TicketBookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. INDEX: Quản lý danh sách đơn (ĐÃ THÊM PHÂN TRANG)
        public async Task<IActionResult> Index(string? searchString, string? travelDate, string? sortOrder, int page = 1)
        {
            var query = _context.Orders
                .Include(o => o.Account)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation!).ThenInclude(t => t.ToSpot)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.TicketBooking!).ThenInclude(tb => tb.Transportation!).ThenInclude(t => t.FromBranch)
                .Where(o => o.OrderDetails!.Any(od => od.TicketBookingId != null) && o.Status != "Pending")
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => 
                    (o.Account!.PhoneNumber != null && o.Account!.PhoneNumber.Contains(searchString)) ||
                    (o.Account!.FullName != null && o.Account!.FullName.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(travelDate) && DateTime.TryParse(travelDate, out DateTime parsedDate))
            {
                DateOnly dateOnly = DateOnly.FromDateTime(parsedDate);
                query = query.Where(o => o.OrderDetails!.Any(od => od.TicketBooking!.TravelDate == dateOnly));
            }

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentDate"] = travelDate;
            ViewData["CurrentSort"] = sortOrder;
            
            // Nếu chưa chọn sort, mặc định là hiển thị MỚI NHẤT (desc). Bấm vào link sẽ đổi thành id_asc
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_asc" : "";

            switch (sortOrder)
            {
                case "id_asc": query = query.OrderBy(o => o.OrderId); break;
                default: query = query.OrderByDescending(o => o.OrderId); break; // MẶC ĐỊNH LUÔN LÀ DESCENDING
            }

            var rawOrders = await query.ToListAsync();
            
            // Lọc trùng lặp do 1 đơn có nhiều vé
            var uniqueOrders = rawOrders.GroupBy(o => o.OrderId).Select(g => g.First()).ToList();

            // XỬ LÝ PHÂN TRANG (PAGINATION)
            int pageSize = 10; // Giới hạn 10 đơn / 1 trang
            int totalItems = uniqueOrders.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedOrders = uniqueOrders.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Truyền dữ liệu phân trang ra View
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedOrders);
        }

        // 2. CONFIRM BOOKING
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
                TempData["SuccessMessage"] = "Ticket booking confirmed successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        // 3. CANCEL ORDER
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order != null)
            {
                order.Status = "Canceled"; 
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId);
                if (invoice != null) invoice.PaymentStatus = "Canceled"; 

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Booking canceled! The seats have been automatically released.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. DETAILS
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

        // 5. GET: Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Customers = await _context.Accounts.Where(a => a.RoleId == 3).ToListAsync();
            ViewBag.Branches = await _context.Branches.ToListAsync();
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();
            ViewBag.Routes = await _context.Transportations.Include(t => t.FromBranch).Include(t => t.ToSpot).ToListAsync();
            return View();
        }

        // 6. GET: SelectSeat
        public async Task<IActionResult> SelectSeat(int transportationId, string? travelDate, string? customerType, int? accountId, string? walkInName, string? walkInPhone)
        {
            if (string.IsNullOrEmpty(travelDate))
            {
                TempData["ErrorMessage"] = "Travel date is required.";
                return RedirectToAction(nameof(Create));
            }

            var transport = await _context.Transportations.Include(t => t.FromBranch).Include(t => t.ToSpot).FirstOrDefaultAsync(t => t.TransportationId == transportationId);
            if (transport == null) return NotFound();

            ViewBag.CustomerType = customerType;
            ViewBag.AccountId = accountId;
            ViewBag.WalkInName = walkInName;
            ViewBag.WalkInPhone = walkInPhone;
            ViewBag.TravelDate = travelDate;

            return View(transport);
        }

        // 7. POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? AccountId, string? CustomerType, string? WalkInName, string? WalkInPhone, int TransportationId, string? TravelDate, string? SelectedSeats)
        {
            if (string.IsNullOrEmpty(TravelDate))
            {
                TempData["ErrorMessage"] = "Travel date is required.";
                return RedirectToAction(nameof(Create));
            }

            if (string.IsNullOrEmpty(SelectedSeats))
            {
                TempData["ErrorMessage"] = "No seats selected. Booking failed.";
                return RedirectToAction(nameof(Create));
            }

            int finalAccountId = 0;

            if (CustomerType == "WalkIn")
            {
                if (string.IsNullOrEmpty(WalkInName) || string.IsNullOrEmpty(WalkInPhone))
                {
                    TempData["ErrorMessage"] = "Please enter full name and phone number for walk-in customer.";
                    return RedirectToAction(nameof(Create));
                }

                var existingAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.PhoneNumber == WalkInPhone);
                if (existingAccount != null)
                {
                    TempData["ErrorMessage"] = $"Phone number {WalkInPhone} is already registered. Please choose 'Existing Member'.";
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
                if (AccountId == null) { TempData["ErrorMessage"] = "Please select a customer."; return RedirectToAction(nameof(Create)); }
                finalAccountId = AccountId.Value;
            }

            var transport = await _context.Transportations.FindAsync(TransportationId);
            if (transport == null) return NotFound();

            DateOnly tDate = DateOnly.FromDateTime(DateTime.Parse(TravelDate!));
            string[] selectedSeats = SelectedSeats.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            
            decimal unitPrice = transport.PriceTransport ?? 0;
            decimal totalAmount = unitPrice * selectedSeats.Length;

            string requestKey = $"TicketOrder_{finalAccountId}_{TransportationId}_{TravelDate}_{SelectedSeats}";
            if (!_inFlightRequests.TryAdd(requestKey, true))
            {
                TempData["ErrorMessage"] = "Processing your booking... Please avoid double-clicking.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                bool isDuplicate = await _context.Orders.AnyAsync(o => 
                    o.AccountId == finalAccountId && 
                    o.TotalAmount == totalAmount && 
                    o.CreateDate >= DateTime.Now.AddSeconds(-30));

                if (isDuplicate)
                {
                    TempData["ErrorMessage"] = "This booking was already processed! Please avoid double-clicking.";
                    return RedirectToAction(nameof(Index));
                }

                var order = new Order { AccountId = finalAccountId, CreateDate = DateTime.Now, TotalAmount = totalAmount, Status = "Submitted" }; 
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var seat in selectedSeats)
                {
                    var ticket = new TicketBooking { TransportationId = TransportationId, TravelDate = tDate, Seat = seat, TotalAmount = unitPrice };
                    _context.TicketBookings.Add(ticket);
                    await _context.SaveChangesAsync();
                    _context.OrderDetails.Add(new OrderDetail { OrderId = order.OrderId, TicketBookingId = ticket.TicketBookingId, Price = unitPrice, Quantity = 1 });
                }

                _context.Invoices.Add(new Invoice { AccountId = finalAccountId, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, FinalTotal = totalAmount, PaymentStatus = "Unpaid" });
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = $"Successfully booked {selectedSeats.Length} tickets! Please review and confirm it below.";
                return RedirectToAction(nameof(Index));
            }
            finally
            {
                _inFlightRequests.TryRemove(requestKey, out _);
            }
        }

        // 8. API GET BOOKED SEATS
        [HttpGet]
        public async Task<IActionResult> GetBookedSeats(int transportationId, string? date)
        {
            if (!DateTime.TryParse(date, out DateTime parsedDate)) return Json(new List<string>());

            DateOnly travelDate = DateOnly.FromDateTime(parsedDate);
            
            var bookedSeats = await _context.OrderDetails
                .Where(od => od.TicketBooking != null 
                          && od.TicketBooking.TransportationId == transportationId 
                          && od.TicketBooking.TravelDate == travelDate
                          && od.Order != null 
                          && od.Order.Status != "Canceled") 
                .Select(od => od.TicketBooking!.Seat)
                .ToListAsync();

            return Json(bookedSeats);
        }
    }
}