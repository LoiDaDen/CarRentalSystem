using CarRentalSystem.Services;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.BackgroundJobs
{
    /// <summary>
    /// Background Job xử lý gửi Email không đồng bộ thông qua Hangfire Queue.
    /// Thay vì bắt người dùng chờ email gửi xong mới thấy response,
    /// ta ném công việc này vào hàng đợi để xử lý ngầm.
    /// </summary>
    public class EmailBackgroundJob
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailBackgroundJob> _logger;

        public EmailBackgroundJob(IEmailService emailService, ILogger<EmailBackgroundJob> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Gửi email xác thực tài khoản — được gọi bởi Hangfire Queue.
        /// Hangfire sẽ tự retry 10 lần nếu thất bại (mặc định).
        /// </summary>
        public async Task SendVerificationEmail(string email, string verificationLink)
        {
            _logger.LogInformation("🚀 [Hangfire] Bắt đầu gửi email xác thực đến: {Email}", email);
            
            try
            {
                await _emailService.SendVerificationEmailAsync(email, verificationLink);
                _logger.LogInformation("✅ [Hangfire] Gửi email xác thực thành công đến: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Hangfire] Gửi email xác thực THẤT BẠI đến: {Email}. Hangfire sẽ tự retry.", email);
                throw; // Để Hangfire bắt exception và tự thực hiện retry
            }
        }
    }
}
