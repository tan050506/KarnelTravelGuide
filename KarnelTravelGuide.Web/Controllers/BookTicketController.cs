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
    public class BookTicketController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookTicketController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. INDEX: TRANG TÌM KIẾM VÀ DANH SÁCH CHUYẾN XE
        public async Task<IActionResult> Index(int? fromBranchId, int? toSpotId, string? transportType, string? travelDate, string? travelTime)
        {
            ViewBag.Branches = await _context.Branches.ToListAsync();
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();

            ViewBag.CurrentFrom = fromBranchId;
            ViewBag.CurrentTo = toSpotId;
            ViewBag.CurrentType = transportType;
            ViewBag.CurrentDate = travelDate ?? DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.CurrentTime = travelTime;

            var routes = _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .AsQueryable();

            if (fromBranchId.HasValue) routes = routes.Where(t => t.FromBranchId == fromBranchId);
            if (toSpotId.HasValue) routes = routes.Where(t => t.ToSpotId == toSpotId);
            if (!string.IsNullOrEmpty(transportType)) routes = routes.Where(t => t.TransportType == transportType);
            
            // Lọc theo giờ nếu có nhập
            if (!string.IsNullOrEmpty(travelTime) && TimeSpan.TryParse(travelTime, out TimeSpan parsedTime))
            {
                routes = routes.Where(t => t.DepartureTime.HasValue && t.DepartureTime.Value.TimeOfDay == parsedTime);
            }

            return View(await routes.ToListAsync());
        }

        // 2. BOOKING: TRANG CHỌN GHẾ
        public async Task<IActionResult> Booking(int id, string? date)
        {
            var transport = await _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .FirstOrDefaultAsync(t => t.TransportationId == id);

            if (transport == null) return NotFound();

            if (string.IsNullOrEmpty(date)) date = DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.TravelDate = date;

            return View(transport);
        }

        // 3. XÁC NHẬN ĐẶT VÉ
        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(int transportationId, string? travelDate, string? selectedSeats)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null)
            {
                TempData["ErrorMessage"] = "Please log in to book tickets.";
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrEmpty(selectedSeats))
            {
                TempData["ErrorMessage"] = "No seats selected. Booking failed.";
                return RedirectToAction("Booking", new { id = transportationId, date = travelDate });
            }

            var transport = await _context.Transportations.FindAsync(transportationId);
            if (transport == null) return NotFound();

            DateOnly tDate = DateOnly.FromDateTime(DateTime.Parse(travelDate));
            string[] seats = selectedSeats.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            
            decimal unitPrice = transport.PriceTransport ?? 0;
            decimal totalAmount = unitPrice * seats.Length;

            try
            {
                var order = new Order { AccountId = accountId.Value, CreateDate = DateTime.Now, TotalAmount = totalAmount, Status = "Pending" }; 
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var seat in seats)
                {
                    var ticket = new TicketBooking { TransportationId = transportationId, TravelDate = tDate, Seat = seat, TotalAmount = unitPrice };
                    _context.TicketBookings.Add(ticket);
                    await _context.SaveChangesAsync();

                    _context.OrderDetails.Add(new OrderDetail { OrderId = order.OrderId, TicketBookingId = ticket.TicketBookingId, Price = unitPrice, Quantity = 1 });
                }

                _context.Invoices.Add(new Invoice { AccountId = accountId.Value, OrderId = order.OrderId, CreatedDate = DateTime.Now, SubTotal = totalAmount, FinalTotal = totalAmount, PaymentStatus = "Unpaid" }); 
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully booked {seats.Length} tickets! Let's find you a room.";
                return RedirectToAction("Index", "BookRoom", new { spotId = transport.ToSpotId }); 
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred, please try again.";
                return RedirectToAction("Booking", new { id = transportationId, date = travelDate });
            }
        }

        // 4. API LẤY GHẾ ĐÃ ĐẶT
        [HttpGet]
        public async Task<IActionResult> GetBookedSeats(int transportationId, string? date)
        {
            if (!DateTime.TryParse(date, out DateTime parsedDate)) 
                return Json(new { bookedSeats = new List<string>(), transportName = "", transportType = "" });

            DateOnly travelDate = DateOnly.FromDateTime(parsedDate);
            var transport = await _context.Transportations.FindAsync(transportationId);
            if (transport == null) return NotFound();

            var bookedSeats = await _context.OrderDetails
                .Where(od => od.TicketBooking != null 
                          && od.TicketBooking.TransportationId == transportationId 
                          && od.TicketBooking.TravelDate == travelDate
                          && od.Order != null 
                          && od.Order.Status != "Canceled")
                .Select(od => od.TicketBooking!.Seat)
                .ToListAsync();

            return Json(new { bookedSeats = bookedSeats, transportName = transport.TransportName, transportType = transport.TransportType });
        }
    }
}