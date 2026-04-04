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

        // Retrieves and displays a list of available restaurants, optionally filtered by a specific tourist spot
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

            if (spotId.HasValue) 
            {
                restaurants = restaurants.Where(r => r.SpotId == spotId);
                return View(await restaurants.OrderByDescending(r => r.RestaurantId).ToListAsync());
            }

            return View(await restaurants.OrderByDescending(r => r.RestaurantId).Take(6).ToListAsync());
        }

        // Displays the table booking interface and calculates real-time table availability for the selected date and time
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

            // Calculate remaining available tables by subtracting active bookings from the total table inventory
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

        // Processes the customer's table reservation, enforces capacity limits, and generates an unpaid invoice
        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(int restaurantId, int tableId, string? resDate, string? resTime, int numberOfTables, int numberOfGuests, string? specialRequest)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null)
            {
                TempData["ErrorMessage"] = "Please log in to book a table.";
                return RedirectToAction("Login", "Account");
            }

            var table = await _context.RestaurantTables.FindAsync(tableId);
            if (table == null || numberOfTables <= 0 || numberOfGuests <= 0)
            {
                TempData["ErrorMessage"] = "Invalid booking details.";
                return RedirectToAction("Booking", new { id = restaurantId, resDate = resDate, resTime = resTime });
            }

            // Validate that the total number of guests does not exceed the allowed capacity for the selected tables
            int maxGuestsPerTable = (table.TableType?.ToUpper().Contains("VIP") == true) ? 10 : 4;
            int maxTotalGuests = maxGuestsPerTable * numberOfTables;
            if (numberOfGuests > maxTotalGuests)
            {
                TempData["ErrorMessage"] = $"Max {maxTotalGuests} guests for {numberOfTables} selected table(s).";
                return RedirectToAction("Booking", new { id = restaurantId, resDate = resDate, resTime = resTime });
            }

            DateTime resDateTime = DateTime.Parse($"{resDate} {resTime}");
            decimal totalAmount = (table.PriceRes ?? 0) * numberOfTables;

            try
            {
                // Create the root order record with a Pending status
                var order = new Order { AccountId = accountId.Value, CreateDate = DateTime.Now, TotalAmount = totalAmount, Status = "Pending" };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); 

                // Create the specific restaurant table booking details
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

                // Link the table reservation to the main order
                var orderDetail = new OrderDetail { OrderId = order.OrderId, ResBookingId = resBooking.ResBookingId, Price = totalAmount, Quantity = numberOfTables };
                _context.OrderDetails.Add(orderDetail);

                // Generate an unpaid invoice for the customer's cart
                var invoice = new Invoice { AccountId = accountId.Value, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, DiscountAmount = 0, FinalTotal = totalAmount, PaymentStatus = "Unpaid" };
                _context.Invoices.Add(invoice);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully booked {numberOfTables} tables! Let's review your invoice.";
                return RedirectToAction("MyInvoices", "Invoice", new { tab = "pending" }); 
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred, please try again.";
                return RedirectToAction("Booking", new { id = restaurantId, resDate = resDate, resTime = resTime });
            }
        }
    }
}