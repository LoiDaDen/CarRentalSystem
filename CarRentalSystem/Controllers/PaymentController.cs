using CarRentalSystem.Models;
using CarRentalSystem.Services;
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
        private const string PendingPaymentMessage =
            "Chưa hoàn thành yêu cầu thanh toán. Vui lòng quét QR hoặc mở lại PayOS để thanh toán.";

        private readonly IBookingService _bookingService;
        private readonly IPayOSPaymentService _payOSPaymentService;
        private readonly IDigitalContractService _digitalContractService;
        private readonly CarRentalSystem.Data.ApplicationDbContext _context;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IBookingService bookingService,
            IPayOSPaymentService payOSPaymentService,
            IDigitalContractService digitalContractService,
            CarRentalSystem.Data.ApplicationDbContext context,
            ILogger<PaymentController> logger)
        {
            _bookingService = bookingService;
            _payOSPaymentService = payOSPaymentService;
            _digitalContractService = digitalContractService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Hold(long orderCode)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingHold)
                    .ThenInclude(h => h!.Car)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));

            if (transaction == null)
                return NotFound();

            var reconcile = ReadOnlyPaymentStatus(transaction);

            if (HasCompletedHoldPayment(reconcile, transaction))
            {
                TempData["Success"] = "PayOS đã xác nhận thanh toán. Đơn thuê xe đã được tạo.";
                return RedirectToAction("MyBookings", "Booking");
            }

            if (reconcile.IsPaid)
                TempData["Warning"] = $"PayOS đã ghi nhận tiền nhưng đơn chưa tạo tự động: {reconcile.Message}. Vui lòng liên hệ quản trị để kiểm tra.";
            else if (!reconcile.IsSuccess)
                TempData["Error"] = BuildReconcileErrorMessage(reconcile);
            else
                TempData["Info"] = PendingPaymentMessage;

            return View("Pending", BuildHoldPaymentViewModel(transaction));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmHold(long orderCode)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingHold)
                    .ThenInclude(h => h!.Car)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));

            if (transaction == null)
                return NotFound();

            var reconcile = await _payOSPaymentService.ReconcilePaymentAsync(orderCode, "confirm-hold");
            transaction = await _context.PayOSTransactions
                .Include(t => t.BookingHold)
                    .ThenInclude(h => h!.Car)
                .FirstAsync(t => t.PayOSTransactionID == transaction.PayOSTransactionID);

            if (HasCompletedHoldPayment(reconcile, transaction))
            {
                TempData["Success"] = "PayOS đã xác nhận thanh toán. Đơn thuê xe đã được tạo.";
                return RedirectToAction("MyBookings", "Booking");
            }

            TempData["Error"] = reconcile.IsSuccess
                ? PendingPaymentMessage
                : BuildReconcileErrorMessage(reconcile);

            return View("Pending", BuildHoldPaymentViewModel(transaction));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelHold(long orderCode)
        {
            return await CancelHoldCore(orderCode);
        }

        [HttpGet]
        [ActionName(nameof(CancelHold))]
        public async Task<IActionResult> CancelHoldReturn(long orderCode)
        {
            return await RenderCancelReturnAsync(orderCode);
        }

        [HttpGet]
        public async Task<IActionResult> Transaction(long orderCode)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingTransaction)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));

            if (transaction?.BookingTransaction == null)
                return NotFound();

            var reconcile = ReadOnlyPaymentStatus(transaction);

            if (HasCompletedCartPayment(reconcile, transaction))
            {
                TempData["Success"] = "PayOS đã xác nhận thanh toán. Hệ thống đã tạo đơn từ giỏ hàng.";
                return RedirectToAction("MyBookings", "Booking");
            }

            if (reconcile.IsPaid)
                TempData["Warning"] = $"PayOS đã ghi nhận tiền nhưng giỏ hàng chưa tạo đơn tự động: {reconcile.Message}. Vui lòng liên hệ quản trị để kiểm tra.";
            else if (!reconcile.IsSuccess)
                TempData["Error"] = BuildReconcileErrorMessage(reconcile);
            else
                TempData["Info"] = PendingPaymentMessage;

            return View("Pending", BuildCartPaymentViewModel(transaction));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTransaction(long orderCode)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingTransaction)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));

            if (transaction?.BookingTransaction == null)
                return NotFound();

            var reconcile = await _payOSPaymentService.ReconcilePaymentAsync(orderCode, "confirm-cart");
            transaction = await _context.PayOSTransactions
                .Include(t => t.BookingTransaction)
                .FirstAsync(t => t.PayOSTransactionID == transaction.PayOSTransactionID);

            if (HasCompletedCartPayment(reconcile, transaction))
            {
                TempData["Success"] = "PayOS đã xác nhận thanh toán. Hệ thống đã tạo đơn từ giỏ hàng.";
                return RedirectToAction("MyBookings", "Booking");
            }

            TempData["Error"] = reconcile.IsSuccess
                ? PendingPaymentMessage
                : BuildReconcileErrorMessage(reconcile);

            return View("Pending", BuildCartPaymentViewModel(transaction));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTransaction(long orderCode)
        {
            return await CancelTransactionCore(orderCode, renderCancelPage: false);
        }

        [HttpGet]
        [ActionName(nameof(CancelTransaction))]
        public async Task<IActionResult> CancelTransactionReturn(long orderCode)
        {
            return await RenderCancelReturnAsync(orderCode);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

        [HttpGet]
        public async Task<IActionResult> Success(int bookingId)
        {
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
                var reconcile = ReadOnlyPaymentStatus(transaction);
                TempData["Success"] = reconcile.IsPaid
                    ? "PayOS đã xác nhận thanh toán giữ chỗ."
                    : "Thanh toán đã được gửi về PayOS. Hệ thống sẽ cập nhật đơn khi webhook xác nhận tiền đã vào.";
            }
            else
            {
                TempData["Success"] = "Thanh toán đã được gửi về PayOS. Hệ thống sẽ cập nhật đơn khi webhook xác nhận tiền đã vào.";
            }

            ViewBag.PaidAmount = transaction?.Amount ?? booking.Payment?.Amount ?? booking.ReservationFee;
            ViewBag.PayOSStatus = transaction?.Status ?? booking.Payment?.PaymentStatus;

            return View(booking);
        }

        [HttpGet]
        public async Task<IActionResult> SuccessTransaction(int transactionId)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var transaction = await _context.BookingTransactions
                .Include(t => t.DigitalContract)
                .Include(t => t.Bookings).ThenInclude(b => b.BookingStatus)
                .Include(t => t.Bookings).ThenInclude(b => b.Payment)
                .Include(t => t.Bookings).ThenInclude(b => b.Car).ThenInclude(c => c!.CarBrand)
                .FirstOrDefaultAsync(t => t.TransactionID == transactionId &&
                    (t.UserID == userId || User.IsInRole("Admin")));

            if (transaction == null)
                return NotFound();

            var payOSTransaction = await _context.PayOSTransactions
                .Where(t => t.TransactionID == transactionId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
            ViewBag.PaidAmount = payOSTransaction?.Amount ?? transaction.Bookings.Sum(b => b.Payment?.Amount ?? b.ReservationFee);
            ViewBag.PayOSStatus = payOSTransaction?.Status ?? transaction.Status;

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

            return View(booking);
        }

        private async Task<IActionResult> CancelHoldCore(long orderCode)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingHold)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));
            if (transaction == null)
                return NotFound();

            await _payOSPaymentService.MarkHoldCancelledAsync(orderCode);
            TempData["Info"] = "Bạn đã hủy thanh toán giữ chỗ. Hệ thống đã nhả xe tạm giữ nếu PayOS chưa xác nhận tiền vào.";
            return View("CancelHold", transaction);
        }

        private async Task<IActionResult> CancelTransactionCore(long orderCode, bool renderCancelPage)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingTransaction)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));
            if (transaction == null)
                return NotFound();

            await _payOSPaymentService.MarkHoldCancelledAsync(orderCode);
            TempData["Error"] = "Bạn đã hủy thanh toán giỏ hàng. Các phiếu giữ xe đã được giải phóng.";

            return renderCancelPage
                ? View("CancelHold", transaction)
                : RedirectToAction("Index", "Cart");
        }

        private async Task<IActionResult> RenderCancelReturnAsync(long orderCode)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var transaction = await _context.PayOSTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode &&
                    (t.UserID == userId || User.IsInRole("Admin")));
            if (transaction == null)
                return NotFound();

            return View("CancelHold", transaction);
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(currentUserId, out userId);
        }

        private bool IsCurrentUserBookingOwner(int bookingUserId)
        {
            return TryGetCurrentUserId(out var userId) && bookingUserId == userId;
        }

        private string CurrentReturnUrl()
        {
            return $"{Request.Path}{Request.QueryString}";
        }

        private static bool HasCompletedHoldPayment(PayOSWebhookResult reconcile, PayOSTransaction transaction)
        {
            return (reconcile.IsPaid && reconcile.BookingId.HasValue)
                || (transaction.Status == PaymentStatusConstants.Paid && transaction.BookingID.HasValue);
        }

        private static bool HasCompletedCartPayment(PayOSWebhookResult reconcile, PayOSTransaction transaction)
        {
            return (reconcile.IsPaid && reconcile.BookingId.HasValue)
                || transaction.Status == PaymentStatusConstants.Paid
                || transaction.BookingTransaction?.Status == PaymentStatusConstants.Paid
                || transaction.BookingTransaction?.Status == BookingStatusConstants.Confirmed;
        }

        private static PayOSWebhookResult ReadOnlyPaymentStatus(PayOSTransaction transaction)
        {
            var paid = string.Equals(transaction.Status, PaymentStatusConstants.Paid, StringComparison.OrdinalIgnoreCase);
            return new PayOSWebhookResult(true, paid, "Read-only return page", transaction.BookingID, transaction.Amount);
        }

        private static string BuildReconcileErrorMessage(PayOSWebhookResult reconcile)
        {
            if (string.IsNullOrWhiteSpace(reconcile.Message))
                return "Không thể kiểm tra trạng thái PayOS lúc này. Vui lòng thử lại sau hoặc mở lại PayOS để hoàn tất thanh toán.";

            return $"Không thể kiểm tra trạng thái PayOS lúc này ({reconcile.Message}). Vui lòng thử lại sau hoặc mở lại PayOS để hoàn tất thanh toán.";
        }

        private static PayOSHoldPaymentViewModel BuildHoldPaymentViewModel(PayOSTransaction transaction)
        {
            var quote = DeserializeQuote(transaction.BookingHold?.QuoteSnapshotJson) ?? new CheckoutQuoteViewModel
            {
                HoldingFee = transaction.Amount,
                TotalPrice = transaction.Amount,
                CarName = transaction.BookingHold?.Car?.CarName ?? "",
                StartDate = transaction.BookingHold?.StartDate ?? DateTime.UtcNow,
                EndDate = transaction.BookingHold?.EndDate ?? DateTime.UtcNow
            };

            return new PayOSHoldPaymentViewModel
            {
                OrderCode = transaction.OrderCode,
                PaymentFlow = PayOSPaymentFlow.Hold,
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
        }

        private static PayOSHoldPaymentViewModel BuildCartPaymentViewModel(PayOSTransaction transaction)
        {
            return new PayOSHoldPaymentViewModel
            {
                OrderCode = transaction.OrderCode,
                PaymentFlow = PayOSPaymentFlow.Cart,
                Status = transaction.Status,
                CheckoutUrl = transaction.CheckoutUrl,
                QrCode = transaction.QrCode,
                Bin = transaction.Bin,
                AccountNumber = transaction.AccountNumber,
                AccountName = transaction.AccountName,
                Description = transaction.PayOSDescription,
                ExpiresAt = transaction.CreatedAt.AddMinutes(15),
                Quote = new CheckoutQuoteViewModel
                {
                    CarName = "Giỏ hàng",
                    HoldingFee = transaction.Amount,
                    TotalPrice = transaction.Amount
                }
            };
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
