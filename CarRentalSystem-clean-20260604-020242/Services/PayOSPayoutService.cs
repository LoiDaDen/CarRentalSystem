using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CarRentalSystem.Services
{
    public record PayOSPayoutCreateCommand(
        string ReferenceId,
        decimal Amount,
        string Description,
        string RecipientBin,
        string RecipientAccountNumber,
        string? RecipientAccountName,
        string IdempotencyKey);

    public record PayOSPayoutProviderResult(
        bool IsSuccess,
        bool IsEnabled,
        bool IsPermissionMissing,
        string? PayoutId,
        string? ApprovalState,
        string? TransactionState,
        string? ErrorMessage,
        string? RawResponse);

    public interface IPayOSPayoutService
    {
        bool IsEnabled { get; }
        Task<PayOSPayoutProviderResult> CreatePayoutAsync(PayOSPayoutCreateCommand command);
        Task<PayOSPayoutProviderResult> GetPayoutAsync(string payoutId);
    }

    public class PayOSPayoutService : IPayOSPayoutService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayOSPayoutService> _logger;

        public PayOSPayoutService(HttpClient httpClient, IConfiguration configuration, ILogger<PayOSPayoutService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _httpClient.BaseAddress ??= new Uri("https://api-merchant.payos.vn");
        }

        public bool IsEnabled =>
            _configuration.GetValue<bool>("PayOS:PayoutEnabled") &&
            !string.IsNullOrWhiteSpace(_configuration["PayOS:ClientId"]) &&
            !string.IsNullOrWhiteSpace(_configuration["PayOS:ApiKey"]) &&
            !string.IsNullOrWhiteSpace(_configuration["PayOS:ChecksumKey"]);

        public async Task<PayOSPayoutProviderResult> CreatePayoutAsync(PayOSPayoutCreateCommand command)
        {
            if (!IsEnabled)
                return new PayOSPayoutProviderResult(false, false, false, null, null, null, "PayOS payout is disabled.", null);

            var amount = (long)Math.Round(command.Amount, 0, MidpointRounding.AwayFromZero);
            var body = new
            {
                referenceId = command.ReferenceId,
                amount,
                description = command.Description,
                toBin = command.RecipientBin,
                toAccountNumber = command.RecipientAccountNumber,
                toAccountName = command.RecipientAccountName,
                category = new[] { "refund" }
            };

            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/payouts")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request, json, command.IdempotencyKey);

            try
            {
                using var response = await _httpClient.SendAsync(request);
                var raw = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return FailureFromResponse(response.StatusCode, raw);

                return ParsePayoutResponse(raw);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create PayOS payout for reference {ReferenceId}.", command.ReferenceId);
                return new PayOSPayoutProviderResult(false, true, false, null, null, null, ex.Message, null);
            }
        }

        public async Task<PayOSPayoutProviderResult> GetPayoutAsync(string payoutId)
        {
            if (!IsEnabled)
                return new PayOSPayoutProviderResult(false, false, false, payoutId, null, null, "PayOS payout is disabled.", null);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/payouts/{Uri.EscapeDataString(payoutId)}");
            AddAuthHeaders(request, payoutId, null);

            try
            {
                using var response = await _httpClient.SendAsync(request);
                var raw = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return FailureFromResponse(response.StatusCode, raw, payoutId);

                return ParsePayoutResponse(raw);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to poll PayOS payout {PayoutId}.", payoutId);
                return new PayOSPayoutProviderResult(false, true, false, payoutId, null, null, ex.Message, null);
            }
        }

        private void AddAuthHeaders(HttpRequestMessage request, string signaturePayload, string? idempotencyKey)
        {
            request.Headers.TryAddWithoutValidation("x-client-id", _configuration["PayOS:ClientId"]);
            request.Headers.TryAddWithoutValidation("x-api-key", _configuration["PayOS:ApiKey"]);
            var partnerCode = _configuration["PayOS:PartnerCode"];
            if (!string.IsNullOrWhiteSpace(partnerCode))
                request.Headers.TryAddWithoutValidation("x-partner-code", partnerCode);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
                request.Headers.TryAddWithoutValidation("x-idempotency-key", idempotencyKey);
            request.Headers.TryAddWithoutValidation("x-signature", Sign(signaturePayload));
        }

        private string Sign(string payload)
        {
            var checksumKey = _configuration["PayOS:ChecksumKey"] ?? string.Empty;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }

        private static PayOSPayoutProviderResult FailureFromResponse(HttpStatusCode statusCode, string raw, string? payoutId = null)
        {
            var permissionMissing = statusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
            return new PayOSPayoutProviderResult(false, true, permissionMissing, payoutId, null, null, raw, raw);
        }

        private static PayOSPayoutProviderResult ParsePayoutResponse(string raw)
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
            var payoutId = GetString(data, "id");
            var approvalState = GetString(data, "approvalState");
            string? transactionState = null;
            if (data.TryGetProperty("transactions", out var transactions))
            {
                foreach (var item in transactions.EnumerateObject())
                {
                    transactionState = GetString(item.Value, "state");
                    break;
                }
            }

            return new PayOSPayoutProviderResult(true, true, false, payoutId, approvalState, transactionState, null, raw);
        }

        private static string? GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) ? value.ToString() : null;
        }
    }
}
