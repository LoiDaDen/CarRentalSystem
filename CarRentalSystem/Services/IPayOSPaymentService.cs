using CarRentalSystem.Models;
using PayOS.Models.Webhooks;

namespace CarRentalSystem.Services
{
    public record PayOSCreatePaymentResult(
        bool IsSuccess,
        string? CheckoutUrl,
        string? ErrorMessage,
        PayOSTransaction? Transaction);

    public record PayOSWebhookResult(
        bool IsSuccess,
        bool IsPaid,
        string Message,
        int? BookingId,
        decimal? PaidAmount);

    public interface IPayOSPaymentService
    {
        bool IsConfigured { get; }
        Task<PayOSCreatePaymentResult> CreateDepositPaymentAsync(Booking booking, string returnUrl, string cancelUrl);
        Task<PayOSWebhookResult> HandleWebhookAsync(Webhook webhookData);
        Task MarkCancelledAsync(int bookingId);
    }
}
