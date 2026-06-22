using CarRentalSystem.Models;

namespace CarRentalSystem.Services;

public sealed class LocalEmailSinkService : IEmailService
{
    private readonly ILogger<LocalEmailSinkService> _logger;

    public LocalEmailSinkService(ILogger<LocalEmailSinkService> logger)
    {
        _logger = logger;
    }

    public Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        => RecordAsync("verification");

    public Task SendOtpEmailAsync(string toEmail, string otp)
        => RecordAsync("OTP");

    public Task SendPasswordResetOtpEmailAsync(string toEmail, string otp)
        => RecordAsync("password-reset OTP");

    public Task SendBookingConfirmationEmailAsync(Booking booking, decimal paidAmount)
        => RecordAsync("booking confirmation");

    public Task SendApprovalEmailAsync(
        string toEmail,
        string recipientName,
        string roleName,
        string contractNo,
        DateTime expiresAt,
        string contractUrl,
        string culture = "vi")
        => RecordAsync("approval");

    public Task SendContractReminderEmailAsync(
        string toEmail,
        string recipientName,
        string roleName,
        string contractNo,
        DateTime expiresAt,
        string contractUrl,
        int daysBeforeDeadline,
        string culture = "vi")
        => RecordAsync("contract reminder");

    private Task RecordAsync(string category)
    {
        _logger.LogInformation("Local email sink accepted a {Category} message without contacting SMTP.", category);
        return Task.CompletedTask;
    }
}
