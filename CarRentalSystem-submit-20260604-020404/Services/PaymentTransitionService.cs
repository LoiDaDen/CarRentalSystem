using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IPaymentTransitionService
    {
        Task<PaymentTransitionResult> ConfirmManualPaymentAsync(int paymentId, int? actorUserId, string actorRole, string? reason = null);
        Task<PaymentTransitionResult> MarkPayOSExistingBookingPaidAsync(PayOSTransaction transaction, PaymentWebhookEvent webhookEvent);
        Task<PaymentTransitionResult> MarkPayOSFailedAsync(PayOSTransaction transaction, PaymentWebhookEvent webhookEvent);
        Task<PaymentTransitionResult> MarkCartPaymentUnderReviewAsync(PayOSTransaction transaction, BookingTransaction bookingTransaction, PaymentWebhookEvent webhookEvent, string reason);
        Task<PaymentTransitionResult> SetBookingDepositStatusAsync(int bookingId, string depositStatus, int? actorUserId, string actorRole, string reason);
    }

    public record PaymentTransitionResult(bool IsSuccess, string Message, Payment? Payment = null, Booking? Booking = null);

    public class PaymentTransitionService : IPaymentTransitionService
    {
        private const string PayOSPaymentMethod = "PayOS";
        private readonly ApplicationDbContext _context;
        private readonly IPaymentLedgerService _ledger;
        private readonly IAuditTrailService _audit;
        private readonly IReservationService _reservationService;

        public PaymentTransitionService(
            ApplicationDbContext context,
            IPaymentLedgerService ledger,
            IAuditTrailService audit,
            IReservationService reservationService)
        {
            _context = context;
            _ledger = ledger;
            _audit = audit;
            _reservationService = reservationService;
        }

        public async Task<PaymentTransitionResult> ConfirmManualPaymentAsync(int paymentId, int? actorUserId, string actorRole, string? reason = null)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId);
            if (payment == null)
                return new PaymentTransitionResult(false, "Payment not found.");

            var isPayOSPayment = string.Equals(payment.PaymentMethod, PayOSPaymentMethod, StringComparison.OrdinalIgnoreCase)
                || await _context.PayOSTransactions.AnyAsync(t => t.PaymentID == payment.PaymentID || t.BookingID == payment.BookingID);
            if (isPayOSPayment)
                return new PaymentTransitionResult(false, "PayOS payment must be confirmed by webhook.");

            var oldValues = JsonSerializer.Serialize(new
            {
                payment.PaymentStatus,
                payment.PaymentDate,
                DepositStatus = payment.Booking?.DepositStatus
            });

            payment.PaymentStatus = PaymentStatusConstants.Paid;
            payment.PaymentDate = DateTime.UtcNow;
            if (payment.Booking != null && payment.Booking.DepositAmount > 0)
                payment.Booking.DepositStatus = DepositStatusConstants.Holding;

            await _ledger.RecordAsync(new PaymentLedgerEntry
            {
                BookingID = payment.BookingID,
                PaymentID = payment.PaymentID,
                Provider = "Manual",
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = payment.Amount,
                Status = PaymentStatusConstants.Paid,
                PostedAt = DateTime.UtcNow,
                IdempotencyKey = $"manual-payment-confirmed:{payment.PaymentID}",
                Description = reason ?? $"Manual payment confirmed by {actorRole}."
            });

            await _context.SaveChangesAsync();
            await _audit.RecordAsync(new AuditLog
            {
                Action = "ConfirmManualPayment",
                EntityType = nameof(Payment),
                EntityId = payment.PaymentID,
                UserId = actorUserId,
                UserName = actorRole,
                Reason = reason,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new
                {
                    payment.PaymentStatus,
                    payment.PaymentDate,
                    DepositStatus = payment.Booking?.DepositStatus
                })
            });

            return new PaymentTransitionResult(true, "Manual payment confirmed.", payment, payment.Booking);
        }

        public async Task<PaymentTransitionResult> MarkPayOSExistingBookingPaidAsync(PayOSTransaction transaction, PaymentWebhookEvent webhookEvent)
        {
            if (transaction.Booking == null)
                return new PaymentTransitionResult(false, "Booking not found.");

            var bookingId = transaction.BookingID ?? transaction.Booking.BookingID;
            var payment = transaction.Payment ?? await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == bookingId);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = bookingId,
                    Amount = transaction.Amount,
                    PaymentMethod = PayOSPaymentMethod,
                    PaymentStatus = PaymentStatusConstants.Pending,
                    PaymentDate = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();
                transaction.PaymentID = payment.PaymentID;
            }

            var oldValues = JsonSerializer.Serialize(new
            {
                PaymentStatus = payment.PaymentStatus,
                TransactionStatus = transaction.Status,
                DepositStatus = transaction.Booking.DepositStatus,
                BookingStatusID = transaction.Booking.BookingStatusID
            });

            payment.Amount = transaction.Amount;
            payment.PaymentStatus = PaymentStatusConstants.Paid;
            payment.PaymentDate = DateTime.UtcNow;
            transaction.Status = PaymentStatusConstants.Paid;
            transaction.PaidAt = DateTime.UtcNow;
            transaction.Booking.ReservationFee = transaction.Amount;
            transaction.Booking.DepositStatus = DepositStatusConstants.Holding;

            var pendingStatus = await _context.BookingStatuses.FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Pending);
            if (pendingStatus != null)
                transaction.Booking.BookingStatusID = pendingStatus.BookingStatusID;

            await _ledger.RecordAsync(new PaymentLedgerEntry
            {
                BookingID = bookingId,
                PaymentID = payment.PaymentID,
                Provider = "PayOS",
                OrderCode = transaction.OrderCode,
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = transaction.Amount,
                Status = PaymentStatusConstants.Paid,
                PostedAt = DateTime.UtcNow,
                IdempotencyKey = $"payos-paid:{transaction.OrderCode}",
                Description = "PayOS reservation fee paid."
            });

            webhookEvent.Status = "Processed";
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _audit.RecordAsync(new AuditLog
            {
                Action = "PayOSExistingBookingPaid",
                EntityType = nameof(PayOSTransaction),
                EntityId = transaction.PayOSTransactionID,
                Reason = "PayOS webhook confirmed existing booking payment.",
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new
                {
                    PaymentStatus = payment.PaymentStatus,
                    TransactionStatus = transaction.Status,
                    DepositStatus = transaction.Booking.DepositStatus,
                    BookingStatusID = transaction.Booking.BookingStatusID
                })
            });

            return new PaymentTransitionResult(true, "Payment confirmed", payment, transaction.Booking);
        }

        public async Task<PaymentTransitionResult> MarkPayOSFailedAsync(PayOSTransaction transaction, PaymentWebhookEvent webhookEvent)
        {
            var oldValues = JsonSerializer.Serialize(new
            {
                TransactionStatus = transaction.Status,
                PaymentStatus = transaction.Payment?.PaymentStatus,
                DepositStatus = transaction.Booking?.DepositStatus
            });

            transaction.Status = PaymentStatusConstants.Failed;
            transaction.FailedAt = DateTime.UtcNow;

            if (transaction.Payment != null && transaction.Payment.PaymentStatus != PaymentStatusConstants.Paid)
                transaction.Payment.PaymentStatus = PaymentStatusConstants.Failed;

            if (transaction.Booking != null)
                transaction.Booking.DepositStatus = DepositStatusConstants.Failed;

            if (transaction.BookingHoldID.HasValue)
                await _reservationService.ReleaseHoldAsync(transaction.BookingHoldID.Value, "PayOS payment failed.");

            webhookEvent.Status = "Processed";
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _audit.RecordAsync(new AuditLog
            {
                Action = "PayOSPaymentFailed",
                EntityType = nameof(PayOSTransaction),
                EntityId = transaction.PayOSTransactionID,
                Reason = webhookEvent.Error,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new
                {
                    TransactionStatus = transaction.Status,
                    PaymentStatus = transaction.Payment?.PaymentStatus,
                    DepositStatus = transaction.Booking?.DepositStatus
                })
            });

            return new PaymentTransitionResult(true, "Payment failed", transaction.Payment, transaction.Booking);
        }

        public async Task<PaymentTransitionResult> MarkCartPaymentUnderReviewAsync(PayOSTransaction transaction, BookingTransaction bookingTransaction, PaymentWebhookEvent webhookEvent, string reason)
        {
            var oldValues = JsonSerializer.Serialize(new
            {
                TransactionStatus = transaction.Status,
                CartStatus = bookingTransaction.Status
            });

            transaction.Status = DepositStatusConstants.UnderReview;
            transaction.PaidAt = DateTime.UtcNow;
            bookingTransaction.Status = DepositStatusConstants.UnderReview;
            webhookEvent.Status = "Processed";
            webhookEvent.Error = reason;
            webhookEvent.ProcessedAt = DateTime.UtcNow;

            await _ledger.RecordAsync(new PaymentLedgerEntry
            {
                TransactionID = bookingTransaction.TransactionID,
                Provider = "PayOS",
                OrderCode = transaction.OrderCode,
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = transaction.Amount,
                Status = DepositStatusConstants.UnderReview,
                PostedAt = DateTime.UtcNow,
                IdempotencyKey = $"payos-cart-under-review:{transaction.OrderCode}",
                Description = $"Payment received for cart transaction #{bookingTransaction.TransactionID}, but holds require manual review."
            });

            await _context.SaveChangesAsync();
            await _audit.RecordAsync(new AuditLog
            {
                Action = "PayOSCartPaymentUnderReview",
                EntityType = nameof(BookingTransaction),
                EntityId = bookingTransaction.TransactionID,
                Reason = reason,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new
                {
                    TransactionStatus = transaction.Status,
                    CartStatus = bookingTransaction.Status
                })
            });

            return new PaymentTransitionResult(true, "Cart payment requires manual review");
        }

        public async Task<PaymentTransitionResult> SetBookingDepositStatusAsync(int bookingId, string depositStatus, int? actorUserId, string actorRole, string reason)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingID == bookingId);
            if (booking == null)
                return new PaymentTransitionResult(false, "Booking not found.");

            var oldValues = JsonSerializer.Serialize(new { booking.DepositStatus });
            booking.DepositStatus = depositStatus;
            await _context.SaveChangesAsync();
            await _audit.RecordAsync(new AuditLog
            {
                Action = "BookingDepositTransition",
                EntityType = nameof(Booking),
                EntityId = booking.BookingID,
                UserId = actorUserId,
                UserName = actorRole,
                Reason = reason,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new { booking.DepositStatus })
            });

            return new PaymentTransitionResult(true, "Booking deposit status updated.", null, booking);
        }
    }
}
