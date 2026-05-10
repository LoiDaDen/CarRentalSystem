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
                throw;
            }
        }

        /// <summary>
        /// Gửi email chứa mã OTP 6 số để xác thực tài khoản.
        /// Mã OTP có hiệu lực trong 5 phút.
        /// </summary>
        public async Task SendOtpEmail(string email, string otp)
        {
            _logger.LogInformation("🚀 [Hangfire] Bắt đầu gửi OTP đến: {Email}", email);

            try
            {
                await _emailService.SendOtpEmailAsync(email, otp);
                _logger.LogInformation("✅ [Hangfire] Gửi OTP thành công đến: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Hangfire] Gửi OTP THẤT BẠI đến: {Email}. Hangfire sẽ tự retry.", email);
                throw;
            }
        }
    }
}
