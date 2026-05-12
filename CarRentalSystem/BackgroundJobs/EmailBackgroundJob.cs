using CarRentalSystem.Services;
using CarRentalSystem.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

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
        private readonly ApplicationDbContext _db;
        private readonly ILogger<EmailBackgroundJob> _logger;

        public EmailBackgroundJob(IEmailService emailService, ApplicationDbContext db, ILogger<EmailBackgroundJob> logger)
        {
            _emailService = emailService;
            _db = db;
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

        public async Task SendApprovalEmail(string email, string recipientName, string roleName, string contractNo, DateTime expiresAt, string contractUrl, string culture = "vi")
        {
            _logger.LogInformation("[Hangfire] Sending {Role} approval email to {Email}", roleName, email);
            await _emailService.SendApprovalEmailAsync(email, recipientName, roleName, contractNo, expiresAt, contractUrl, culture);
        }

        public async Task SendDriverContractReminder(int driverContractId, int daysBeforeDeadline, string contractUrl, string culture = "vi")
        {
            var contract = await _db.DriverContracts
                .Include(c => c.Driver)
                .ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(c => c.DriverContractID == driverContractId);

            if (contract?.Driver == null || contract.Status != "Pending" || contract.SignedByPartyB || DateTime.UtcNow > contract.ExpiresAt)
                return;

            var email = contract.Driver.Email ?? contract.Driver.User?.Email;
            if (string.IsNullOrWhiteSpace(email))
                return;

            await _emailService.SendContractReminderEmailAsync(
                email,
                contract.Driver.FullName,
                "driver",
                contract.ContractNo,
                contract.ExpiresAt,
                contractUrl,
                daysBeforeDeadline,
                culture);
        }

        public async Task SendPartnerContractReminder(int partnerContractId, int daysBeforeDeadline, string contractUrl, string culture = "vi")
        {
            var contract = await _db.PartnerContracts
                .Include(c => c.PartnerUser)
                .FirstOrDefaultAsync(c => c.PartnerContractID == partnerContractId);

            if (contract?.PartnerUser == null || contract.Status != "Pending" || contract.SignedByPartyB || DateTime.UtcNow > contract.ExpiresAt)
                return;

            await _emailService.SendContractReminderEmailAsync(
                contract.PartnerUser.Email,
                contract.PartnerUser.Name,
                "partner",
                contract.ContractNo,
                contract.ExpiresAt,
                contractUrl,
                daysBeforeDeadline,
                culture);
        }
    }
}
