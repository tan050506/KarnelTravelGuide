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
    public class BookRoomController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookRoomController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? spotId, string checkIn, string checkOut)
        {
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();

            ViewBag.CurrentSpot = spotId;
            ViewBag.CheckIn = checkIn ?? DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.CheckOut = checkOut ?? DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");

            var stays = _context.Stays
                .Include(s => s.Spot)
                .Include(s => s.Rooms)
                .AsQueryable();

            if (spotId.HasValue) stays = stays.Where(s => s.SpotId == spotId);

            return View(await stays.ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Booking(int? id, string checkIn, string checkOut)
        {
            if (id == null) return NotFound();

            var stay = await _context.Stays
                .Include(s => s.Spot)
                .Include(s => s.Rooms)
                .FirstOrDefaultAsync(s => s.StayId == id);

            if (stay == null) return NotFound();

            ViewBag.CheckIn = checkIn ?? DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.CheckOut = checkOut ?? DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");

            DateOnly dateIn = DateOnly.FromDateTime(DateTime.Parse(ViewBag.CheckIn));
            DateOnly dateOut = DateOnly.FromDateTime(DateTime.Parse(ViewBag.CheckOut));
            ViewBag.TotalNights = dateOut.DayNumber - dateIn.DayNumber;

            var availableRooms = new Dictionary<int, int>();
            foreach (var room in stay.Rooms)
            {
                var bookedRooms = await _context.OrderDetails
                    .Where(od => od.RoomBooking != null
                              && od.RoomBooking.RoomId == room.RoomId
                              && od.RoomBooking.CheckInDate < dateOut
                              && od.RoomBooking.CheckOutDate > dateIn
                              && od.Order != null && od.Order.Status != "Canceled")
                    .SumAsync(od => (int?)od.RoomBooking!.NumberOfRooms) ?? 0;

                availableRooms[room.RoomId] = room.Quantity - bookedRooms;
            }

            ViewBag.AvailableRooms = availableRooms;
            return View(stay);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(int stayId, int roomId, string checkIn, string checkOut, int numberOfRooms)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null)
            {
                TempData["ErrorMessage"] = "Please log in to book a room.";
                return RedirectToAction("Login", "Auth");
            }

            var room = await _context.Rooms.FindAsync(roomId);
            if (room == null || numberOfRooms <= 0)
            {
                TempData["ErrorMessage"] = "Invalid room selection.";
                return RedirectToAction("Booking", new { id = stayId, checkIn = checkIn, checkOut = checkOut });
            }

            DateOnly dateIn = DateOnly.FromDateTime(DateTime.Parse(checkIn));
            DateOnly dateOut = DateOnly.FromDateTime(DateTime.Parse(checkOut));
            int totalNights = dateOut.DayNumber - dateIn.DayNumber;
            decimal totalAmount = (room.PriceRoom ?? 0) * numberOfRooms * totalNights;

            try
            {
                var order = new Order { AccountId = accountId.Value, CreateDate = DateTime.Now, TotalAmount = totalAmount, Status = "Pending" };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); 

                var roomBooking = new RoomBooking
                {
                    RoomId = roomId,
                    CheckInDate = dateIn,
                    CheckOutDate = dateOut,
                    NumberOfRooms = numberOfRooms,
                    TotalAmount = totalAmount
                };
                _context.RoomBookings.Add(roomBooking);
                await _context.SaveChangesAsync(); 

                var orderDetail = new OrderDetail { OrderId = order.OrderId, RoomBookingId = roomBooking.RoomBookingId, Price = totalAmount, Quantity = 1 };
                _context.OrderDetails.Add(orderDetail);

                var invoice = new Invoice { AccountId = accountId.Value, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, DiscountAmount = 0, FinalTotal = totalAmount, PaymentStatus = "Unpaid" };
                _context.Invoices.Add(invoice);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully booked {numberOfRooms} rooms ({room.RoomType}) for {totalNights} nights! You can review your bookings in your profile.";
                return RedirectToAction("Index"); 
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred, please try again.";
                return RedirectToAction("Booking", new { id = stayId, checkIn = checkIn, checkOut = checkOut });
            }
        }
    }
}