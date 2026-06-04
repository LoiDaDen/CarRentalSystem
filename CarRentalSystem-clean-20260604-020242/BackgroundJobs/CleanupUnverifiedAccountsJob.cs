using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.BackgroundJobs
{
    /// <summary>
    /// Background Job xóa tài khoản chưa xác thực email sau 24 giờ kể từ khi đăng ký.
    /// Job này chạy định kỳ mỗi giờ để dọn dẹp tài khoản rác.
    /// </summary>
    public class CleanupUnverifiedAccountsJob
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CleanupUnverifiedAccountsJob> _logger;

        public CleanupUnverifiedAccountsJob(ApplicationDbContext context, ILogger<CleanupUnverifiedAccountsJob> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Xóa tất cả tài khoản chưa xác thực email và đã tạo quá 24 giờ.
        /// Sử dụng batch delete để tránh load toàn bộ user vào RAM.
        /// </summary>
        public async Task ExecuteAsync()
        {
            _logger.LogInformation("🚀 [Hangfire] Bắt đầu dọn dẹp tài khoản chưa xác thực...");

            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-24);

                // Đếm số lượng trước khi xóa
                var countToDelete = await _context.Users
                    .Where(u => !u.IsEmailVerified && u.CreatedAt < cutoffTime)
                    .CountAsync();

                if (countToDelete == 0)
                {
                    _logger.LogInformation("✅ [Hangfire] Không có tài khoản nào cần xóa.");
                    return;
                }

                _logger.LogInformation("📋 [Hangfire] Tìm thấy {Count} tài khoản chưa xác thực quá 24 giờ. Đang xóa...", countToDelete);

                // Xóa CartItems liên quan trước (CASCADE)
                var userIdsToDelete = await _context.Users
                    .Where(u => !u.IsEmailVerified && u.CreatedAt < cutoffTime)
                    .Select(u => u.UserID)
                    .ToListAsync();

                var cartItemsToDelete = await _context.CartItems
                    .Where(ci => _context.Carts.Any(c => userIdsToDelete.Contains(c.UserID) && c.CartID == ci.CartID))
                    .ToListAsync();

                if (cartItemsToDelete.Count > 0)
                {
                    _context.CartItems.RemoveRange(cartItemsToDelete);
                }

                // Xóa Carts liên quan
                var cartsToDelete = await _context.Carts
                    .Where(c => userIdsToDelete.Contains(c.UserID))
                    .ToListAsync();

                if (cartsToDelete.Count > 0)
                {
                    _context.Carts.RemoveRange(cartsToDelete);
                }

                // Xóa Users
                await _context.Users
                    .Where(u => !u.IsEmailVerified && u.CreatedAt < cutoffTime)
                    .ExecuteDeleteAsync();

                _logger.LogInformation("✅ [Hangfire] Đã xóa thành công {Count} tài khoản chưa xác thực.", countToDelete);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Hangfire] Lỗi khi dọn dẹp tài khoản chưa xác thực.");
                throw;
            }
        }
    }
}
