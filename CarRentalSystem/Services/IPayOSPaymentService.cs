using CarRentalSystem.Models;
using CarRentalSystem.ViewModels;
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
        Task<PayOSCreatePaymentResult> CreateHoldPaymentAsync(BookingHold hold, CheckoutQuoteViewModel quote, string baseUrl);
        Task<PayOSCreatePaymentResult> CreateCartHoldPaymentAsync(BookingTransaction transaction, decimal holdingFee, string baseUrl);
        Task<PayOSWebhookResult> HandleWebhookAsync(Webhook webhookData);
        Task<PayOSWebhookResult> ReconcilePaymentAsync(long orderCode, string source);
        Task MarkCancelledAsync(int bookingId);
        Task MarkHoldCancelledAsync(long orderCode);
    }
}
