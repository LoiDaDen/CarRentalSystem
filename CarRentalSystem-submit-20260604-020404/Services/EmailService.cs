using System;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string verificationLink);
        Task SendOtpEmailAsync(string toEmail, string otp);
        Task SendPasswordResetOtpEmailAsync(string toEmail, string otp);
        Task SendBookingConfirmationEmailAsync(Booking booking, decimal paidAmount);
        Task SendApprovalEmailAsync(string toEmail, string recipientName, string roleName, string contractNo, DateTime expiresAt, string contractUrl, string culture = "vi");
        Task SendContractReminderEmailAsync(string toEmail, string recipientName, string roleName, string contractNo, DateTime expiresAt, string contractUrl, int daysBeforeDeadline, string culture = "vi");
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

            // Encode verification link để tránh XSS
            var encodedLink = System.Net.WebUtility.HtmlEncode(verificationLink);
            string body = $@"
                <h3>Chào bạn,</h3>
                <p>Cảm ơn bạn đã đăng ký tài khoản. Vui lòng click vào link bên dưới để xác thực email của bạn:</p>
                <p><a href='{encodedLink}'>{encodedLink}</a></p>
                <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>
            ";

            await SendEmailAsync(host, portStr, user, pass, toEmail, subject, body);
        }

        public async Task SendOtpEmailAsync(string toEmail, string otp)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            string host = smtpSettings["Host"];
            string portStr = smtpSettings["Port"];
            string user = smtpSettings["User"];
            string pass = smtpSettings["Pass"];

            string subject = "Mã xác thực OTP - Hệ Thống Thuê Xe";
            string body = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <title>Xác thực tài khoản</title>
</head>
<body style='margin:0;padding:0;font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;padding:30px 10px;'>
        <tr>
            <td align='center'>
                <table width='500' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);'>
                    <tr>
                        <td style='background:linear-gradient(135deg,#0369a1 0%,#0ea5e9 50%,#38bdf8 100%);padding:32px 40px;text-align:center;'>
                            <h1 style='color:#ffffff;margin:0;font-size:24px;font-weight:700;letter-spacing:0.5px;'>CarRental</h1>
                            <p style='color:rgba(255,255,255,0.85);margin:8px 0 0;font-size:14px;'>Xác thực tài khoản của bạn</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:30px 40px;text-align:center;'>
                            <p style='margin:0 0 8px;font-size:15px;color:#1e293b;'>Cảm ơn bạn đã đăng ký tài khoản.</p>
                            <p style='margin:0 0 24px;font-size:14px;color:#64748b;'>Sử dụng mã OTP bên dưới để xác thực tài khoản của bạn:</p>
                            <div style='background:#f0f9ff;border:2px dashed #0ea5e9;border-radius:12px;padding:20px 32px;display:inline-block;margin-bottom:20px;'>
                                <span style='font-size:36px;font-weight:800;color:#0369a1;letter-spacing:8px;'>{otp}</span>
                            </div>
                            <p style='margin:0;font-size:13px;color:#dc2626;font-weight:600;'>&#9888; Ma nay chi co hieu luc trong <strong>5 phut</strong>.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:0 40px 30px;'>
                            <div style='background:#fffbeb;border:1px solid #fcd34d;border-radius:8px;padding:14px 18px;'>
                                <p style='margin:0;font-size:12px;color:#92400e;line-height:1.6;'>
                                    Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.<br/>
                                    Không chia sẻ mã OTP cho bất kỳ ai.
                                </p>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style='background:#0f172a;padding:20px 40px;text-align:center;'>
                            <p style='margin:0;font-size:12px;color:#64748b;'>CarRental - Hệ thống thuê xe thông minh | 1900 1234</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailAsync(host, portStr, user, pass, toEmail, subject, body);
        }

        public async Task SendPasswordResetOtpEmailAsync(string toEmail, string otp)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            string host = smtpSettings["Host"];
            string portStr = smtpSettings["Port"];
            string user = smtpSettings["User"];
            string pass = smtpSettings["Pass"];

            string subject = "Mã đặt lại mật khẩu - CarRental";
            string body = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <title>Đặt lại mật khẩu</title>
