using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace CarRentalSystem.Services
{
    public interface IPayOSGateway
    {
        bool IsConfigured { get; }
        Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(CreatePaymentLinkRequest request);
        Task<PaymentLink> GetPaymentLinkAsync(long orderCode);
        Task<WebhookData?> VerifyWebhookAsync(Webhook webhook);
    }

    public sealed class PayOSGateway : IPayOSGateway
    {
        private readonly PayOSClient? _client;

        public PayOSGateway(IConfiguration configuration)
        {
            var clientId = configuration["PayOS:ClientId"] ?? string.Empty;
            var apiKey = configuration["PayOS:ApiKey"] ?? string.Empty;
            var checksumKey = configuration["PayOS:ChecksumKey"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(apiKey) &&
                !string.IsNullOrWhiteSpace(checksumKey))
            {
                _client = new PayOSClient(new PayOSOptions
                {
                    ClientId = clientId,
                    ApiKey = apiKey,
                    ChecksumKey = checksumKey
                });
            }
        }

        public bool IsConfigured => _client != null;

        public Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(CreatePaymentLinkRequest request)
        {
            return Client.PaymentRequests.CreateAsync(request);
        }

        public Task<PaymentLink> GetPaymentLinkAsync(long orderCode)
        {
            return Client.PaymentRequests.GetAsync(orderCode);
        }

        public async Task<WebhookData?> VerifyWebhookAsync(Webhook webhook)
        {
            return await Client.Webhooks.VerifyAsync(webhook);
        }

        private PayOSClient Client => _client ?? throw new InvalidOperationException("PayOS is not configured.");
    }
}
