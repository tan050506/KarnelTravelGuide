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

        // BƯỚC 1 & 2: TRANG TÌM KIẾM VÀ DANH SÁCH CHUYẾN XE
        public async Task<IActionResult> Index(int? fromBranchId, int? toSpotId, string transportType, string travelDate)
        {
            ViewBag.Branches = await _context.Branches.ToListAsync();
            ViewBag.Spots = await _context.TouristSpots.ToListAsync();

            ViewBag.CurrentFrom = fromBranchId;
            ViewBag.CurrentTo = toSpotId;
            ViewBag.CurrentType = transportType;
            ViewBag.CurrentDate = travelDate ?? DateTime.Now.ToString("yyyy-MM-dd");

            var routes = _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .AsQueryable();

            if (fromBranchId.HasValue) routes = routes.Where(r => r.FromBranchId == fromBranchId);
            if (toSpotId.HasValue) routes = routes.Where(r => r.ToSpotId == toSpotId);
            if (!string.IsNullOrEmpty(transportType)) routes = routes.Where(r => r.TransportType == transportType);

            return View(await routes.OrderBy(t => t.DepartureTime).ToListAsync());
        }

        // BƯỚC 3: TRANG CHỌN GHẾ
        [HttpGet]
        public async Task<IActionResult> Booking(int? id, string date)
        {
            var accountId = HttpContext.Session.GetInt32("AccountId"); 
            if (accountId == null) 
            {
                TempData["ErrorMessage"] = "Please login to book tickets.";
                return RedirectToAction("Login", "Account");
            }

            if (id == null || string.IsNullOrEmpty(date)) return RedirectToAction("Index");

            var route = await _context.Transportations
                .Include(t => t.FromBranch)
                .Include(t => t.ToSpot)
                .FirstOrDefaultAsync(m => m.TransportationId == id);

            if (route == null) return NotFound();

            ViewBag.SelectedDate = date; 
            return View(route);
        }

        // XỬ LÝ THANH TOÁN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Booking(int transportationId, string travelDate, string seat)
        {
            var accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            var transport = await _context.Transportations.FindAsync(transportationId);
            if (transport == null) return NotFound();

            if (string.IsNullOrEmpty(seat) || string.IsNullOrEmpty(travelDate))
            {
                TempData["ErrorMessage"] = "Please select at least 1 seat.";
                return RedirectToAction("Booking", new { id = transportationId, date = travelDate });
            }

            string[] selectedSeats = seat.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int seatCount = selectedSeats.Length;
            DateOnly parsedDate = DateOnly.FromDateTime(DateTime.Parse(travelDate));

            try 
            {
                decimal unitPrice = transport.PriceTransport ?? 0;
                decimal totalAmount = unitPrice * seatCount;

                var order = new Order
                {
                    AccountId = accountId.Value,
                    CreateDate = DateTime.Now,
                    TotalAmount = totalAmount,
                    Status = "Pending" 
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); 

                foreach (var seatId in selectedSeats)
                {
                    var ticket = new TicketBooking
                    {
                        TransportationId = transportationId,
                        TravelDate = parsedDate,
                        Seat = seatId.Trim(),
                        TotalAmount = unitPrice
                    };
                    _context.TicketBookings.Add(ticket);
                    await _context.SaveChangesAsync(); 

                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        TicketBookingId = ticket.TicketBookingId,
                        Price = unitPrice,
                        Quantity = 1
                    };
                    _context.OrderDetails.Add(orderDetail);
                }

                var invoice = new Invoice
                {
                    AccountId = accountId.Value,
                    OrderId = order.OrderId,
                    CreatedDate = DateTime.Now,
                    SubTotal = totalAmount,
                    DiscountAmount = 0,
                    FinalTotal = totalAmount,
                    PaymentStatus = "Unpaid"
                };
                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully booked {seatCount} tickets! Please check your invoice.";
                return RedirectToAction("Index"); 
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred, please try again.";
                return RedirectToAction("Booking", new { id = transportationId, date = travelDate });
            }
        }

        // API LẤY GHẾ
        [HttpGet]
        public async Task<IActionResult> GetBookedSeats(int transportationId, string date)
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