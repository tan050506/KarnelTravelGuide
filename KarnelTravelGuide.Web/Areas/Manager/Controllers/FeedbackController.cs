using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace KarnelTravelGuide.Web.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class FeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FeedbackController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. INDEX: Hiển thị và lọc danh sách đánh giá
        public async Task<IActionResult> Index(string? searchString, string? serviceType)
        {
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentType"] = serviceType;

            var query = _context.Feedbacks
                .Include(f => f.Account)
                .Include(f => f.Stay)
                .Include(f => f.Restaurant)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(f =>
                    (f.Account != null && f.Account.FullName != null && f.Account.FullName.Contains(searchString)) ||
                    (f.Message != null && f.Message.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(serviceType))
            {
                if (serviceType == "Stay") query = query.Where(f => f.StayId != null);
                else if (serviceType == "Restaurant") query = query.Where(f => f.RestaurantId != null);
            }

            // Vì Database không có CreatedDate, ta sắp xếp theo ID để tin mới nhất lên đầu
            query = query.OrderByDescending(f => f.FeedbackId);

            return View(await query.ToListAsync());
        }

        // 2. DETAILS: Xem chi tiết nội dung đánh giá
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

        // 3. DELETE: Xóa các đánh giá vi phạm tiêu chuẩn cộng đồng
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback != null)
            {
                _context.Feedbacks.Remove(feedback);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Feedback deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}