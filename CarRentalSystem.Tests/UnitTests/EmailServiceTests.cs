using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    /// <summary>
    /// Unit tests cho EmailService — Gửi email xác thực, fallback khi không có SMTP
    /// </summary>
    public class EmailServiceTests
    {
        private readonly Mock<ILogger<EmailService>> _loggerMock;

        public EmailServiceTests()
        {
            _loggerMock = new Mock<ILogger<EmailService>>();
        }

        /// <summary>
        /// Tạo IConfiguration giả lập với SmtpSettings tùy chỉnh
        /// </summary>
        private IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings!)
                .Build();
        }

        // =============================================
        // NO SMTP CONFIG TESTS (Dev Environment)
        // =============================================

        [Fact]
        [Trait("Module", "Email")]
        [Trait("Type", "Unit")]
        public async Task SendVerificationEmail_NoSmtpConfig_DoesNotThrow()
        {
            // Arrange — không cấu hình SMTP (giả lập môi trường Dev)
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                { "SmtpSettings:Host", "" },
                { "SmtpSettings:Port", "" },
                { "SmtpSettings:User", "" },
                { "SmtpSettings:Pass", "" }
            });
            var emailService = new EmailService(_loggerMock.Object, config);

            // Act
            var act = async () => await emailService.SendVerificationEmailAsync(
                "test@email.com",
                "https://localhost/verify?token=abc123"
            );

            // Assert
            await act.Should().NotThrowAsync("vì khi thiếu SMTP config, service phải fallback log mà không crash");
        }

        [Fact]
        [Trait("Module", "Email")]
        [Trait("Type", "Unit")]
        public async Task SendVerificationEmail_NoSmtpConfig_LogsWarning()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                { "SmtpSettings:Host", "" },
                { "SmtpSettings:Port", "" },
                { "SmtpSettings:User", "" },
                { "SmtpSettings:Pass", "" }
            });
            var emailService = new EmailService(_loggerMock.Object, config);

            // Act
            await emailService.SendVerificationEmailAsync("user@test.com", "https://localhost/verify?token=xyz");

            // Assert — Verify logger được gọi (LogWarning hoặc LogInformation)
            _loggerMock.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
                Times.AtLeastOnce,
                "vì khi không có SMTP config, service phải log thông tin thay vì gửi email"
            );
        }

        [Fact]
        [Trait("Module", "Email")]
        [Trait("Type", "Unit")]
        public async Task SendVerificationEmail_InvalidSmtpHost_DoesNotThrow()
        {
            // Arrange — cấu hình SMTP với host không hợp lệ
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                { "SmtpSettings:Host", "invalid.smtp.host.fake" },
                { "SmtpSettings:Port", "587" },
                { "SmtpSettings:User", "test@fake.com" },
                { "SmtpSettings:Pass", "fakepassword" }
            });
            var emailService = new EmailService(_loggerMock.Object, config);

            // Act
            var act = async () => await emailService.SendVerificationEmailAsync(
                "recipient@test.com",
                "https://localhost/verify?token=test123"
            );

            // Assert
            await act.Should().NotThrowAsync(
                "vì khi SMTP host không hợp lệ, service phải catch exception và log lỗi thay vì crash");
        }
    }
}
