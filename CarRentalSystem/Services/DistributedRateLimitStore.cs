using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace CarRentalSystem.Services
{
    public sealed record RateLimitCounterResult(bool IsLimited, int Count, bool StoreFailed);

    public interface IDistributedRateLimitStore
    {
        bool FailClosedOnStoreError { get; }
        Task<RateLimitCounterResult> HitAsync(string key, int maxAttempts, TimeSpan window, CancellationToken cancellationToken = default);
        Task<RateLimitCounterResult> PeekAsync(string key, int maxAttempts, TimeSpan window, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    }

    public sealed class DistributedRateLimitStore : IDistributedRateLimitStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly ConcurrentDictionary<string, RateLimitCounter> FallbackCounters = new();

        private readonly IDistributedCache _cache;
        private readonly ILogger<DistributedRateLimitStore> _logger;

        public DistributedRateLimitStore(
            IDistributedCache cache,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger<DistributedRateLimitStore> logger)
        {
            _cache = cache;
            _logger = logger;
            FailClosedOnStoreError =
                configuration.GetValue("Security:RequireDistributedRateLimit", false) ||
                configuration.GetValue("Redis:RequireRateLimit", false) ||
                (environment.IsProduction() && configuration.GetValue("PublicApi:RequireRateLimit", true));
        }

        public bool FailClosedOnStoreError { get; }

        public async Task<RateLimitCounterResult> HitAsync(string key, int maxAttempts, TimeSpan window, CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                var counter = await ReadCounterAsync(key, cancellationToken);
                if (counter == null || now - counter.WindowStartUtc > window)
                    counter = new RateLimitCounter(0, now);

                counter = counter with { Count = counter.Count + 1 };
                await WriteCounterAsync(key, counter, window, cancellationToken);
                return new RateLimitCounterResult(counter.Count > maxAttempts, counter.Count, false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Distributed rate-limit HIT failed for key {Key}.", key);
                if (FailClosedOnStoreError)
                    return new RateLimitCounterResult(true, maxAttempts + 1, true);

                return HitFallback(key, maxAttempts, window);
            }
        }

        public async Task<RateLimitCounterResult> PeekAsync(string key, int maxAttempts, TimeSpan window, CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                var counter = await ReadCounterAsync(key, cancellationToken);
                if (counter == null || now - counter.WindowStartUtc > window)
                    return new RateLimitCounterResult(false, 0, false);

                return new RateLimitCounterResult(counter.Count >= maxAttempts, counter.Count, false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Distributed rate-limit PEEK failed for key {Key}.", key);
                if (FailClosedOnStoreError)
                    return new RateLimitCounterResult(true, maxAttempts, true);

                var fallback = FallbackCounters.TryGetValue(key, out var counter) && DateTime.UtcNow - counter.WindowStartUtc <= window
                    ? counter.Count
                    : 0;
                return new RateLimitCounterResult(fallback >= maxAttempts, fallback, true);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cache.RemoveAsync(CacheKey(key), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Distributed rate-limit REMOVE failed for key {Key}.", key);
            }

            FallbackCounters.TryRemove(key, out _);
        }

        private async Task<RateLimitCounter?> ReadCounterAsync(string key, CancellationToken cancellationToken)
        {
            var json = await _cache.GetStringAsync(CacheKey(key), cancellationToken);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<RateLimitCounter>(json, JsonOptions);
        }

        private async Task WriteCounterAsync(string key, RateLimitCounter counter, TimeSpan window, CancellationToken cancellationToken)
        {
            await _cache.SetStringAsync(
                CacheKey(key),
                JsonSerializer.Serialize(counter, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = window },
                cancellationToken);
        }

        private static RateLimitCounterResult HitFallback(string key, int maxAttempts, TimeSpan window)
        {
            var now = DateTime.UtcNow;
            var counter = FallbackCounters.AddOrUpdate(
                key,
                _ => new RateLimitCounter(1, now),
                (_, existing) => now - existing.WindowStartUtc > window
                    ? new RateLimitCounter(1, now)
                    : existing with { Count = existing.Count + 1 });

            return new RateLimitCounterResult(counter.Count > maxAttempts, counter.Count, true);
        }

        private static string CacheKey(string key) => $"rate-limit:{key}";

        private sealed record RateLimitCounter(int Count, DateTime WindowStartUtc);
    }
}
