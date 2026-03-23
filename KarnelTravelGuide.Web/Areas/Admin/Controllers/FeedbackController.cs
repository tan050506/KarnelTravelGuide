using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace KarnelTravelGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class FeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FeedbackController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/Feedback/Index
        public async Task<IActionResult> Index()
        {
            var feedbacks = await _context.Feedbacks
                .Include(f => f.Account)
                .Include(f => f.Stay)
                .Include(f => f.Restaurant)
                .OrderByDescending(f => f.FeedbackId)
                .ToListAsync();

            return View(feedbacks);
        }

        // GET: /Admin/Feedback/Reply/5
        public async Task<IActionResult> Reply(int? id)
        {
            if (id == null) return NotFound();

            var feedback = await _context.Feedbacks
                .Include(f => f.Account)
                .Include(f => f.Stay)
                .Include(f => f.Restaurant)
                .FirstOrDefaultAsync(m => m.FeedbackId == id);

            if (feedback == null) return NotFound();

            return View(feedback);
        }

        // POST: /Admin/Feedback/Reply/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, [Bind("FeedbackId,ReplyMessage")] Feedback feedbackUpdates)
        {
            if (id != feedbackUpdates.FeedbackId) return NotFound();

            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null) return NotFound();

            feedback.ReplyMessage = feedbackUpdates.ReplyMessage;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(feedback);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FeedbackExists(feedback.FeedbackId)) return NotFound();
                    else throw;
                }
                TempData["SuccessMessage"] = "Phản hồi đã được gửi thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(feedback);
        }

        // POST: /Admin/Feedback/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback != null)
            {
                _context.Feedbacks.Remove(feedback);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa đánh giá thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool FeedbackExists(int id)
        {
            return _context.Feedbacks.Any(e => e.FeedbackId == id);
        }
    }
}
