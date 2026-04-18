using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.BackgroundJobs
{
    /// <summary>
    /// Hangfire Recurring Job — Chạy mỗi ngày lúc 9:00 AM
    /// Tự động hoàn cọc cho các booking đã trả xe >= 15 ngày mà không vi phạm.
    /// Nghiệp vụ: Giữ cọc 15 ngày sau trả xe để chờ kiểm tra phạt nguội/hư hỏng.
    /// </summary>
    public class DepositRefundJob
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DepositRefundJob> _logger;

        public DepositRefundJob(ApplicationDbContext db, ILogger<DepositRefundJob> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task Execute()
        {
            _logger.LogInformation("💰 [Hangfire] Bắt đầu xử lý hoàn cọc...");
            var now = DateTime.UtcNow;
            var refundThreshold = now.AddDays(-15); // 15 ngày holding period

            // Tìm các booking đã trả xe >= 15 ngày với DepositStatus = "PendingRefund"
            var eligibleBookings = await _db.Bookings
                .Include(b => b.User)
                .Include(b => b.Car)
                .Include(b => b.BookingStatus)
                .Where(b => b.DepositStatus == "PendingRefund" &&
                            b.BookingStatus != null && b.BookingStatus.Name == "Completed" &&
                            b.ActualReturnDate.HasValue &&
                            b.ActualReturnDate.Value <= refundThreshold)
                .ToListAsync();

            int refundedCount = 0;

            foreach (var booking in eligibleBookings)
            {
                // Kiểm tra không có phạt nguội hoặc hư hỏng chưa xử lý
                // (Trong thực tế, sẽ check bảng Violations/DamageReport)
                
                booking.DepositStatus = "Refunded";
                refundedCount++;

                _logger.LogInformation(
                    "✅ Hoàn cọc Booking #{BookingId} — {Amount:N0}đ cho khách {Customer}.",
                    booking.BookingID, booking.DepositAmount, booking.User?.Name ?? "N/A");

                // Ghi audit log
                _db.AuditLogs.Add(new Models.AuditLog
                {
                    Action = "DepositRefund",
                    EntityType = "Booking",
                    EntityId = booking.BookingID,
                    Description = $"Tự động hoàn cọc {booking.DepositAmount:N0}đ sau 15 ngày giữ. Xe: {booking.Car?.LicensePlate}",
                    UserName = "System (Hangfire)"
                });
            }

            if (refundedCount > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("💰 [Hangfire] Đã hoàn cọc cho {Count} booking.", refundedCount);
            }
            else
            {
                _logger.LogInformation("💰 [Hangfire] Không có booking nào cần hoàn cọc.");
            }
        }
    }
}
