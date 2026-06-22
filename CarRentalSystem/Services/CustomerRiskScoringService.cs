using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public sealed record CustomerRiskAssessmentResult(
        int UserID,
        int Score,
        string RiskLevel,
        string ModelVersion,
        IReadOnlyList<string> ReasonCodes,
        IReadOnlyDictionary<string, object> FeatureSnapshot,
        DateTime AssessedAtUtc);

    public interface ICustomerRiskScoringService
    {
        Task<CustomerRiskAssessmentResult> AssessAsync(int userId, DateTime asOfUtc, CancellationToken cancellationToken = default);
    }

    public sealed class CustomerRiskScoringService : ICustomerRiskScoringService
    {
        private static readonly string[] CompletedStatuses = { BookingStatusConstants.Completed };
        private static readonly string[] CancelledStatuses = { BookingStatusConstants.Cancelled, BookingStatusConstants.Rejected, BookingStatusConstants.Expired };
        private static readonly string[] ClaimTypes = { SettlementReviewTypeConstants.Damage, SettlementReviewTypeConstants.LostItem, SettlementReviewTypeConstants.Violation };

        private readonly ApplicationDbContext _context;

        public CustomerRiskScoringService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CustomerRiskAssessmentResult> AssessAsync(int userId, DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken)
                ?? throw new InvalidOperationException($"User #{userId} was not found.");

            var bookings = await _context.Bookings.AsNoTracking()
                .Include(b => b.BookingStatus)
                .Where(b => b.UserID == userId && b.CreatedAt <= asOfUtc)
                .Select(b => new
                {
                    b.BookingID,
                    b.CreatedAt,
                    b.EndDate,
                    b.ActualReturnDate,
                    StatusName = b.BookingStatus != null ? b.BookingStatus.Name : null
                })
                .ToListAsync(cancellationToken);

            var bookingIds = bookings.Select(b => b.BookingID).ToList();
            var paymentFailureCount = await _context.PaymentLedgerEntries.AsNoTracking()
                .CountAsync(l => l.BookingID != null &&
                    bookingIds.Contains(l.BookingID.Value) &&
                    (l.Status == PaymentStatusConstants.Failed || l.Status == PaymentStatusConstants.Cancelled),
                    cancellationToken);

            var claimCount = await _context.SettlementReviews.AsNoTracking()
                .CountAsync(s => bookingIds.Contains(s.BookingID) && ClaimTypes.Contains(s.ReviewType), cancellationToken);

            var approvedSettlementAmount = await _context.SettlementReviews.AsNoTracking()
                .Where(s => bookingIds.Contains(s.BookingID) && s.Status == SettlementReviewStatusConstants.Approved)
                .SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0m;

            var completedCount = bookings.Count(b => b.StatusName != null && CompletedStatuses.Contains(b.StatusName));
            var cancelledCount = bookings.Count(b => b.StatusName != null && CancelledStatuses.Contains(b.StatusName));
            var lateReturnCount = bookings.Count(b => b.ActualReturnDate != null && b.ActualReturnDate > b.EndDate);
            var accountAgeDays = Math.Max(0, (int)Math.Floor((asOfUtc - user.CreatedAt).TotalDays));

            var score = 10;
            var reasons = new List<string>();

            AddScoreIf(user.IsBlacklisted, 100, "BLACKLISTED");
            AddScoreIf(user.IsLocked, 65, "ACCOUNT_LOCKED");
            AddScoreIf(!user.IsEmailVerified, 15, "EMAIL_NOT_VERIFIED");
            AddScoreIf(user.VerificationStatus != "Verified" || !user.IsVerified, 25, "EKYC_NOT_VERIFIED");
            AddScoreIf(accountAgeDays < 7, 10, "NEW_ACCOUNT_LT_7D");
            AddScoreIf(completedCount >= 5, -12, "TRUSTED_COMPLETED_5_PLUS");
            AddScoreIf(cancelledCount >= 2, Math.Min(20, cancelledCount * 5), "MULTIPLE_CANCELLED_BOOKINGS");
            AddScoreIf(lateReturnCount > 0, Math.Min(25, lateReturnCount * 10), "LATE_RETURN_HISTORY");
            AddScoreIf(paymentFailureCount > 0, Math.Min(30, paymentFailureCount * 10), "PAYMENT_FAILURE_HISTORY");
            AddScoreIf(claimCount > 0, Math.Min(35, claimCount * 15), "CLAIM_OR_DAMAGE_HISTORY");
            AddScoreIf(approvedSettlementAmount > 0, Math.Min(30, (int)Math.Ceiling(approvedSettlementAmount / 500000m) * 5), "APPROVED_SETTLEMENT_AMOUNT");

            score = Math.Clamp(score, 0, 100);
            var riskLevel = score switch
            {
                >= 85 => CustomerRiskLevels.Critical,
                >= 65 => CustomerRiskLevels.High,
                >= 35 => CustomerRiskLevels.Medium,
                _ => CustomerRiskLevels.Low
            };

            var features = new Dictionary<string, object>
            {
                ["accountAgeDays"] = accountAgeDays,
                ["isEmailVerified"] = user.IsEmailVerified,
                ["isEkycVerified"] = user.VerificationStatus == "Verified" && user.IsVerified,
                ["completedBookingCount"] = completedCount,
                ["cancelledBookingCount"] = cancelledCount,
                ["lateReturnCount"] = lateReturnCount,
                ["paymentFailureCount"] = paymentFailureCount,
                ["claimCount"] = claimCount,
                ["approvedSettlementAmount"] = approvedSettlementAmount,
                ["isBlacklisted"] = user.IsBlacklisted,
                ["isLocked"] = user.IsLocked
            };

            var result = new CustomerRiskAssessmentResult(
                user.UserID,
                score,
                riskLevel,
                CustomerRiskModelVersions.RuleBasedV1,
                reasons,
                features,
                asOfUtc);

            _context.CustomerRiskAssessments.Add(new CustomerRiskAssessment
            {
                UserID = result.UserID,
                Score = result.Score,
                RiskLevel = result.RiskLevel,
                ModelVersion = result.ModelVersion,
                ReasonCodesJson = JsonSerializer.Serialize(result.ReasonCodes),
                FeatureSnapshotJson = JsonSerializer.Serialize(result.FeatureSnapshot),
                AssessedAtUtc = result.AssessedAtUtc
            });
            await _context.SaveChangesAsync(cancellationToken);

            return result;

            void AddScoreIf(bool condition, int delta, string reason)
            {
                if (!condition)
                    return;

                score += delta;
                reasons.Add(reason);
            }
        }
    }
}
