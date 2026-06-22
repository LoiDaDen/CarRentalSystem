using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.BackgroundJobs
{
    public class DepositRefundJob
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DepositRefundJob> _logger;
        private readonly IPaymentLedgerService _ledger;

        public DepositRefundJob(
            ApplicationDbContext db,
            ILogger<DepositRefundJob> logger,
            IPaymentLedgerService ledger)
        {
            _db = db;
            _logger = logger;
            _ledger = ledger;
        }

        public async Task Execute()
        {
            var now = DateTime.UtcNow;
            var refundThreshold = now.AddDays(-15);

            var eligibleBookings = await _db.Bookings
                .Include(b => b.User)
                .Include(b => b.Car)
                .Include(b => b.BookingStatus)
                .Where(b => b.DepositStatus == DepositStatusConstants.PendingRefund &&
                            b.BookingStatus != null &&
                            b.BookingStatus.Name == BookingStatusConstants.Completed &&
                            b.ActualReturnDate.HasValue &&
                            b.ActualReturnDate.Value <= refundThreshold)
                .ToListAsync();

            var createdCount = 0;
            var updatedCount = 0;
            foreach (var booking in eligibleBookings)
            {
                var approvedDeductions = await _db.SettlementReviews
                    .Where(r => r.BookingID == booking.BookingID && r.Status == SettlementReviewStatusConstants.Approved)
                    .SumAsync(r => r.Amount);
                var refundableAmount = Math.Max(0, booking.DepositAmount - approvedDeductions);

                if (refundableAmount <= 0)
                {
                    booking.DepositStatus = DepositStatusConstants.Deducted;
                    _db.AuditLogs.Add(new AuditLog
                    {
                        Action = "DepositRefundSkipped",
                        EntityType = "Booking",
                        EntityId = booking.BookingID,
                        Description = $"No remaining deposit to refund after approved deductions {approvedDeductions:N0}. Car: {booking.Car?.LicensePlate}",
                        UserName = "System (Hangfire)"
                    });
                    updatedCount++;
                    continue;
                }

                var request = await _ledger.CreateRefundRequestAsync(
                    booking.BookingID,
                    refundableAmount,
                    $"Eligible for deposit refund after 15-day holding period. Approved deductions: {approvedDeductions:N0}.");

                booking.DepositStatus = DepositStatusConstants.PendingRefund;
                createdCount++;

                _db.AuditLogs.Add(new AuditLog
                {
                    Action = "DepositRefundRequest",
                    EntityType = "Booking",
                    EntityId = booking.BookingID,
                    Description = $"Created refund request #{request.RefundRequestID} for {refundableAmount:N0}. Approved deductions: {approvedDeductions:N0}. Car: {booking.Car?.LicensePlate}",
                    UserName = "System (Hangfire)"
                });
            }

            if (createdCount > 0 || updatedCount > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("Created {Count} deposit refund requests and updated {UpdatedCount} deposit statuses.", createdCount, updatedCount);
            }
        }
    }
}
