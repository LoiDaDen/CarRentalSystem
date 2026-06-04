using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IPaymentLedgerService
    {
        Task<PaymentLedgerEntry> RecordAsync(PaymentLedgerEntry entry);
        Task<RefundRequest> CreateRefundRequestAsync(int bookingId, decimal amount, string reason);
        Task MarkRefundPaidAsync(int refundRequestId, int paidByUserId);
    }

    public class PaymentLedgerService : IPaymentLedgerService
    {
        private readonly ApplicationDbContext _context;

        public PaymentLedgerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentLedgerEntry> RecordAsync(PaymentLedgerEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.IdempotencyKey))
            {
                var existing = await _context.PaymentLedgerEntries
                    .FirstOrDefaultAsync(l => l.IdempotencyKey == entry.IdempotencyKey);
                if (existing != null) return existing;
            }

            _context.PaymentLedgerEntries.Add(entry);
            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task<RefundRequest> CreateRefundRequestAsync(int bookingId, decimal amount, string reason)
        {
            var existing = await _context.RefundRequests
                .FirstOrDefaultAsync(r => r.BookingID == bookingId &&
                    (r.Status == RefundRequestStatusConstants.PendingReview ||
                     r.Status == RefundRequestStatusConstants.Approved));

            if (existing != null) return existing;

            var request = new RefundRequest
            {
                BookingID = bookingId,
                Amount = amount,
                Reason = reason,
                Status = RefundRequestStatusConstants.PendingReview
            };

            _context.RefundRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task MarkRefundPaidAsync(int refundRequestId, int paidByUserId)
        {
            var request = await _context.RefundRequests
                .Include(r => r.Booking)
                .FirstOrDefaultAsync(r => r.RefundRequestID == refundRequestId);
            if (request == null) return;

            request.Status = RefundRequestStatusConstants.Paid;
            request.PaidByUserID = paidByUserId;
            request.PaidAt = DateTime.UtcNow;

            if (request.Booking != null)
                request.Booking.DepositStatus = DepositStatusConstants.Refunded;

            _context.PaymentLedgerEntries.Add(new PaymentLedgerEntry
            {
                BookingID = request.BookingID,
                EntryType = PaymentLedgerEntryTypeConstants.Refund,
                Amount = request.Amount,
                Provider = "Manual",
                Status = PaymentStatusConstants.Refunded,
                PostedAt = DateTime.UtcNow,
                IdempotencyKey = $"refund-paid:{refundRequestId}",
                Description = $"Manual refund reconciled for request #{refundRequestId}"
            });

            await _context.SaveChangesAsync();
        }
    }
}
