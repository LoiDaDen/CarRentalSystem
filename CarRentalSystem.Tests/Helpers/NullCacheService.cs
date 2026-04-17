using CarRentalSystem.Services;

namespace CarRentalSystem.Tests.Helpers
{
    /// <summary>
    /// Fake CacheService luôn trả null (cache miss) — dùng cho Unit/Integration tests.
    /// Đảm bảo tests luôn hit DB thay vì cache.
    /// </summary>
    public class NullCacheService : ICacheService
    {
        public Task<T> GetAsync<T>(string key) => Task.FromResult(default(T)!);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) => Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
    }
}
