using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ISettlementReviewService
    {
        Task<SettlementReview> CreateAsync(int bookingId, string reviewType, decimal amount, string reason, int requestedByUserId, int? handoverId = null);
        Task<SettlementReviewServiceResult> ApproveAsync(int settlementReviewId, int reviewedByUserId, string reason);
        Task<SettlementReviewServiceResult> RejectAsync(int settlementReviewId, int reviewedByUserId, string reason);
    }

    public record SettlementReviewServiceResult(bool IsSuccess, string Message, SettlementReview? Review = null);

    public class SettlementReviewService : ISettlementReviewService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentLedgerService _ledger;
        private readonly IAuditTrailService _audit;
        private readonly IEnterpriseTelemetryService _telemetry;
        private readonly IProductAnalyticsService _analytics;

        public SettlementReviewService(ApplicationDbContext context, IPaymentLedgerService ledger, IAuditTrailService audit, IEnterpriseTelemetryService telemetry, IProductAnalyticsService analytics)
        {
            _context = context;
            _ledger = ledger;
            _audit = audit;
            _telemetry = telemetry;
            _analytics = analytics;
        }

        public async Task<SettlementReview> CreateAsync(int bookingId, string reviewType, decimal amount, string reason, int requestedByUserId, int? handoverId = null)
        {
            using var activity = _telemetry.StartActivity("settlement_review_created",
                new("booking.id", bookingId),
                new("settlement.type", reviewType),
                new("settlement.amount", amount));
            var review = new SettlementReview
            {
                BookingID = bookingId,
                HandoverID = handoverId,
                ReviewType = NormalizeType(reviewType),
                Amount = amount,
                Reason = reason,
                RequestedByUserID = requestedByUserId,
                Status = SettlementReviewStatusConstants.PendingReview
            };

            _context.SettlementReviews.Add(review);
            await _context.SaveChangesAsync();
            await _audit.RecordAsync(new AuditLog
            {
                Action = "CreateSettlementReview",
                EntityType = nameof(SettlementReview),
                EntityId = review.SettlementReviewID,
                UserId = requestedByUserId,
                Reason = reason,
                NewValues = JsonSerializer.Serialize(new { bookingId, review.ReviewType, amount, handoverId })
            });
            await _analytics.TrackAsync("settlement_review_created", new Dictionary<string, object?>
            {
                ["booking_id"] = bookingId,
                ["settlement_type"] = review.ReviewType,
                ["amount"] = amount,
                ["status"] = review.Status
            });
            return review;
        }

        public async Task<SettlementReviewServiceResult> ApproveAsync(int settlementReviewId, int reviewedByUserId, string reason)
        {
            using var activity = _telemetry.StartActivity("settlement_approval",
                new("settlement_review.id", settlementReviewId),
                new("settlement.status", SettlementReviewStatusConstants.Approved));
            var review = await _context.SettlementReviews.FirstOrDefaultAsync(r => r.SettlementReviewID == settlementReviewId);
            if (review == null)
                return new SettlementReviewServiceResult(false, "Settlement review not found.");
            if (review.Status != SettlementReviewStatusConstants.PendingReview)
                return new SettlementReviewServiceResult(false, "Settlement review is not pending.", review);

            var oldValues = JsonSerializer.Serialize(new { review.Status, review.PaymentLedgerEntryID });
            var ledger = await _ledger.RecordAsync(new PaymentLedgerEntry
            {
                BookingID = review.BookingID,
                EntryType = PaymentLedgerEntryTypeConstants.Deduction,
                Amount = review.Amount,
                Status = PaymentStatusConstants.Paid,
                Provider = "Internal",
                IdempotencyKey = $"settlement-review:{review.SettlementReviewID}:approved",
                Description = $"Approved settlement review {review.ReviewType}: {review.Reason}",
                PostedAt = DateTime.UtcNow
            });

            review.Status = SettlementReviewStatusConstants.Approved;
            review.ReviewedByUserID = reviewedByUserId;
            review.ReviewedAt = DateTime.UtcNow;
            review.PaymentLedgerEntryID = ledger.PaymentLedgerEntryID;
            await _context.SaveChangesAsync();

            await _audit.RecordAsync(new AuditLog
            {
                Action = "ApproveSettlementReview",
                EntityType = nameof(SettlementReview),
                EntityId = review.SettlementReviewID,
                UserId = reviewedByUserId,
                Reason = reason,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new { review.Status, review.PaymentLedgerEntryID })
            });

            return new SettlementReviewServiceResult(true, "Settlement review approved.", review);
        }

        public async Task<SettlementReviewServiceResult> RejectAsync(int settlementReviewId, int reviewedByUserId, string reason)
        {
            var review = await _context.SettlementReviews.FirstOrDefaultAsync(r => r.SettlementReviewID == settlementReviewId);
            if (review == null)
                return new SettlementReviewServiceResult(false, "Settlement review not found.");
            if (review.Status != SettlementReviewStatusConstants.PendingReview)
                return new SettlementReviewServiceResult(false, "Settlement review is not pending.", review);

            var oldValues = JsonSerializer.Serialize(new { review.Status });
            review.Status = SettlementReviewStatusConstants.Rejected;
            review.ReviewedByUserID = reviewedByUserId;
            review.ReviewedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.RecordAsync(new AuditLog
            {
                Action = "RejectSettlementReview",
                EntityType = nameof(SettlementReview),
                EntityId = review.SettlementReviewID,
                UserId = reviewedByUserId,
                Reason = reason,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new { review.Status })
            });

            return new SettlementReviewServiceResult(true, "Settlement review rejected.", review);
        }

        private static string NormalizeType(string reviewType)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                SettlementReviewTypeConstants.Late,
                SettlementReviewTypeConstants.Fuel,
                SettlementReviewTypeConstants.Cleaning,
                SettlementReviewTypeConstants.Damage,
                SettlementReviewTypeConstants.Toll,
                SettlementReviewTypeConstants.Violation,
                SettlementReviewTypeConstants.Other
            };

            return allowed.Contains(reviewType) ? reviewType : SettlementReviewTypeConstants.Other;
        }
    }
}
