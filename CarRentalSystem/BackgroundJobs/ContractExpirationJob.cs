using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.BackgroundJobs
{
    /// <summary>
    /// Hangfire Recurring Job thay thế ContractExpirationService (BackgroundService cũ).
    /// Chạy mỗi giờ để kiểm tra và đánh dấu các hợp đồng quá hạn ký.
    /// Ưu điểm so với BackgroundService:
    /// - Có UI Dashboard theo dõi lịch sử chạy
    /// - Tự retry khi fail
    /// - Dễ debug qua /hangfire
    /// </summary>
    public class ContractExpirationJob
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ContractExpirationJob> _logger;

        public ContractExpirationJob(ApplicationDbContext db, ILogger<ContractExpirationJob> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Kiểm tra và expire các hợp đồng chưa ký trong 7 ngày.
        /// Được Hangfire gọi tự động mỗi giờ (Recurring Job).
        /// </summary>
        public async Task Execute()
        {
            _logger.LogInformation("⏰ [Hangfire] Bắt đầu kiểm tra hợp đồng hết hạn...");
            var now = DateTime.Now;

            // ═══ Expire Driver Contracts ═══
            var expiredDriverContracts = await _db.DriverContracts
                .Where(dc => dc.Status == "Pending" && dc.ExpiresAt < now)
                .ToListAsync();

            foreach (var contract in expiredDriverContracts)
            {
                contract.Status = "Expired";
                _logger.LogInformation("Driver contract #{ContractNo} expired (not signed within 7 days).", contract.ContractNo);
            }

            // ═══ Expire Partner Contracts ═══
            var expiredPartnerContracts = await _db.PartnerContracts
                .Where(pc => pc.Status == "Pending" && pc.ExpiresAt < now)
                .ToListAsync();

            foreach (var contract in expiredPartnerContracts)
            {
                contract.Status = "Expired";
                _logger.LogInformation("Partner contract #{ContractNo} expired (not signed within 7 days).", contract.ContractNo);
            }

            if (expiredDriverContracts.Count > 0 || expiredPartnerContracts.Count > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("✅ [Hangfire] Expired {DriverCount} driver contracts and {PartnerCount} partner contracts.",
                    expiredDriverContracts.Count, expiredPartnerContracts.Count);
            }
            else
            {
                _logger.LogInformation("✅ [Hangfire] Không có hợp đồng nào hết hạn.");
            }
        }
    }
}
