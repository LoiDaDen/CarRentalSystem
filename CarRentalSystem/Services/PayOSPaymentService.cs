using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using System.Text.Json;

namespace CarRentalSystem.Services
{
    public class PayOSPaymentService : IPayOSPaymentService
    {
        private const string DepositPaymentMethod = "PayOS";
        private const string PendingStatus = "Pending";
        private const string PaidStatus = "Paid";
        private const string FailedStatus = "Failed";
        private const string CancelledStatus = "Cancelled";

        private readonly ApplicationDbContext _context;
        private readonly IBookingService _bookingService;
        private readonly IEmailService _emailService;
        private readonly PayOSClient? _payOS;

        public bool IsConfigured => _payOS != null;

        public PayOSPaymentService(
            ApplicationDbContext context,
            IBookingService bookingService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _bookingService = bookingService;
            _emailService = emailService;

            var clientId = configuration["PayOS:ClientId"] ?? string.Empty;
            var apiKey = configuration["PayOS:ApiKey"] ?? string.Empty;
            var checksumKey = configuration["PayOS:ChecksumKey"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(apiKey) &&
                !string.IsNullOrWhiteSpace(checksumKey))
            {
                _payOS = new PayOSClient(new PayOSOptions
                {
                    ClientId = clientId,
                    ApiKey = apiKey,
                    ChecksumKey = checksumKey
                });
            }
        }

        public async Task<PayOSCreatePaymentResult> CreateDepositPaymentAsync(Booking booking, string returnUrl, string cancelUrl)
        {
            if (_payOS == null)
            {
                return new PayOSCreatePaymentResult(false, null, "PayOS chưa được cấu hình.", null);
            }

            if (booking.Payment != null && string.Equals(booking.Payment.PaymentStatus, PaidStatus, StringComparison.OrdinalIgnoreCase))
            {
                return new PayOSCreatePaymentResult(false, null, "Đơn này đã được thanh toán cọc trước đó.", null);
            }

            if (booking.DepositAmount <= 0)
            {
                booking.DepositAmount = Math.Round(booking.TotalPrice * 0.2m, 0, MidpointRounding.AwayFromZero);
                _context.Bookings.Update(booking);
                await _context.SaveChangesAsync();
            }

            var depositAmount = (long)booking.DepositAmount;
            if (depositAmount <= 0)
            {
                return new PayOSCreatePaymentResult(false, null, "Số tiền cọc không hợp lệ.", null);
            }

            var payment = booking.Payment ?? await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == booking.BookingID);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = booking.BookingID,
                    Amount = booking.DepositAmount,
                    PaymentMethod = DepositPaymentMethod,
                    PaymentStatus = PendingStatus,
                    PaymentDate = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
            }
            else
            {
                payment.Amount = booking.DepositAmount;
                payment.PaymentMethod = DepositPaymentMethod;
                payment.PaymentStatus = PendingStatus;
                payment.PaymentDate = DateTime.UtcNow;
                _context.Payments.Update(payment);
            }

            var transaction = new PayOSTransaction
            {
                BookingID = booking.BookingID,
                Payment = payment,
                OrderCode = booking.BookingID,
                Amount = booking.DepositAmount,
                Status = PendingStatus,
                PayOSDescription = $"Coc xe #{booking.BookingID}"
            };
            _context.PayOSTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            var items = new List<PaymentLinkItem>
            {
                new()
                {
                    Name = $"Tiền cọc thuê xe {booking.Car?.CarName ?? booking.BookingID.ToString()}",
                    Quantity = 1,
                    Price = depositAmount
                }
            };

            var paymentLinkRequest = new CreatePaymentLinkRequest
            {
                OrderCode = transaction.OrderCode,
                Amount = depositAmount,
                Description = transaction.PayOSDescription,
                Items = items,
                CancelUrl = cancelUrl,
                ReturnUrl = returnUrl
            };

