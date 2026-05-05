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
        Task SendBookingConfirmationEmailAsync(Booking booking, decimal paidAmount);
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
    <title>Hoa don dat xe</title>
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
                                &#10003; Thanh toan thanh cong
                            </span>
                        </td>
                    </tr>

                    <!-- Greeting -->
                    <tr>
                        <td style='padding:24px 40px 0;'>
                            <p style='margin:0 0 4px;font-size:15px;color:#1e293b;'>Kinh gui <strong>{user.Name}</strong>,</p>
                            <p style='margin:0;font-size:14px;color:#64748b;'>Cam on ban da dat xe tai CarRental. Dưới đây là chi tiết đơn đặt xe của ban.</p>
                        </td>
                    </tr>

                    <!-- Booking Info Card -->
                    <tr>
                        <td style='padding:20px 40px;'>
                            <table width='100%' cellpadding='0' cellspacing='0' style='background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;overflow:hidden;'>
                                <tr>
                                    <td colspan='2' style='padding:14px 20px;background:#0ea5e9;color:#fff;font-size:13px;font-weight:700;letter-spacing:1px;text-transform:uppercase;'>
                                        Thong tin dat xe - #{booking.BookingID:D4}
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:12px 20px;border-bottom:1px solid #e2e8f0;width:45%;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Xe</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{car?.CarName ?? "N/A"}</div>
                                        <div style='font-size:12px;color:#64748b;'>{car?.LicensePlate ?? ""}</div>
                                    </td>
                                    <td style='padding:12px 20px;border-bottom:1px solid #e2e8f0;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Bao hiem</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.InsuranceTier}</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:12px 20px;border-bottom:1px solid #e2e8f0;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Nhan xe</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.StartDate:HH:mm, dd/MM/yyyy}</div>
                                    </td>
                                    <td style='padding:12px 20px;border-bottom:1px solid #e2e8f0;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Tra xe</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.EndDate:HH:mm, dd/MM/yyyy}</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:12px 20px;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Dia diem nhan</div>
                                        <div style='font-size:14px;color:#1e293b;font-weight:600;'>{booking.PickupLocation?.Name ?? "N/A"}</div>
                                    </td>
                                    <td style='padding:12px 20px;vertical-align:top;'>
                                        <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:4px;'>Dia diem tra</div>
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
                                        Chi tiet thanh toan
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding:10px 20px;font-size:13px;color:#475569;border-bottom:1px solid #e2e8f0;'>Tong tien thue xe</td>
                                    <td style='padding:10px 20px;font-size:13px;color:#1e293b;font-weight:600;text-align:right;border-bottom:1px solid #e2e8f0;'>{booking.TotalPrice:N0} VND</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px 20px;font-size:13px;color:#475569;border-bottom:1px solid #e2e8f0;'>Tong so gio thue</td>
                                    <td style='padding:10px 20px;font-size:13px;color:#1e293b;font-weight:600;text-align:right;border-bottom:1px solid #e2e8f0;'>{booking.TotalHours} gio</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px 20px;font-size:13px;color:#475569;border-bottom:1px solid #e2e8f0;'>Tien dat coc (20%)</td>
                                    <td style='padding:10px 20px;font-size:13px;color:#16a34a;font-weight:700;text-align:right;border-bottom:1px solid #e2e8f0;'>-{depositAmount:N0} VND</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px 20px;font-size:13px;color:#475569;'>Con lai (thanh toan khi nhan xe)</td>
                                    <td style='padding:10px 20px;font-size:13px;color:#1e293b;font-weight:600;text-align:right;'>{remainingAmount:N0} VND</td>
                                </tr>
                                <tr>
                                    <td colspan='2' style='padding:14px 20px;background:#e0f2fe;border-top:2px solid #0ea5e9;'>
                                        <table width='100%' cellpadding='0' cellspacing='0'>
                                            <tr>
                                                <td style='font-size:15px;color:#0369a1;font-weight:700;'>So du tong thanh toan</td>
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
                                    <span style='font-size:16px;margin-right:6px;'>&#9888;</span>Luu y quan trong
                                </p>
                                <ul style='margin:0;padding-left:18px;font-size:12px;color:#78350f;line-height:1.7;'>
                                    <li>Vui long co the CCCD va bang lai xe khi nhan xe.</li>
                                    <li>Dat coc se duoc hoan tra trong 15 ngay sau khi tra xe (neu khong co vi pham).</li>
                                    <li>Lien he 1900 1234 neu can ho tro.</li>
                                </ul>
                            </div>
                        </td>
                    </tr>

                    <!-- CTA Button -->
                    <tr>
                        <td style='padding:0 40px 24px;text-align:center;'>
                            <a href='https://localhost:7285/Booking/MyBookings' style='display:inline-block;background:linear-gradient(135deg,#0369a1,#0ea5e9);color:#fff;text-decoration:none;padding:12px 32px;border-radius:8px;font-size:14px;font-weight:700;letter-spacing:0.5px;'>
                                Xem danh sach chuyen xe cua toi
                            </a>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style='background:#0f172a;padding:24px 40px;text-align:center;'>
                            <p style='margin:0 0 6px;font-size:14px;color:#94a3b8;font-weight:600;'>CarRental - He thong thue xe thong minh</p>
                            <p style='margin:0;font-size:12px;color:#64748b;'>1900 1234 | support@carrental.vn | www.carrental.vn</p>
                            <p style='margin:12px 0 0;font-size:11px;color:#475569;'>Email nay duoc gui tu dong. Vui long khong tra loi email nay.</p>
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
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user))
            {
                _logger.LogWarning("SMTP Settings not found. Logging email instead:");
                _logger.LogInformation("================ EMAIL ====================");
                _logger.LogInformation("To: " + toEmail);
                _logger.LogInformation("Subject: " + subject);
                _logger.LogInformation("============================================");
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
                _logger.LogError("Failed to send email to {Email}: {Error}", toEmail, ex.Message);
                _logger.LogInformation("Fallback: Email to {Email}, Subject: {Subject}", toEmail, subject);
            }
        }
    }
}

