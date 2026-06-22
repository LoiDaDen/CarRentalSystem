using CarRentalSystem.Authorization;
using CarRentalSystem.Data;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IRefundService _refundService;
        private readonly IPaymentTransitionService _paymentTransition;

        public PaymentController(
            ApplicationDbContext context,
            IEmailService emailService,
            IRefundService refundService,
            IPaymentTransitionService paymentTransition)
        {
            _context = context;
            _emailService = emailService;
            _refundService = refundService;
            _paymentTransition = paymentTransition;
        }

        public async Task<IActionResult> Index()
        {
            var payments = await _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.User)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Car)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var userIdText = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _ = int.TryParse(userIdText, out var actorUserId);
            var result = await _paymentTransition.ConfirmManualPaymentAsync(
                id,
                actorUserId == 0 ? null : actorUserId,
                User.Identity?.Name ?? "Admin",
                "Admin manual payment confirmation.");

            if (!result.IsSuccess && result.Message == "Payment not found.")
                return NotFound();

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            if (result.Booking != null)
                _ = _emailService.SendBookingConfirmationEmailAsync(result.Booking, result.Payment?.Amount ?? 0m);

            TempData["Success"] = $"Đã xác nhận thanh toán cho đơn #{result.Payment?.BookingID}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRefund(int bookingId, decimal amount, string reason)
        {
            var request = await _refundService.RequestRefundAsync(bookingId, amount, reason);
            TempData["Success"] = $"Đã tạo yêu cầu hoàn tiền #{request.RefundRequestID}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRefund(
            int refundRequestId,
            string recipientBin,
            string recipientAccountNumber,
            string? recipientAccountName)
        {
            var userIdText = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _ = int.TryParse(userIdText, out var approvedByUserId);
            var result = await _refundService.ApproveAndStartPayoutAsync(
                refundRequestId,
                approvedByUserId,
                recipientBin,
                recipientAccountNumber,
                recipientAccountName);

            TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
