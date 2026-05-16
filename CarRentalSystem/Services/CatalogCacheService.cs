using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ICatalogCacheService
    {
        Task<IReadOnlyList<CatalogCarModel>> GetCarModelsAsync(int brandId);
        Task InvalidateAsync();
    }

    public record CatalogCarModel(int Id, string Name, int? SeatCount, string? FuelType, string? Transmission);

    public class CatalogCacheService : ICatalogCacheService
    {
        private const string Prefix = "catalog:v1";
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cache;
        private readonly IConfiguration _configuration;

        public CatalogCacheService(ApplicationDbContext context, ICacheService cache, IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _configuration = configuration;
        }

        public async Task<IReadOnlyList<CatalogCarModel>> GetCarModelsAsync(int brandId)
        {
            var key = $"{Prefix}:car-models:{brandId}";
            var cached = await _cache.GetAsync<List<CatalogCarModel>>(key);
            if (cached != null)
                return cached;

            var models = await _context.CarModels
                .AsNoTracking()
                .Where(m => m.BrandID == brandId && m.IsActive)
                .OrderBy(m => m.Name)
                .Select(m => new CatalogCarModel(m.CarModelID, m.Name, m.SeatCount, m.FuelType, m.Transmission))
                .ToListAsync();

            await _cache.SetAsync(key, models, TimeSpan.FromMinutes(_configuration.GetValue("Cache:CatalogTtlMinutes", 15)));
            return models;
        }

        public async Task InvalidateAsync()
        {
            // Distributed cache has no portable prefix delete; keep this method as the single invalidation hook.
            await Task.CompletedTask;
        }
    }
}
