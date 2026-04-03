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
    public class RoomBookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        // THÊM Ổ KHÓA CHỐNG SPAM CLICK ĐÚP
        private static readonly ConcurrentDictionary<string, bool> _inFlightRequests = new();

        public RoomBookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. INDEX (ĐÃ THÊM PHÂN TRANG)
        public async Task<IActionResult> Index(string? searchString, string? checkInDate, string? sortOrder, int page = 1)
        {
            var query = _context.Orders
                .Include(o => o.Account)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay)
                .Where(o => o.OrderDetails!.Any(od => od.RoomBookingId != null) && o.Status != "Pending")
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => 
                    (o.Account!.PhoneNumber != null && o.Account!.PhoneNumber.Contains(searchString)) ||
                    (o.Account!.FullName != null && o.Account!.FullName.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(checkInDate) && DateTime.TryParse(checkInDate, out DateTime parsedDate))
            {
                DateOnly date = DateOnly.FromDateTime(parsedDate);
                query = query.Where(o => o.OrderDetails!.Any(od => od.RoomBooking!.CheckInDate == date));
            }

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentDate"] = checkInDate;
            ViewData["CurrentSort"] = sortOrder;
            
            // Mặc định là hiển thị MỚI NHẤT (desc). Bấm vào link sẽ đổi thành id_asc
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_asc" : "";

            switch (sortOrder)
            {
                case "id_asc": query = query.OrderBy(o => o.OrderId); break;
                default: query = query.OrderByDescending(o => o.OrderId); break; // MẶC ĐỊNH LUÔN LÀ DESCENDING
            }

            var rawOrders = await query.ToListAsync();
            
            // Lọc trùng lặp
            var uniqueOrders = rawOrders.GroupBy(o => o.OrderId).Select(g => g.First()).ToList();

            // XỬ LÝ PHÂN TRANG
            int pageSize = 10;
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
                TempData["SuccessMessage"] = "Room order confirmed successfully!";
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
                if (order.Status == "Confirmed")
                {
                    TempData["ErrorMessage"] = "Cannot cancel a confirmed booking.";
                    return RedirectToAction(nameof(Index));
                }

                order.Status = "Canceled"; 
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId);
                if (invoice != null) invoice.PaymentStatus = "Canceled"; 

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Order canceled! The rooms have been automatically released.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. DETAILS
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Account)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.RoomBooking!).ThenInclude(rb => rb.Room!).ThenInclude(r => r.Stay!).ThenInclude(s => s.Spot)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // 5. GET: Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Customers = await _context.Accounts.Where(a => a.RoleId == 3).ToListAsync();
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();
            ViewBag.Stays = await _context.Stays.Include(s => s.Spot).Include(s => s.Rooms).ToListAsync();
            return View();
        }

        // 6. GET: SelectRoom
        public async Task<IActionResult> SelectRoom(int stayId, string? checkIn, string? checkOut, string? customerType, int? accountId, string? walkInName, string? walkInPhone)
        {
            // FIX CẢNH BÁO NULL REFERENCE: Bắt buộc CheckIn và CheckOut phải có giá trị
            if (string.IsNullOrEmpty(checkIn) || string.IsNullOrEmpty(checkOut))
            {
                TempData["ErrorMessage"] = "Check-in and Check-out dates are required.";
                return RedirectToAction(nameof(Create));
            }

            var stay = await _context.Stays
                .Include(s => s.Spot)
                .Include(s => s.Rooms)
                .FirstOrDefaultAsync(s => s.StayId == stayId);

            if (stay == null) return NotFound();

            // Thêm dấu '!' báo cho compiler biết biến chắc chắn không null
            DateOnly dateIn = DateOnly.FromDateTime(DateTime.Parse(checkIn!));
            DateOnly dateOut = DateOnly.FromDateTime(DateTime.Parse(checkOut!));

            var availableRoomsDict = new Dictionary<int, int>();
            foreach (var room in stay.Rooms)
            {
                var bookedRooms = await _context.OrderDetails
                    .Where(od => od.RoomBooking != null
                              && od.RoomBooking.RoomId == room.RoomId
                              && od.RoomBooking.CheckInDate < dateOut
                              && od.RoomBooking.CheckOutDate > dateIn
                              && od.Order != null && od.Order.Status != "Canceled")
                    .SumAsync(od => (int?)od.RoomBooking!.NumberOfRooms) ?? 0;

                availableRoomsDict[room.RoomId] = room.Quantity - bookedRooms;
            }

            ViewBag.CustomerType = customerType;
            ViewBag.AccountId = accountId;
            ViewBag.WalkInName = walkInName;
            ViewBag.WalkInPhone = walkInPhone;
            ViewBag.CheckIn = checkIn;
            ViewBag.CheckOut = checkOut;
            ViewBag.TotalNights = dateOut.DayNumber - dateIn.DayNumber;
            ViewBag.AvailableRooms = availableRoomsDict;

            return View(stay);
        }

        // 7. POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? AccountId, string? CustomerType, string? WalkInName, string? WalkInPhone, int StayId, int RoomId, string? CheckIn, string? CheckOut, int NumberOfRooms)
        {
            // FIX CẢNH BÁO NULL REFERENCE
            if (string.IsNullOrEmpty(CheckIn) || string.IsNullOrEmpty(CheckOut))
            {
                TempData["ErrorMessage"] = "Check-in and Check-out dates are required.";
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

            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.RoomId == RoomId);
            if (room == null || NumberOfRooms <= 0) return NotFound();

            DateOnly dateIn = DateOnly.FromDateTime(DateTime.Parse(CheckIn!));
            DateOnly dateOut = DateOnly.FromDateTime(DateTime.Parse(CheckOut!));
            int totalNights = dateOut.DayNumber - dateIn.DayNumber;
            decimal totalAmount = (room.PriceRoom ?? 0) * NumberOfRooms * totalNights;

            // KHÓA YÊU CẦU: Chặn click đúp khi tạo đơn hàng
            string requestKey = $"RoomOrder_{finalAccountId}_{RoomId}_{CheckIn}_{CheckOut}";
            if (!_inFlightRequests.TryAdd(requestKey, true))
            {
                TempData["ErrorMessage"] = "Processing your booking... Please avoid double-clicking.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // CHỐNG SPAM / DOUBLE-CLICK ở tầng Database (Khoảng thời gian 30s)
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

                var roomBooking = new RoomBooking { RoomId = RoomId, CheckInDate = dateIn, CheckOutDate = dateOut, NumberOfRooms = NumberOfRooms, TotalAmount = totalAmount };
                _context.RoomBookings.Add(roomBooking);
                await _context.SaveChangesAsync();

                _context.OrderDetails.Add(new OrderDetail { OrderId = order.OrderId, RoomBookingId = roomBooking.RoomBookingId, Price = totalAmount, Quantity = 1 });
                _context.Invoices.Add(new Invoice { AccountId = finalAccountId, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, FinalTotal = totalAmount, PaymentStatus = "Unpaid" }); 
                
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully created an order for {NumberOfRooms} rooms! Please review and confirm it below.";
                return RedirectToAction(nameof(Index));
            }
            finally
            {
                _inFlightRequests.TryRemove(requestKey, out _);
            }
        }
    }
}