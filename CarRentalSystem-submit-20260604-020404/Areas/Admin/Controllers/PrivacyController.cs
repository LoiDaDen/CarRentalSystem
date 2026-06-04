using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager")]
    public class PrivacyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPrivacyRequestService _privacyRequests;

        public PrivacyController(ApplicationDbContext context, IPrivacyRequestService privacyRequests)
        {
            _context = context;
            _privacyRequests = privacyRequests;
        }

        public async Task<IActionResult> Index()
        {
            var requests = await _context.PrivacyRequests
                .AsNoTracking()
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Take(200)
                .ToListAsync();
            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string reason)
        {
            var result = await _privacyRequests.ApproveAsync(id, GetCurrentUserId(), reason);
            TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var result = await _privacyRequests.RejectAsync(id, GetCurrentUserId(), reason);
            TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, string reason)
        {
            var result = await _privacyRequests.CompleteAsync(id, GetCurrentUserId(), reason);
            TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> RetentionPreview()
        {
            return Json(await _privacyRequests.PreviewRetentionAsync(DateTime.UtcNow));
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }
    }
}
