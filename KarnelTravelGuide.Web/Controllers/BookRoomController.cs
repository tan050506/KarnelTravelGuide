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
            var accountId = HttpContext.Session.GetInt32("AccountId"); 
            if (accountId == null) 
            {
                TempData["ErrorMessage"] = "Please login to book a room.";
                return RedirectToAction("Login", "Account");
            }

            if (id == null || string.IsNullOrEmpty(checkIn) || string.IsNullOrEmpty(checkOut)) 
                return RedirectToAction("Index");

            var stay = await _context.Stays
                .Include(s => s.Spot)
                .Include(s => s.Rooms)
                .FirstOrDefaultAsync(m => m.StayId == id);

            if (stay == null) return NotFound();

            DateOnly dateIn = DateOnly.FromDateTime(DateTime.Parse(checkIn));
            DateOnly dateOut = DateOnly.FromDateTime(DateTime.Parse(checkOut));

            var availableRoomsDict = new Dictionary<int, int>();
            foreach(var room in stay.Rooms)
            {
                var bookedRooms = await _context.OrderDetails
                    .Where(od => od.RoomBooking != null 
                              && od.RoomBooking.RoomId == room.RoomId
                              && od.RoomBooking.CheckInDate < dateOut 
                              && od.RoomBooking.CheckOutDate > dateIn
                              && od.Order != null 
                              && od.Order.Status != "Canceled")
                    .SumAsync(od => (int?)od.RoomBooking!.NumberOfRooms) ?? 0;

                availableRoomsDict[room.RoomId] = room.Quantity - bookedRooms;
            }

            ViewBag.CheckIn = checkIn;
            ViewBag.CheckOut = checkOut;
            ViewBag.TotalNights = dateOut.DayNumber - dateIn.DayNumber;
            ViewBag.AvailableRooms = availableRoomsDict;

            return View(stay);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Booking(int stayId, int roomId, string checkIn, string checkOut, int numberOfRooms)
        {
            var accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            var room = await _context.Rooms.Include(r => r.Stay).FirstOrDefaultAsync(r => r.RoomId == roomId);
            if (room == null || numberOfRooms <= 0) return NotFound();

            DateOnly dateIn = DateOnly.FromDateTime(DateTime.Parse(checkIn));
            DateOnly dateOut = DateOnly.FromDateTime(DateTime.Parse(checkOut));
            int totalNights = dateOut.DayNumber - dateIn.DayNumber;

            try 
            {
                // FIX LỖI CS0266 TẠI ĐÂY: Thêm (room.PriceRoom ?? 0)
                decimal totalAmount = (room.PriceRoom ?? 0) * numberOfRooms * totalNights;

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

                TempData["SuccessMessage"] = $"Successfully booked {numberOfRooms} rooms ({room.RoomType}) for {totalNights} nights! Please check your invoice.";
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