</head>
<body style='margin:0;padding:0;font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;padding:30px 10px;'>
        <tr>
            <td align='center'>
                <table width='500' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);'>
                    <tr>
                        <td style='background:linear-gradient(135deg,#0f172a 0%,#2563eb 100%);padding:32px 40px;text-align:center;'>
                            <h1 style='color:#ffffff;margin:0;font-size:24px;font-weight:700;'>CarRental</h1>
                            <p style='color:rgba(255,255,255,0.85);margin:8px 0 0;font-size:14px;'>Đặt lại mật khẩu của bạn</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:30px 40px;text-align:center;'>
                            <p style='margin:0 0 8px;font-size:15px;color:#1e293b;'>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu.</p>
                            <p style='margin:0 0 24px;font-size:14px;color:#64748b;'>Sử dụng mã OTP bên dưới để tạo mật khẩu mới:</p>
                            <div style='background:#eff6ff;border:2px dashed #2563eb;border-radius:12px;padding:20px 32px;display:inline-block;margin-bottom:20px;'>
                                <span style='font-size:36px;font-weight:800;color:#1d4ed8;letter-spacing:8px;'>{otp}</span>
                            </div>
                            <p style='margin:0;font-size:13px;color:#dc2626;font-weight:600;'>&#9888; Mã này chỉ có hiệu lực trong <strong>5 phút</strong>.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:0 40px 30px;'>
                            <div style='background:#fffbeb;border:1px solid #fcd34d;border-radius:8px;padding:14px 18px;'>
                                <p style='margin:0;font-size:12px;color:#92400e;line-height:1.6;'>
                                    Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.<br/>
                                    Không chia sẻ mã OTP cho bất kỳ ai.
                                </p>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style='background:#0f172a;padding:20px 40px;text-align:center;'>
                            <p style='margin:0;font-size:12px;color:#64748b;'>CarRental - Hệ thống thuê xe thông minh | 1900 1234</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailAsync(host, portStr, user, pass, toEmail, subject, body);
        }

        public async Task SendBookingConfirmationEmailAsync(Booking booking, decimal paidAmount)
        {
            if (booking == null || booking.User == null)
            {
                _logger.LogWarning("Cannot send booking email: booking or user is null");
                return;
            }

            var smtpSettings = _configuration.GetSection("SmtpSettings");
            string host = smtpSettings["Host"];
            string portStr = smtpSettings["Port"];
            string user = smtpSettings["User"];
            string pass = smtpSettings["Pass"];
            string toEmail = booking.User.Email;

            string subject = $"[CarRental] Xác nhận đặt xe #{booking.BookingID:D4} - Thanh toán thành công";

            decimal depositAmount = paidAmount;
            decimal remainingAmount = booking.TotalPrice - depositAmount;

            string body = BuildBookingEmailHtml(booking, depositAmount, remainingAmount);

            await SendEmailAsync(host, portStr, user, pass, toEmail, subject, body);
        }

        public async Task SendApprovalEmailAsync(string toEmail, string recipientName, string roleName, string contractNo, DateTime expiresAt, string contractUrl, string culture = "vi")
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var isEnglish = string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase)
                || culture.StartsWith("en-", StringComparison.OrdinalIgnoreCase);

            var subject = isEnglish
                ? $"[CarRental] Your {roleName} profile has been approved"
                : $"[CarRental] Hồ sơ {roleName} của bạn đã được duyệt";

            var title = isEnglish ? "Profile approved" : "Hồ sơ đã được duyệt";
            var intro = isEnglish
                ? $"Congratulations {System.Net.WebUtility.HtmlEncode(recipientName)}, your {roleName} profile has been approved."
                : $"Chúc mừng {System.Net.WebUtility.HtmlEncode(recipientName)}, hồ sơ {roleName} của bạn đã được duyệt.";
            var deadline = isEnglish
                ? $"Please sign contract #{System.Net.WebUtility.HtmlEncode(contractNo)} before {expiresAt.ToLocalTime():HH:mm dd/MM/yyyy}."
                : $"Vui lòng ký hợp đồng #{System.Net.WebUtility.HtmlEncode(contractNo)} trước {expiresAt.ToLocalTime():HH:mm dd/MM/yyyy}.";
            var button = isEnglish ? "Open contract" : "Mở hợp đồng";

            var body = BuildContractNoticeEmailHtml(title, intro, deadline, contractUrl, button);
            await SendEmailAsync(smtpSettings["Host"], smtpSettings["Port"], smtpSettings["User"], smtpSettings["Pass"], toEmail, subject, body);
        }

        public async Task SendContractReminderEmailAsync(string toEmail, string recipientName, string roleName, string contractNo, DateTime expiresAt, string contractUrl, int daysBeforeDeadline, string culture = "vi")
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var isEnglish = string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase)
                || culture.StartsWith("en-", StringComparison.OrdinalIgnoreCase);

            var subject = isEnglish
                ? $"[CarRental] Contract signing reminder: {daysBeforeDeadline} day(s) left"
                : $"[CarRental] Nhắc ký hợp đồng: còn {daysBeforeDeadline} ngày";

            var title = isEnglish ? "Contract signing reminder" : "Nhắc ký hợp đồng";
            var intro = isEnglish
                ? $"Hi {System.Net.WebUtility.HtmlEncode(recipientName)}, your {roleName} contract is still waiting for your signature."
                : $"Chào {System.Net.WebUtility.HtmlEncode(recipientName)}, hợp đồng {roleName} của bạn vẫn đang chờ ký.";
            var deadline = isEnglish
                ? $"Contract #{System.Net.WebUtility.HtmlEncode(contractNo)} expires at {expiresAt.ToLocalTime():HH:mm dd/MM/yyyy}."
                : $"Hợp đồng #{System.Net.WebUtility.HtmlEncode(contractNo)} hết hạn lúc {expiresAt.ToLocalTime():HH:mm dd/MM/yyyy}.";
            var button = isEnglish ? "Sign contract now" : "Ký hợp đồng ngay";

            var body = BuildContractNoticeEmailHtml(title, intro, deadline, contractUrl, button);
            await SendEmailAsync(smtpSettings["Host"], smtpSettings["Port"], smtpSettings["User"], smtpSettings["Pass"], toEmail, subject, body);
        }

        private static string BuildContractNoticeEmailHtml(string title, string intro, string deadline, string contractUrl, string buttonText)
        {
            var encodedUrl = System.Net.WebUtility.HtmlEncode(contractUrl);
            return $@"<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='UTF-8' /><meta name='viewport' content='width=device-width, initial-scale=1.0'/></head>
<body style='margin:0;padding:0;font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;padding:30px 10px;'>
    <tr><td align='center'>
      <table width='560' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 10px 30px rgba(15,23,42,.12);'>
        <tr><td style='background:linear-gradient(135deg,#0369a1,#0ea5e9);padding:30px 36px;text-align:center;color:#fff;'>
          <h1 style='margin:0;font-size:24px;'>CarRental</h1>
          <p style='margin:8px 0 0;color:rgba(255,255,255,.86);'>{System.Net.WebUtility.HtmlEncode(title)}</p>
        </td></tr>
        <tr><td style='padding:30px 36px;color:#1e293b;'>
          <p style='font-size:15px;line-height:1.6;margin:0 0 16px;'>{intro}</p>
          <div style='background:#eff6ff;border:1px solid #bfdbfe;border-radius:10px;padding:16px;color:#1e40af;font-weight:700;margin-bottom:22px;'>{deadline}</div>
          <p style='font-size:13px;color:#64748b;line-height:1.6;margin:0 0 24px;'>Please complete this step before the deadline so your account can keep using partner/driver features.</p>
          <p style='text-align:center;margin:0;'><a href='{encodedUrl}' style='display:inline-block;background:#2563eb;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:700;'>{System.Net.WebUtility.HtmlEncode(buttonText)}</a></p>
        </td></tr>
        <tr><td style='background:#0f172a;padding:20px 36px;text-align:center;color:#94a3b8;font-size:12px;'>CarRental - Smart car rental system</td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }

        private string BuildBookingEmailHtml(Booking booking, decimal depositAmount, decimal remainingAmount)
        {
            var user = booking.User;
            var car = booking.Car;
            var statusColor = "#0ea5e9";
            var statusBg = "#f0f9ff";

            return $@"<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <title>Hóa đơn đặt xe</title>
</head>
<body style='margin:0;padding:0;font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;padding:30px 10px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);'>

                    <!-- Header Banner -->
                    <tr>
                        <td style='background:linear-gradient(135deg,#0369a1 0%,#0ea5e9 50%,#38bdf8 100%);padding:32px 40px;text-align:center;'>
                            <table cellpadding='0' cellspacing='0' align='center'>
                                <tr>
                                    <td style='padding-bottom:12px;'>
                                        <img src='https://img.icons8.com/color/48/car--v1.png' width='48' height='48' alt='CarRental' style='display:block;' />
                                    </td>
                                </tr>
                            </table>
                            <h1 style='color:#ffffff;margin:0;font-size:24px;font-weight:700;letter-spacing:0.5px;'>CarRental</h1>
                            <p style='color:rgba(255,255,255,0.85);margin:8px 0 0;font-size:14px;'>Xác nhận đặt xe & Thanh toán thành công</p>
                        </td>
                    </tr>

                    <!-- Status Badge -->
                    <tr>
                        <td style='padding:24px 40px 0;text-align:center;'>
                            <span style='display:inline-block;background:{statusBg};color:{statusColor};padding:8px 24px;border-radius:50px;font-size:13px;font-weight:700;letter-spacing:1px;text-transform:uppercase;border:1px solid rgba(14,165,233,0.2);'>
                                &#10003; Thanh toán thành công
                            </span>
                        </td>
                    </tr>

                    <!-- Greeting -->
                    <tr>
                        <td style='padding:24px 40px 0;'>
                            <p style='margin:0 0 4px;font-size:15px;color:#1e293b;'>Kính gửi <strong>{user.Name}</strong>,</p>
                            <p style='margin:0;font-size:14px;color:#64748b;'>Cảm ơn bạn đã đặt xe tại CarRental. Dưới đây là chi tiết đơn đặt xe của bạn.</p>
                        </td>
                    </tr>

                    <!-- Booking Info Card -->
                    <tr>
                        <td style='padding:20px 40px;'>
                            <table width='100%' cellpadding='0' cellspacing='0' style='background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;overflow:hidden;'>
                                <tr>
                                    <td colspan='2' style='padding:14px 20px;background:#0ea5e9;color:#fff;font-size:13px;font-weight:700;letter-spacing:1px;text-transform:uppercase;'>
                                        Thông tin đặt xe - #{booking.BookingID:D4}
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:12px 20px;border-bottom:1px solid #e2e8f0;width:45%;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Xe</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{car?.CarName ?? "N/A"}</div>
                                        <div style='font-size:12px;color:#64748b;'>{car?.LicensePlate ?? ""}</div>
                                    </td>
                                    <td style='padding:12px 20px;border-bottom:1px solid #e2e8f0;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Bảo hiểm</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.InsuranceTier}</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:12px 20px;border-bottom:1px solid #e2e8f0;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Nhận xe</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.StartDate:HH:mm, dd/MM/yyyy}</div>
                                    </td>
                                    <td style='padding:12px 20px;border-bottom:1px solid #e2e8f0;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Trả xe</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.EndDate:HH:mm, dd/MM/yyyy}</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:12px 20px;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Địa điểm nhận</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.PickupLocation?.Name ?? "N/A"}</div>
                                    </td>
                                    <td style='padding:12px 20px;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Địa điểm trả</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.ReturnLocation?.Name ?? "N/A"}</div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Price Breakdown -->
                    <tr>
                        <td style='padding:0 40px 20px;'>
                            <table width='100%' cellpadding='0' cellspacing='0' style='background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;overflow:hidden;'>
                                <tr>
                                    <td colspan='2' style='padding:14px 20px;background:#0ea5e9;color:#fff;font-size:13px;font-weight:700;letter-spacing:1px;text-transform:uppercase;'>
                                        Chi tiết thanh toán
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:10px 20px;font-size:13px;color:#475569;border-bottom:1px solid #e2e8f0;'>Tổng tiền thuê xe</td>
                                    <td style='padding:10px 20px;font-size:13px;color:#1e293b;font-weight:600;text-align:right;border-bottom:1px solid #e2e8f0;'>{booking.TotalPrice:N0} VND</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px 20px;font-size:13px;color:#475569;border-bottom:1px solid #e2e8f0;'>Tổng số giờ thuê</td>
                                    <td style='padding:10px 20px;font-size:13px;color:#1e293b;font-weight:600;text-align:right;border-bottom:1px solid #e2e8f0;'>{booking.TotalHours} giờ</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px 20px;font-size:13px;color:#475569;border-bottom:1px solid #e2e8f0;'>Tiền đặt cọc (20%)</td>
                                    <td style='padding:10px 20px;font-size:13px;color:#16a34a;font-weight:700;text-align:right;border-bottom:1px solid #e2e8f0;'>-{depositAmount:N0} VND</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px 20px;font-size:13px;color:#475569;'>Còn lại (thanh toán khi nhận xe)</td>
                                    <td style='padding:10px 20px;font-size:13px;color:#1e293b;font-weight:600;text-align:right;'>{remainingAmount:N0} VND</td>
                                </tr>
                                <tr>
                                    <td colspan='2' style='padding:14px 20px;background:#e0f2fe;border-top:2px solid #0ea5e9;'>
                                        <table width='100%' cellpadding='0' cellspacing='0'>
                                            <tr>
                                                <td style='font-size:15px;color:#0369a1;font-weight:700;'>Số dư tổng thanh toán</td>
                                                <td style='font-size:18px;color:#0369a1;font-weight:800;text-align:right;'>{booking.TotalPrice:N0} VND</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Notice -->
                    <tr>
                        <td style='padding:0 40px 20px;'>
                            <div style='background:#fffbeb;border:1px solid #fcd34d;border-radius:8px;padding:14px 18px;'>
                                <p style='margin:0 0 6px;font-size:13px;color:#92400e;font-weight:700;'>
                                    <span style='font-size:16px;margin-right:6px;'>&#9888;</span>Lưu ý quan trọng
                                </p>
                                <ul style='margin:0;padding-left:18px;font-size:12px;color:#78350f;line-height:1.7;'>
                                    <li>Vui lòng có thẻ CCCD và bằng lái xe khi nhận xe.</li>
                                    <li>Đặt cọc sẽ được hoàn trả trong 15 ngày sau khi trả xe (nếu không có vi phạm).</li>
                                    <li>Liên hệ 1900 1234 nếu cần hỗ trợ.</li>
                                </ul>
                            </div>
                        </td>
                    </tr>

                    <!-- CTA Button -->
                    <tr>
                        <td style='padding:0 40px 24px;text-align:center;'>
                            <a href='https://localhost:7285/Booking/MyBookings' style='display:inline-block;background:linear-gradient(135deg,#0369a1,#0ea5e9);color:#fff;text-decoration:none;padding:12px 32px;border-radius:8px;font-size:14px;font-weight:700;letter-spacing:0.5px;'>
                                Xem danh sách chuyến xe của tôi
                            </a>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style='background:#0f172a;padding:24px 40px;text-align:center;'>
                            <p style='margin:0 0 6px;font-size:14px;color:#94a3b8;font-weight:600;'>CarRental - Hệ thống thuê xe thông minh</p>
                            <p style='margin:0;font-size:12px;color:#64748b;'>1900 1234 | support@carrental.vn | www.carrental.vn</p>
                            <p style='margin:12px 0 0;font-size:11px;color:#475569;'>Email này được gửi tự động. Vui lòng không trả lời email này.</p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private async Task SendEmailAsync(string host, string portStr, string user, string pass, string toEmail, string subject, string body)
        {
            var throwOnFailure = _configuration.GetValue("Email:ThrowOnFailure", false);
            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(pass))
            {
                var exception = new InvalidOperationException("SMTP settings are not configured.");
                _logger.LogError(exception, "SMTP settings are not configured. Email to {Email} with subject {Subject} was not sent.", toEmail, subject);
                if (throwOnFailure)
                    throw exception;

                return;
            }

            try
            {
                int port = int.TryParse(portStr, out int p) ? p : 587;
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(user, pass),
                    Timeout = 30000 // 30 seconds timeout
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(user, "CarRental System"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);
                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} with subject {Subject}", toEmail, subject);
                if (throwOnFailure)
                    throw;
            }
        }
    }
}
