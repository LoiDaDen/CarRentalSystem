using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayOS.Models.Webhooks;
using System.Security.Claims;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly IPayOSPaymentService _payOSPaymentService;
        private readonly CarRentalSystem.Data.ApplicationDbContext _context;

        public PaymentController(
            IBookingService bookingService,
            IPayOSPaymentService payOSPaymentService,
            CarRentalSystem.Data.ApplicationDbContext context)
        {
            _bookingService = bookingService;
            _payOSPaymentService = payOSPaymentService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> CreatePayment(int bookingId)
        {
            if (!_payOSPaymentService.IsConfigured)
            {
                TempData["Error"] = "PayOS chưa được cấu hình. Hãy thêm ClientId/ApiKey/ChecksumKey vào biến môi trường.";
                return RedirectToAction("Deposit", "Booking", new { id = bookingId });
            }

            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null)
                return NotFound();

            if (!IsCurrentUserBookingOwner(booking.UserID))
                return Forbid();

            var domain = $"{Request.Scheme}://{Request.Host}";
            var result = await _payOSPaymentService.CreateDepositPaymentAsync(
                booking,
                $"{domain}/Payment/Success?bookingId={bookingId}",
                $"{domain}/Payment/Cancel?bookingId={bookingId}");

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.CheckoutUrl))
                return Redirect(result.CheckoutUrl);

            TempData["Error"] = result.ErrorMessage ?? "Không thể tạo thanh toán PayOS.";
            return RedirectToAction("Deposit", "Booking", new { id = bookingId });
        }

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook([FromBody] Webhook webhookData)
        {
            if (webhookData == null)
                return BadRequest(new { message = "Missing webhook payload" });

            var result = await _payOSPaymentService.HandleWebhookAsync(webhookData);
            if (!result.IsSuccess)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = "success", bookingId = result.BookingId, paid = result.IsPaid });
        }

        [HttpGet]
        public async Task<IActionResult> Success(int bookingId)
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null)
                return NotFound();

            if (!IsCurrentUserBookingOwner(booking.UserID))
                return Forbid();

            return View(booking);
        }

        [HttpGet]
        public async Task<IActionResult> SuccessTransaction(int transactionId)
        {
            var transaction = await _context.BookingTransactions
                .Include(t => t.Bookings).ThenInclude(b => b.BookingStatus)
                .FirstOrDefaultAsync(t => t.TransactionID == transactionId);

            if (transaction == null)
                return NotFound();

            return View(transaction);
        }

        [HttpGet]
        public async Task<IActionResult> Cancel(int bookingId)
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null)
                return NotFound();

            if (!IsCurrentUserBookingOwner(booking.UserID))
                return Forbid();

            await _payOSPaymentService.MarkCancelledAsync(bookingId);
            return View(booking);
        }

        private bool IsCurrentUserBookingOwner(int bookingUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(currentUserId, out var userId) && bookingUserId == userId;
        }
    }
}
