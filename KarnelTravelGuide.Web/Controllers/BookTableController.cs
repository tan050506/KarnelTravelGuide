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

        public async Task<IActionResult> Index(int? spotId, string? resDate, string? resTime)
        {
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();

            ViewBag.CurrentSpot = spotId;
            ViewBag.ResDate = resDate ?? DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.ResTime = resTime ?? "19:00"; 

            var restaurants = _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables)
                .AsQueryable();

            if (spotId.HasValue) restaurants = restaurants.Where(r => r.SpotId == spotId);

            return View(await restaurants.ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Booking(int? id, string? resDate, string? resTime)
        {
            if (id == null) return NotFound();

            var restaurant = await _context.Restaurants
                .Include(r => r.Spot)
                .Include(r => r.RestaurantTables)
                .FirstOrDefaultAsync(r => r.RestaurantId == id);

            if (restaurant == null) return NotFound();

            ViewBag.ResDate = resDate ?? DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.ResTime = resTime ?? "19:00";

            DateTime resDateTime = DateTime.Parse($"{ViewBag.ResDate} {ViewBag.ResTime}");

            var availableTables = new Dictionary<int, int>();
            foreach (var table in restaurant.RestaurantTables)
            {
                var bookedTables = await _context.OrderDetails
                    .Where(od => od.ResBooking != null
                              && od.ResBooking.TableId == table.TableId
                              && od.ResBooking.ReservationDateTime.HasValue
                              && od.ResBooking.ReservationDateTime.Value.Date == resDateTime.Date
                              && od.Order != null && od.Order.Status != "Canceled")
                    .SumAsync(od => (int?)od.Quantity) ?? 0;

                availableTables[table.TableId] = table.Quantity - bookedTables;
            }

            ViewBag.AvailableTables = availableTables;
            return View(restaurant);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(int restaurantId, int tableId, string? resDate, string? resTime, int numberOfTables, int numberOfGuests, string? specialRequest)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null)
            {
                TempData["ErrorMessage"] = "Please log in to book a table.";
                return RedirectToAction("Login", "Auth");
            }

            var table = await _context.RestaurantTables.FindAsync(tableId);
            if (table == null || numberOfTables <= 0 || numberOfGuests <= 0)
            {
                TempData["ErrorMessage"] = "Invalid booking details.";
                return RedirectToAction("Booking", new { id = restaurantId, resDate = resDate, resTime = resTime });
            }

            DateTime resDateTime = DateTime.Parse($"{resDate} {resTime}");
            decimal totalAmount = (table.PriceRes ?? 0) * numberOfTables;

            try
            {
                var order = new Order { AccountId = accountId.Value, CreateDate = DateTime.Now, TotalAmount = totalAmount, Status = "Pending" };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); 

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

                var orderDetail = new OrderDetail { OrderId = order.OrderId, ResBookingId = resBooking.ResBookingId, Price = totalAmount, Quantity = numberOfTables };
                _context.OrderDetails.Add(orderDetail);

                var invoice = new Invoice { AccountId = accountId.Value, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, DiscountAmount = 0, FinalTotal = totalAmount, PaymentStatus = "Unpaid" };
                _context.Invoices.Add(invoice);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully booked {numberOfTables} tables ({table.TableType})! You can review your bookings in your profile.";
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