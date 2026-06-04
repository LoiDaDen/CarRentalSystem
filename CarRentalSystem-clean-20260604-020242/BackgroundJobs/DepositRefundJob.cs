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
            foreach (var booking in eligibleBookings)
            {
                var request = await _ledger.CreateRefundRequestAsync(
                    booking.BookingID,
                    booking.DepositAmount,
                    "Eligible for deposit refund after 15-day holding period.");

                booking.DepositStatus = DepositStatusConstants.PendingRefund;
                createdCount++;

                _db.AuditLogs.Add(new AuditLog
                {
                    Action = "DepositRefundRequest",
                    EntityType = "Booking",
                    EntityId = booking.BookingID,
                    Description = $"Created refund request #{request.RefundRequestID} for {booking.DepositAmount:N0}. Car: {booking.Car?.LicensePlate}",
                    UserName = "System (Hangfire)"
                });
            }

            if (createdCount > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("Created {Count} deposit refund requests.", createdCount);
            }
        }
    }
}