            try
            {
                var response = await _payOS.PaymentRequests.CreateAsync(paymentLinkRequest);
                transaction.CheckoutUrl = response.CheckoutUrl;
                transaction.Status = PendingStatus;
                _context.PayOSTransactions.Update(transaction);
                await _context.SaveChangesAsync();

                return new PayOSCreatePaymentResult(true, response.CheckoutUrl, null, transaction);
            }
            catch (Exception ex)
            {
                payment.PaymentStatus = FailedStatus;
                transaction.Status = FailedStatus;
                transaction.FailedAt = DateTime.UtcNow;
                transaction.PayOSDescription = ex.Message;
                _context.Payments.Update(payment);
                _context.PayOSTransactions.Update(transaction);
                await _context.SaveChangesAsync();

                return new PayOSCreatePaymentResult(false, null, ex.Message, transaction);
            }
        }

        public async Task<PayOSWebhookResult> HandleWebhookAsync(Webhook webhookData)
        {
            if (_payOS == null)
            {
                return new PayOSWebhookResult(false, false, "PayOS not configured", null, null);
            }

            var data = await _payOS.Webhooks.VerifyAsync(webhookData);
            if (data == null)
            {
                return new PayOSWebhookResult(false, false, "Invalid webhook signature", null, null);
            }

            var bookingId = (int)data.OrderCode;
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Car)
                .Include(b => b.Payment)
                .Include(b => b.BookingStatus)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
            {
                return new PayOSWebhookResult(false, false, "Booking not found", bookingId, null);
            }

            var payment = booking.Payment ?? await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == bookingId);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = bookingId,
                    Amount = booking.DepositAmount,
                    PaymentMethod = DepositPaymentMethod,
                    PaymentDate = DateTime.UtcNow,
                    PaymentStatus = PendingStatus
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();
            }

            var transaction = await _context.PayOSTransactions
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(t => t.OrderCode == data.OrderCode && t.BookingID == bookingId);

            if (transaction == null)
            {
                transaction = new PayOSTransaction
                {
                    BookingID = bookingId,
                    PaymentID = payment.PaymentID,
                    OrderCode = data.OrderCode,
                    Amount = data.Amount > 0 ? data.Amount : booking.DepositAmount,
                    Status = PendingStatus,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PayOSTransactions.Add(transaction);
            }

            transaction.PaymentID = payment.PaymentID;
            transaction.PayOSCode = data.Code;
            transaction.PayOSDescription = string.IsNullOrWhiteSpace(data.Code) ? transaction.PayOSDescription : $"PayOS code: {data.Code}";
            transaction.WebhookPayload = JsonSerializer.Serialize(webhookData);

            var isPaid = string.Equals(data.Code, "00", StringComparison.OrdinalIgnoreCase);

            if (isPaid)
            {
                var paidAmount = data.Amount > 0 ? data.Amount : payment.Amount;
                payment.Amount = paidAmount;
                payment.PaymentStatus = PaidStatus;
                payment.PaymentDate = DateTime.UtcNow;

                booking.DepositAmount = paidAmount;
                booking.DepositStatus = "Holding";

                transaction.Amount = paidAmount;
                transaction.Status = PaidStatus;
                transaction.PaidAt = DateTime.UtcNow;

                _context.Payments.Update(payment);
                _context.Bookings.Update(booking);
                _context.PayOSTransactions.Update(transaction);
                await _context.SaveChangesAsync();

                await _bookingService.UpdateBookingStatusAsync(bookingId, "Confirmed");

                _ = _emailService.SendBookingConfirmationEmailAsync(booking, paidAmount);
                return new PayOSWebhookResult(true, true, "Payment confirmed", bookingId, paidAmount);
            }

            payment.PaymentStatus = FailedStatus;
            transaction.Status = FailedStatus;
            transaction.FailedAt = DateTime.UtcNow;
            _context.Payments.Update(payment);
            _context.PayOSTransactions.Update(transaction);
            await _context.SaveChangesAsync();

            return new PayOSWebhookResult(true, false, "Payment failed", bookingId, payment.Amount);
        }

        public async Task MarkCancelledAsync(int bookingId)
        {
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == bookingId);
            if (payment != null && string.Equals(payment.PaymentStatus, PendingStatus, StringComparison.OrdinalIgnoreCase))
            {
                payment.PaymentStatus = FailedStatus;
                _context.Payments.Update(payment);
            }

            var transaction = await _context.PayOSTransactions
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(t => t.BookingID == bookingId && t.Status == PendingStatus);

            if (transaction != null)
            {
                transaction.Status = CancelledStatus;
                transaction.CancelledAt = DateTime.UtcNow;
                _context.PayOSTransactions.Update(transaction);
            }

            await _context.SaveChangesAsync();
        }
    }
}
