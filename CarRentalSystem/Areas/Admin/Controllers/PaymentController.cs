using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IRefundService _refundService;

        public PaymentController(ApplicationDbContext context, IEmailService emailService, IRefundService refundService)
        {
            _context = context;
            _emailService = emailService;
            _refundService = refundService;
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
            var payment = await _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.User)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Car)
                .FirstOrDefaultAsync(p => p.PaymentID == id);

            if (payment == null)
                return NotFound();

            var isPayOSPayment = string.Equals(payment.PaymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase)
                || await _context.PayOSTransactions.AnyAsync(t => t.PaymentID == payment.PaymentID || t.BookingID == payment.BookingID);

            if (isPayOSPayment)
            {
                TempData["Error"] = "PayOS payment chi duoc xac nhan boi webhook sau khi tien da vao.";
                return RedirectToAction(nameof(Index));
            }

            var booking = payment.Booking;
            if (booking != null && booking.DepositAmount > 0)
            {
                booking.DepositStatus = "Holding";
            }

            payment.PaymentStatus = "Paid";
            payment.PaymentDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Gui email xac nhan cho khach hang
            if (booking != null)
            {
                _ = _emailService.SendBookingConfirmationEmailAsync(booking, payment.Amount);
            }

            TempData["Success"] = $"Đã xác nhận thanh toán cho đơn #{payment.BookingID}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRefund(int bookingId, decimal amount, string reason)
        {
            var request = await _refundService.RequestRefundAsync(bookingId, amount, reason);
            TempData["Success"] = $"Da tao yeu cau hoan tien #{request.RefundRequestID}.";
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
