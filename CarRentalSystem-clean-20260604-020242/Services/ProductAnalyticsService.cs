namespace CarRentalSystem.Services
{
    public interface IProductAnalyticsService
    {
        Task TrackAsync(string eventName, IReadOnlyDictionary<string, object?> properties, CancellationToken cancellationToken = default);
        bool ContainsSensitiveProperty(string key);
    }

    public class NoopProductAnalyticsService : IProductAnalyticsService
    {
        private static readonly HashSet<string> AllowedEvents = new(StringComparer.OrdinalIgnoreCase)
        {
            "booking_hold_created",
            "checkout_started",
            "payment_webhook_processed",
            "ekyc_submitted",
            "settlement_review_created",
            "dispute_export_created"
        };

        private static readonly string[] SensitiveKeyFragments =
        {
            "email", "phone", "citizen", "license", "password", "token", "secret", "payload", "pin", "address"
        };

        private readonly ILogger<NoopProductAnalyticsService> _logger;

        public NoopProductAnalyticsService(ILogger<NoopProductAnalyticsService> logger)
        {
            _logger = logger;
        }

        public Task TrackAsync(string eventName, IReadOnlyDictionary<string, object?> properties, CancellationToken cancellationToken = default)
        {
            if (!AllowedEvents.Contains(eventName))
            {
                _logger.LogDebug("Analytics event {EventName} ignored because it is not in the Phase 3 taxonomy.", eventName);
                return Task.CompletedTask;
            }

            var safeProperties = properties
                .Where(p => !ContainsSensitiveProperty(p.Key))
                .ToDictionary(p => p.Key, p => p.Value);

            _logger.LogDebug("Analytics noop event {EventName} with {PropertyCount} safe properties.", eventName, safeProperties.Count);
            return Task.CompletedTask;
        }

        public bool ContainsSensitiveProperty(string key)
        {
            return SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}
