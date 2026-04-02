using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Controllers
{
    public class FeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FeedbackController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ======================================================
        // LUỒNG 1: TRANG ĐÁNH GIÁ (CHỈ HIỆN DỊCH VỤ ĐÃ MUA & THANH TOÁN)
        // ======================================================
        public async Task<IActionResult> Reviews(string? searchString, string type = "Stay")
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            ViewBag.ActiveType = type;
            ViewBag.CurrentSearch = searchString;

            var paidOrderIds = await _context.Invoices.Where(i => i.AccountId == accountId && i.PaymentStatus == "Paid").Select(i => i.OrderId).ToListAsync();
            
            var orderDetails = await _context.OrderDetails
                .Include(od => od.RoomBooking).ThenInclude(rb => rb.Room).ThenInclude(r => r.Stay).ThenInclude(s => s.Spot)
                .Include(od => od.ResBooking).ThenInclude(rb => rb.RestaurantTable).ThenInclude(rt => rt.Restaurant).ThenInclude(res => res.Spot)
                .Where(od => paidOrderIds.Contains(od.OrderId)).ToListAsync();

            if (type == "Stay")
            {
                var stays = orderDetails.Where(od => od.RoomBooking?.Room?.Stay != null)
                                        .Select(od => od.RoomBooking!.Room!.Stay!)
                                        .GroupBy(s => s.StayId).Select(g => g.First()).AsQueryable();
                
                if (!string.IsNullOrEmpty(searchString)) 
                    stays = stays.Where(s => s.Name.Contains(searchString) || (s.Spot != null && s.Spot.SpotName.Contains(searchString)));
                
                return View("ReviewsStay", stays.ToList());
            }
            else
            {
                var restaurants = orderDetails.Where(od => od.ResBooking?.RestaurantTable?.Restaurant != null)
                                              .Select(od => od.ResBooking!.RestaurantTable!.Restaurant!)
                                              .GroupBy(r => r.RestaurantId).Select(g => g.First()).AsQueryable();
                
                if (!string.IsNullOrEmpty(searchString)) 
                    restaurants = restaurants.Where(r => r.RestaurantName.Contains(searchString) || (r.Spot != null && r.Spot.SpotName.Contains(searchString)));
                
                return View("ReviewsRes", restaurants.ToList());
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitReview(int serviceId, string type, int rating, string comment)
        {
            int? accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return RedirectToAction("Login", "Account");

            var feedback = new Feedback { AccountId = accountId.Value, Message = $"R|{rating}|{comment}" };
            if (type == "Stay") feedback.StayId = serviceId;
            else feedback.RestaurantId = serviceId;

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your review has been published. Thank you!";
            return RedirectToAction(nameof(Reviews), new { type = type });
        }

        // ======================================================
        // LUỒNG 2: CHAT TRỰC TIẾP VỚI MANAGER (ZALO CHUẨN ĐỐI XỨNG Z|)
        // ======================================================
        public async Task<IActionResult> Chat(int? activeManagerId)
        {
            int? myId = HttpContext.Session.GetInt32("AccountId");
            if (myId == null) return RedirectToAction("Login", "Account");

            var managers = await _context.Accounts
                .Include(a => a.Role)
                .Where(a => a.Role != null && a.Role.RoleName == "Manager")
                .ToListAsync();

            ViewBag.Managers = managers;
            ViewBag.ActiveManagerId = activeManagerId;
            ViewBag.MyId = myId;

            if (activeManagerId.HasValue)
            {
                string myPrefix = $"Z|{activeManagerId.Value}|";
                string theirPrefix = $"Z|{myId.Value}|";

                var chatHistory = await _context.Feedbacks
                    .Where(f => f.Message != null && 
                               ((f.AccountId == myId && f.Message.StartsWith(myPrefix)) || 
                                (f.AccountId == activeManagerId && f.Message.StartsWith(theirPrefix))))
                    .OrderBy(f => f.FeedbackId)
                    .ToListAsync();
                return View(chatHistory);
            }

            return View(new List<Feedback>());
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(int activeManagerId, string messageContent)
        {
            int? myId = HttpContext.Session.GetInt32("AccountId");
            if (myId == null || activeManagerId == 0 || string.IsNullOrWhiteSpace(messageContent))
                return RedirectToAction(nameof(Chat), new { activeManagerId = activeManagerId });

            // Gắn protocol Z| chuẩn xác
            var feedback = new Feedback { AccountId = myId.Value, Message = $"Z|{activeManagerId}|{messageContent}" };

            // Mượn ID lách luật
            var dummyStay = await _context.Stays.FirstOrDefaultAsync();
            if (dummyStay != null) feedback.StayId = dummyStay.StayId;
            else {
                var dummyRes = await _context.Restaurants.FirstOrDefaultAsync();
                if (dummyRes != null) feedback.RestaurantId = dummyRes.RestaurantId;
            }

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Chat), new { activeManagerId = activeManagerId });
        }
    }
}