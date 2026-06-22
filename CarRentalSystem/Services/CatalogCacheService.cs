using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ICatalogCacheService
    {
        Task<IReadOnlyList<CatalogCarModel>> GetCarModelsAsync(int brandId);
        Task<IReadOnlyList<CatalogLocation>> GetLocationsAsync();
        Task<IReadOnlyList<CatalogBrand>> GetBrandsAsync();
        Task<IReadOnlyList<CatalogCarType>> GetCarTypesAsync();
        Task InvalidateAsync();
    }

    public record CatalogCarModel(int Id, string Name, int? SeatCount, string? FuelType, string? Transmission, int? FromYear, int? ToYear);
    public sealed class CatalogLocation
    {
        public int LocationID { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public sealed class CatalogBrand
    {
        public int BrandID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }

    public sealed class CatalogCarType
    {
        public int CarTypeID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CatalogCacheService : ICatalogCacheService
    {
        private const string Prefix = "catalog:v2";
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
                .Select(m => new CatalogCarModel(m.CarModelID, m.Name, m.SeatCount, m.FuelType, m.Transmission, m.FromYear, m.ToYear))
                .ToListAsync();

            await _cache.SetAsync(key, models, TimeSpan.FromMinutes(_configuration.GetValue("Cache:CatalogTtlMinutes", 15)));
            return models;
        }

        public async Task<IReadOnlyList<CatalogLocation>> GetLocationsAsync()
        {
            var key = $"{Prefix}:locations";
            var cached = await _cache.GetAsync<List<CatalogLocation>>(key);
            if (cached != null)
                return cached;

            var locations = await _context.Locations
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .Select(l => new CatalogLocation
                {
                    LocationID = l.LocationID,
                    Name = l.Name,
                    Latitude = l.Latitude,
                    Longitude = l.Longitude
                })
                .ToListAsync();

            await _cache.SetAsync(key, locations, TimeSpan.FromMinutes(_configuration.GetValue("Cache:CatalogTtlMinutes", 15)));
            return locations;
        }

        public async Task<IReadOnlyList<CatalogBrand>> GetBrandsAsync()
        {
            var key = $"{Prefix}:brands";
            var cached = await _cache.GetAsync<List<CatalogBrand>>(key);
            if (cached != null)
                return cached;

            var brands = await _context.CarBrands
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new CatalogBrand
                {
                    BrandID = b.BrandID,
                    Name = b.Name,
                    LogoUrl = b.LogoUrl
                })
                .ToListAsync();

            await _cache.SetAsync(key, brands, TimeSpan.FromMinutes(_configuration.GetValue("Cache:CatalogTtlMinutes", 15)));
            return brands;
        }

        public async Task<IReadOnlyList<CatalogCarType>> GetCarTypesAsync()
        {
            var key = $"{Prefix}:car-types";
            var cached = await _cache.GetAsync<List<CatalogCarType>>(key);
            if (cached != null)
                return cached;

            var carTypes = await _context.CarTypes
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new CatalogCarType
                {
                    CarTypeID = t.CarTypeID,
                    Name = t.Name
                })
                .ToListAsync();

            await _cache.SetAsync(key, carTypes, TimeSpan.FromMinutes(_configuration.GetValue("Cache:CatalogTtlMinutes", 15)));
            return carTypes;
        }

        public async Task InvalidateAsync()
        {
            // Distributed cache has no portable prefix delete; keep this method as the single invalidation hook.
            await _cache.RemoveAsync($"{Prefix}:locations");
            await _cache.RemoveAsync($"{Prefix}:brands");
            await _cache.RemoveAsync($"{Prefix}:car-types");
            await Task.CompletedTask;
        }
    }
}
