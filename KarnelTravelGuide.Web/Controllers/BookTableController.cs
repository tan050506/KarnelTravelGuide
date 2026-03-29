using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Controllers
{
    public class BookTableController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookTableController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Hiển thị danh sách Nhà hàng có Filter
        public async Task<IActionResult> Index(int? spotId, string resDate, string resTime)
        {
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();

            ViewBag.CurrentSpot = spotId;
            ViewBag.ResDate = resDate ?? DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.ResTime = resTime ?? "19:00"; // Mặc định 7h tối

            var restaurants = _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables)
                .AsQueryable();

            if (spotId.HasValue) restaurants = restaurants.Where(r => r.SpotId == spotId);

            return View(await restaurants.ToListAsync());
        }

        // GET: Trang chọn bàn và nhập thông tin
        [HttpGet]
        public async Task<IActionResult> Booking(int? id, string resDate, string resTime)
        {
            var accountId = HttpContext.Session.GetInt32("AccountId"); 
            if (accountId == null) 
            {
                TempData["ErrorMessage"] = "Please login to book a table.";
                return RedirectToAction("Login", "Account");
            }

            if (id == null || string.IsNullOrEmpty(resDate) || string.IsNullOrEmpty(resTime)) 
                return RedirectToAction("Index");

            var restaurant = await _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables)
                .FirstOrDefaultAsync(m => m.RestaurantId == id);

            if (restaurant == null) return NotFound();

            DateTime resDateTime = DateTime.Parse($"{resDate} {resTime}");

            // Tính số lượng bàn CÒN TRỐNG (Kiểm tra trùng ngày)
            var availableTablesDict = new Dictionary<int, int>();
            foreach(var table in restaurant.RestaurantTables)
            {
                var bookedTables = await _context.OrderDetails
                    .Where(od => od.ResBooking != null 
                              && od.ResBooking.TableId == table.TableId
                              && od.ResBooking.ReservationDateTime.HasValue
                              && od.ResBooking.ReservationDateTime.Value.Date == resDateTime.Date
                              && od.Order != null 
                              && od.Order.Status != "Canceled")
                    .SumAsync(od => (int?)od.Quantity) ?? 0;

                availableTablesDict[table.TableId] = table.Quantity - bookedTables;
            }

            ViewBag.ResDate = resDate;
            ViewBag.ResTime = resTime;
            ViewBag.AvailableTables = availableTablesDict;

            return View(restaurant);
        }

        // POST: Xử lý lưu Booking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Booking(int restaurantId, int tableId, string resDate, string resTime, int numberOfTables, int numberOfGuests, string specialRequest)
        {
            var accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            var table = await _context.RestaurantTables.Include(t => t.Restaurant).FirstOrDefaultAsync(t => t.TableId == tableId);
            if (table == null || numberOfTables <= 0) return NotFound();

            DateTime resDateTime = DateTime.Parse($"{resDate} {resTime}");

            try 
            {
                // Tổng tiền = Giá bàn * Số lượng bàn
                decimal totalAmount = (table.PriceRes ?? 0) * numberOfTables;

                // 1. Tạo Đơn hàng (Order)
                var order = new Order { AccountId = accountId.Value, CreateDate = DateTime.Now, TotalAmount = totalAmount, Status = "Pending" };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); 

                // 2. Tạo Phiếu đặt bàn (RestaurantBooking)
                var resBooking = new RestaurantBooking
                {
                    TableId = tableId,
                    ReservationDateTime = resDateTime,
                    NumberOfGuests = numberOfGuests,
                    SpecialRequest = specialRequest,
                    TotalAmount = totalAmount
                };
                _context.RestaurantBookings.Add(resBooking);
                await _context.SaveChangesAsync(); 

                // 3. Tạo Chi tiết Đơn hàng (OrderDetail) - Lưu Quantity là số lượng bàn
                var orderDetail = new OrderDetail { OrderId = order.OrderId, ResBookingId = resBooking.ResBookingId, Price = totalAmount, Quantity = numberOfTables };
                _context.OrderDetails.Add(orderDetail);

                // 4. Tạo Hóa đơn (Invoice)
                var invoice = new Invoice { AccountId = accountId.Value, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, DiscountAmount = 0, FinalTotal = totalAmount, PaymentStatus = "Unpaid" };
                _context.Invoices.Add(invoice);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully booked {numberOfTables} tables ({table.TableType})! Please check your invoice.";
                return RedirectToAction("Index"); 
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred, please try again.";
                return RedirectToAction("Booking", new { id = restaurantId, resDate = resDate, resTime = resTime });
            }
        }
    }
}