using CarRentalSystem.Services;
using CarRentalSystem.Models;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayOS.Models.Webhooks;
using System.Security.Claims;
using System.Text.Json;

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

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Hold(long orderCode)
        {
            var reconcile = await _payOSPaymentService.ReconcilePaymentAsync(orderCode, "return-hold");
            if (reconcile.IsPaid && reconcile.BookingId.HasValue)
            {
                TempData["Success"] = "PayOS đã xác nhận thanh toán. Đơn thuê xe đã được tạo.";
            }

            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Auth", new { returnUrl = CurrentReturnUrl() });

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingHold)
                    .ThenInclude(h => h!.Car)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));

            if (transaction == null)
                return NotFound();

            if (reconcile.IsPaid && reconcile.BookingId.HasValue)
                return RedirectToAction("Deposit", "Booking", new { id = reconcile.BookingId.Value });

            if (transaction.Status == PaymentStatusConstants.Paid && transaction.BookingID.HasValue)
                return RedirectToAction("Deposit", "Booking", new { id = transaction.BookingID.Value });

            var quote = DeserializeQuote(transaction.BookingHold?.QuoteSnapshotJson) ?? new CheckoutQuoteViewModel
            {
                HoldingFee = transaction.Amount,
                TotalPrice = transaction.Amount,
                CarName = transaction.BookingHold?.Car?.CarName ?? "",
                StartDate = transaction.BookingHold?.StartDate ?? DateTime.UtcNow,
                EndDate = transaction.BookingHold?.EndDate ?? DateTime.UtcNow
            };

            var vm = new PayOSHoldPaymentViewModel
            {
                OrderCode = transaction.OrderCode,
                Status = transaction.Status,
                CheckoutUrl = transaction.CheckoutUrl,
                QrCode = transaction.QrCode,
                Bin = transaction.Bin,
                AccountNumber = transaction.AccountNumber,
                AccountName = transaction.AccountName,
                Description = transaction.PayOSDescription,
                ExpiresAt = transaction.BookingHold?.ExpiresAt ?? transaction.CreatedAt.AddMinutes(15),
                Quote = quote
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> CancelHold(long orderCode)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));
            if (transaction == null)
                return NotFound();

            await _payOSPaymentService.MarkHoldCancelledAsync(orderCode);
            return View("CancelHold", transaction);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Transaction(long orderCode)
        {
            var reconcile = await _payOSPaymentService.ReconcilePaymentAsync(orderCode, "return-cart");
            if (reconcile.IsPaid)
            {
                TempData["Success"] = "PayOS đã xác nhận thanh toán. Hệ thống đã tạo đơn từ giỏ hàng.";
            }

            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Auth", new { returnUrl = CurrentReturnUrl() });

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingTransaction)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));

            if (transaction?.BookingTransaction == null)
                return NotFound();

            if (!reconcile.IsPaid)
                TempData["Success"] = "Thanh toán đã được gửi về PayOS. Hệ thống sẽ cập nhật đơn khi webhook xác nhận tiền đã vào.";
            return RedirectToAction("DepositTransaction", "Booking", new { id = transaction.BookingTransaction.TransactionID });
        }

        [HttpGet]
        public async Task<IActionResult> CancelTransaction(long orderCode)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingTransaction)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));
            if (transaction == null)
                return NotFound();

            await _payOSPaymentService.MarkHoldCancelledAsync(orderCode);
            TempData["Error"] = "Bạn đã hủy thanh toán giỏ hàng. Các phiếu giữ xe đã được giải phóng.";
            return transaction.BookingTransaction == null
                ? RedirectToAction("Index", "Cart")
                : RedirectToAction("DepositTransaction", "Booking", new { id = transaction.BookingTransaction.TransactionID });
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

            var domain = PaymentUrlResolver.ResolvePublicBaseUrl(HttpContext);
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

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Success(int bookingId)
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Auth", new { returnUrl = CurrentReturnUrl() });

            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null)
                return NotFound();

            if (!IsCurrentUserBookingOwner(booking.UserID))
                return Forbid();

            var transaction = await _context.PayOSTransactions
                .Where(t => t.BookingID == bookingId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
            if (transaction != null)
            {
                var reconcile = await _payOSPaymentService.ReconcilePaymentAsync(transaction.OrderCode, "return-booking");
                TempData["Success"] = reconcile.IsPaid
                    ? "PayOS đã xác nhận thanh toán giữ chỗ."
                    : "Thanh toán đã được gửi về PayOS. Hệ thống sẽ cập nhật đơn khi webhook xác nhận tiền đã vào.";
            }
            else
            {
                TempData["Success"] = "Thanh toán đã được gửi về PayOS. Hệ thống sẽ cập nhật đơn khi webhook xác nhận tiền đã vào.";
            }

            return RedirectToAction("Deposit", "Booking", new { id = bookingId });
        }

        [HttpGet]
        public async Task<IActionResult> SuccessTransaction(int transactionId)
        {
            // =========================================
            // SECURITY: Verify ownership
            // =========================================
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var transaction = await _context.BookingTransactions
                .Include(t => t.Bookings).ThenInclude(b => b.BookingStatus)
                .FirstOrDefaultAsync(t => t.TransactionID == transactionId &&
                    (t.UserID == userId || User.IsInRole("Admin")));

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

        private string CurrentReturnUrl()
        {
            return $"{Request.Path}{Request.QueryString}";
        }

        private static CheckoutQuoteViewModel? DeserializeQuote(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<CheckoutQuoteViewModel>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }
}
