using System;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace CarRentalSystem.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string verificationLink);
    }

    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            string host = smtpSettings["Host"];
            string portStr = smtpSettings["Port"];
            string user = smtpSettings["User"];
            string pass = smtpSettings["Pass"];

            string subject = "Xác thực tài khoản của bạn - Hệ Thống Thuê Xe";
            string body = $@"
                <h3>Chào bạn,</h3>
                <p>Cảm ơn bạn đã đăng ký tài khoản. Vui lòng click vào link bên dưới để xác thực email của bạn:</p>
                <p><a href='{verificationLink}'>{verificationLink}</a></p>
                <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>
            ";

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user))
            {
                // Môi trường Dev không cấu hình SMTP
                _logger.LogWarning("SMTP Settings not found. Logging email instead:");
                _logger.LogInformation("================ EMAIL VERIFICATION ====================");
                _logger.LogInformation("To: " + toEmail);
                _logger.LogInformation("Link: " + verificationLink);
                _logger.LogInformation("========================================================");
                await Task.CompletedTask;
                return;
            }

            try
            {
                int port = int.TryParse(portStr, out int p) ? p : 587;
                var client = new SmtpClient(host, port)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(user, pass)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(user, "Car Rental Administrator"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send email to {toEmail}: {ex.Message}");
                _logger.LogInformation("Fallback: Verification Link -> " + verificationLink);
            }
        }
    }
}
