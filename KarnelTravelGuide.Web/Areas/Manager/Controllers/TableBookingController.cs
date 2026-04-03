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
    public class TableBookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        // THÊM Ổ KHÓA CHỐNG SPAM CLICK ĐÚP
        private static readonly ConcurrentDictionary<string, bool> _inFlightRequests = new();

        public TableBookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. INDEX (ĐÃ THÊM PHÂN TRANG)
        public async Task<IActionResult> Index(string? searchString, string? resDate, string? sortOrder, int page = 1)
        {
            var query = _context.Orders
                .Include(o => o.Account)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant)
                .Where(o => o.OrderDetails!.Any(od => od.ResBookingId != null) && o.Status != "Pending")
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => 
                    (o.Account!.PhoneNumber != null && o.Account!.PhoneNumber.Contains(searchString)) ||
                    (o.Account!.FullName != null && o.Account!.FullName.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(resDate) && DateTime.TryParse(resDate, out DateTime parsedDate))
            {
                query = query.Where(o => o.OrderDetails!.Any(od => od.ResBooking!.ReservationDateTime.HasValue && od.ResBooking.ReservationDateTime.Value.Date == parsedDate.Date));
            }

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentDate"] = resDate;
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
                TempData["SuccessMessage"] = "Table booking confirmed successfully!";
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
                TempData["SuccessMessage"] = "Booking canceled! The tables have been automatically released.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. DETAILS
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Account)
                .Include(o => o.OrderDetails!).ThenInclude(od => od.ResBooking!).ThenInclude(rb => rb.RestaurantTable!).ThenInclude(rt => rt.Restaurant!).ThenInclude(r => r.Spot)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // 5. GET: Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Customers = await _context.Accounts.Where(a => a.RoleId == 3).ToListAsync();
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();
            ViewBag.Restaurants = await _context.Restaurants.Include(r => r.Spot).Include(r => r.RestaurantTables).ToListAsync();
            return View();
        }

        // 6. GET: SelectTable
        public async Task<IActionResult> SelectTable(int restaurantId, string? resDate, string? resTime, string? customerType, int? accountId, string? walkInName, string? walkInPhone)
        {
            if (string.IsNullOrEmpty(resDate) || string.IsNullOrEmpty(resTime))
            {
                TempData["ErrorMessage"] = "Reservation date and time are required.";
                return RedirectToAction(nameof(Create));
            }

            var restaurant = await _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables)
                .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);

            if (restaurant == null) return NotFound();

            DateTime resDateTime = DateTime.Parse($"{resDate} {resTime}");

            var availableTablesDict = new Dictionary<int, int>();
            foreach (var table in restaurant.RestaurantTables)
            {
                var bookedTables = await _context.OrderDetails
                    .Where(od => od.ResBooking != null
                              && od.ResBooking.TableId == table.TableId
                              && od.ResBooking.ReservationDateTime.HasValue
                              && od.ResBooking.ReservationDateTime.Value.Date == resDateTime.Date
                              && od.Order != null && od.Order.Status != "Canceled")
                    .SumAsync(od => (int?)od.Quantity) ?? 0;

                availableTablesDict[table.TableId] = table.Quantity - bookedTables;
            }

            ViewBag.CustomerType = customerType;
            ViewBag.AccountId = accountId;
            ViewBag.WalkInName = walkInName;
            ViewBag.WalkInPhone = walkInPhone;
            ViewBag.ResDate = resDate;
            ViewBag.ResTime = resTime;
            ViewBag.AvailableTables = availableTablesDict;

            return View(restaurant);
        }

        // 7. POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? AccountId, string? CustomerType, string? WalkInName, string? WalkInPhone, int RestaurantId, int TableId, string? ResDate, string? ResTime, int NumberOfTables, int NumberOfGuests, string? SpecialRequest)
        {
            if (string.IsNullOrEmpty(ResDate) || string.IsNullOrEmpty(ResTime))
            {
                TempData["ErrorMessage"] = "Reservation date and time are required.";
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

            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableId == TableId);
            if (table == null || NumberOfTables <= 0) return NotFound();

            int maxGuestsPerTable = (table.TableType?.ToUpper().Contains("VIP") == true) ? 10 : 4;
            int maxTotalGuests = maxGuestsPerTable * NumberOfTables;
            if (NumberOfGuests > maxTotalGuests)
            {
                TempData["ErrorMessage"] = $"Max {maxTotalGuests} guests for {NumberOfTables} selected table(s).";
                return RedirectToAction(nameof(Create));
            }

            DateTime resDateTime = DateTime.Parse($"{ResDate} {ResTime}");
            decimal totalAmount = (table.PriceRes ?? 0) * NumberOfTables;

            string requestKey = $"TableOrder_{finalAccountId}_{TableId}_{ResDate}_{ResTime}";
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

                var resBooking = new RestaurantBooking { TableId = TableId, ReservationDateTime = resDateTime, NumberOfGuests = NumberOfGuests, SpecialRequest = SpecialRequest, TotalAmount = totalAmount };
                _context.RestaurantBookings.Add(resBooking);
                await _context.SaveChangesAsync();

                _context.OrderDetails.Add(new OrderDetail { OrderId = order.OrderId, ResBookingId = resBooking.ResBookingId, Price = totalAmount, Quantity = NumberOfTables });
                _context.Invoices.Add(new Invoice { AccountId = finalAccountId, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, FinalTotal = totalAmount, PaymentStatus = "Unpaid" }); 
                
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully booked {NumberOfTables} tables! Please review and confirm it below.";
                return RedirectToAction(nameof(Index));
            }
            finally
            {
                _inFlightRequests.TryRemove(requestKey, out _);
            }
        }
    }
}