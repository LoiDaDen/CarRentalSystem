using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CarRentalSystem.Services
{
    /// <summary>
    /// Interface cho dịch vụ Cache phân tán (Distributed Cache).
    /// Hỗ trợ Redis khi có, fallback sang MemoryCache khi không cài Redis.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>Lấy dữ liệu từ cache theo key. Trả về default(T) nếu không có.</summary>
        Task<T> GetAsync<T>(string key);

        /// <summary>Lưu dữ liệu vào cache với thời gian sống (TTL).</summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>Xóa cache theo key (dùng khi dữ liệu thay đổi).</summary>
        Task RemoveAsync(string key);
    }

    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<CacheService> _logger;
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

        // JSON options cho serialize/deserialize - tối ưu hiệu năng
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T> GetAsync<T>(string key)
        {
            try
            {
                var cachedData = await _cache.GetStringAsync(key);
                if (cachedData == null)
                {
                    _logger.LogDebug("Cache MISS for key: {Key}", key);
                    return default;
                }

                _logger.LogDebug("Cache HIT for key: {Key}", key);
                return JsonSerializer.Deserialize<T>(cachedData, _jsonOptions);
            }
            catch (Exception ex)
            {
                // Nếu Redis bị sập, không làm crash app — trả về default
                _logger.LogWarning(ex, "Cache GET failed for key: {Key}. Falling back to database.", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
                };

                var serializedData = JsonSerializer.Serialize(value, _jsonOptions);
                await _cache.SetStringAsync(key, serializedData, options);
                _logger.LogDebug("Cache SET for key: {Key}, TTL: {TTL}", key, expiration ?? DefaultExpiration);
            }
            catch (Exception ex)
            {
                // Nếu Redis bị sập, log warning nhưng KHÔNG throw — app vẫn hoạt động bình thường
                _logger.LogWarning(ex, "Cache SET failed for key: {Key}. Data will be served from database.", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _cache.RemoveAsync(key);
                _logger.LogDebug("Cache REMOVED for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache REMOVE failed for key: {Key}", key);
            }
        }
    }
}
