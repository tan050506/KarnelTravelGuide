using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using KarnelTravelGuide.Web.Attributes;
using System;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    [RoleAuthorize("Manager")]
    public class FeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FeedbackController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ======================================================
        // LUỒNG 1: QUẢN LÝ ĐÁNH GIÁ (CÓ PHÂN TRANG VÀ SẮP XẾP)
        // ======================================================
        public async Task<IActionResult> Index(string? searchString, string? serviceType, string? sortOrder, int page = 1)
        {
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentType"] = serviceType;
            ViewData["CurrentSort"] = sortOrder;
            
            // Mặc định là mới nhất lên đầu (desc). Bấm vào sẽ đảo thành cũ nhất (asc)
            ViewData["IdSortParm"] = string.IsNullOrEmpty(sortOrder) ? "id_asc" : "";

            var query = _context.Feedbacks
                .Include(f => f.Account)
                .Include(f => f.Stay)
                .Include(f => f.Restaurant)
                .Where(f => f.Message != null && f.Message.StartsWith("R|"))
                .AsQueryable();

            // Lọc theo tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(f =>
                    (f.Account != null && f.Account.FullName != null && f.Account.FullName.Contains(searchString)) ||
                    (f.Message != null && f.Message.Contains(searchString)));
            }

            // Lọc theo loại dịch vụ
            if (!string.IsNullOrEmpty(serviceType))
            {
                if (serviceType == "Stay") query = query.Where(f => f.StayId != null);
                else if (serviceType == "Restaurant") query = query.Where(f => f.RestaurantId != null);
            }

            // Sắp xếp
            switch (sortOrder)
            {
                case "id_asc": query = query.OrderBy(f => f.FeedbackId); break;
                default: query = query.OrderByDescending(f => f.FeedbackId); break; // Mặc định desc
            }

            var allFeedbacks = await query.ToListAsync();

            // Phân trang
            int pageSize = 10;
            int totalItems = allFeedbacks.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedFeedbacks = allFeedbacks.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(pagedFeedbacks);
        }

        public async Task<IActionResult> Details(int id)
        {
            var feedback = await _context.Feedbacks
                .Include(f => f.Account)
                .Include(f => f.Stay)
                .Include(f => f.Restaurant)
                .FirstOrDefaultAsync(f => f.FeedbackId == id);

            if (feedback == null) return NotFound();
            return View(feedback);
        }

        [HttpPost]
        public async Task<IActionResult> Details(int id, string replyContent)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback != null)
            {
                feedback.ReplyMessage = replyContent;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Your reply to this review has been updated!";
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback != null)
            {
                _context.Feedbacks.Remove(feedback);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Review deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ======================================================
        // LUỒNG 2: ZALO CHAT (LOẠI BỎ ADMIN VÀ BẢN THÂN MANAGER)
        // ======================================================
        public async Task<IActionResult> SupportChat(int? activeUserId)
        {
            int? myId = HttpContext.Session.GetInt32("AccountId");
            if (myId == null) return RedirectToAction("Login", "Account", new { area = "" });

            // CHỈ lấy những ai LÀ Customer HOẶC LÀ Manager KHÁC MÌNH (AccountId != myId)
            var allAccounts = await _context.Accounts
                .Include(a => a.Role)
                .Where(a => a.AccountId != myId && a.Role != null && (a.Role.RoleName == "Manager" || a.Role.RoleName == "Customer"))
                .ToListAsync();
            
            ViewBag.Staffs = allAccounts.Where(a => a.Role?.RoleName == "Manager").ToList();
            ViewBag.Customers = allAccounts.Where(a => a.Role?.RoleName == "Customer").ToList();
            
            ViewBag.ActiveUserId = activeUserId;
            ViewBag.MyId = myId;

            if (activeUserId.HasValue)
            {
                string myPrefix = $"Z|{activeUserId.Value}|"; 
                string theirPrefix = $"Z|{myId.Value}|";       

                var chatHistory = await _context.Feedbacks
                    .Where(f => f.Message != null && 
                               ((f.AccountId == myId && f.Message.StartsWith(myPrefix)) || 
                                (f.AccountId == activeUserId && f.Message.StartsWith(theirPrefix))))
                    .OrderBy(f => f.FeedbackId)
                    .ToListAsync();
                    
                return View(chatHistory);
            }

            return View(new List<Feedback>());
        }

        [HttpPost]
        public async Task<IActionResult> SendZaloMessage(int activeUserId, string messageContent)
        {
            int? myId = HttpContext.Session.GetInt32("AccountId");
            if (myId == null || activeUserId == 0 || string.IsNullOrWhiteSpace(messageContent))
                return RedirectToAction(nameof(SupportChat), new { activeUserId = activeUserId });

            var feedback = new Feedback { AccountId = myId.Value, Message = $"Z|{activeUserId}|{messageContent}" };

            // Mượn tạm ID dịch vụ để không lỗi DB Constraint
            var dummyStay = await _context.Stays.FirstOrDefaultAsync();
            if (dummyStay != null) feedback.StayId = dummyStay.StayId;
            else {
                var dummyRes = await _context.Restaurants.FirstOrDefaultAsync();
                if (dummyRes != null) feedback.RestaurantId = dummyRes.RestaurantId;
            }

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(SupportChat), new { activeUserId = activeUserId });
        }
    }
}