using CarRentalSystem.Authorization;
using System.Security.Claims;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class SettlementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettlementReviewService _settlementReview;

        public SettlementController(ApplicationDbContext context, ISettlementReviewService settlementReview)
        {
            _context = context;
            _settlementReview = settlementReview;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = null)
        {
            var query = _context.SettlementReviews
                .Include(r => r.Booking).ThenInclude(b => b!.User)
                .Include(r => r.Booking).ThenInclude(b => b!.Car)
                .Include(r => r.HandoverRecord)
                .OrderByDescending(r => r.CreatedAt)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            return View(await query.Take(200).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int bookingId, string reviewType, decimal amount, string reason, int? handoverId = null)
        {
            var userId = CurrentUserId();
            await _settlementReview.CreateAsync(bookingId, reviewType, amount, reason, userId, handoverId);
            TempData["Success"] = "Đã tạo settlement review.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string reason)
        {
            var result = await _settlementReview.ApproveAsync(id, CurrentUserId(), reason);
            TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var result = await _settlementReview.RejectAsync(id, CurrentUserId(), reason);
            TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private int CurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        }
    }
}
