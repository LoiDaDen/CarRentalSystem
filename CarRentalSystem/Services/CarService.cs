using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Services
{
    public class CarService : ICarService
    {
        private readonly IRepository<Car> _carRepository;
        private readonly IRepository<Review> _reviewRepository;
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cache;

        // Cache Keys — tập trung quản lý để tránh typo
        private const string CacheKeyAllCars = "cars:all";
        private const string CacheKeyAvailableCars = "cars:available";

        public CarService(
            IRepository<Car> carRepository,
            IRepository<Review> reviewRepository,
            ApplicationDbContext context,
            ICacheService cache)
        {
            _carRepository = carRepository;
            _reviewRepository = reviewRepository;
            _context = context;
            _cache = cache;
        }

        public async Task<IEnumerable<Car>> GetAllCarsAsync()
        {
            // 1. Thử lấy từ Cache trước
            List<Car>? cached = null;
            if (cached != null) return cached;

            // 2. Cache miss → query DB
            var cars = await _carRepository.Query()
                .AsNoTracking()
                .Include(c => c.Location)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .Include(c => c.CarBrand) // Added CarBrand
                .ToListAsync();

            // 3. Lưu vào Cache (TTL 5 phút)
            // Do not cache EF entity graphs here; it can serialize a very large object graph.
            return cars;
        }

        public async Task<IEnumerable<Car>> GetAvailableCarsAsync()
        {
            // 1. Thử lấy từ Cache trước
            List<Car>? cached = null;
            if (cached != null) return cached;

            // 2. Cache miss → query DB
            var cars = await _context.Cars
                .AsNoTracking()
                .Include(c => c.CarImages)
                .Include(c => c.CarStatus)
                .Include(c => c.CarType)
                .Include(c => c.Location)
                .Include(c => c.CarBrand)
                .Where(c => c.CarStatus!.Name == "Available")
                .ToListAsync();

            // 3. Lưu vào Cache (TTL 5 phút)
            // Do not cache EF entity graphs here; it can serialize a very large object graph.
            return cars;
        }

        public async Task<Car> GetCarByIdAsync(int id)
        {
            return await _context.Cars
                .Include(c => c.CarImages)
                .Include(c => c.CarStatus)
                .Include(c => c.CarType)
                .Include(c => c.Location)
                .Include(c => c.CarBrand)
                .FirstOrDefaultAsync(m => m.CarID == id);
        }

        public async Task AddCarAsync(Car car)
        {
            await _carRepository.AddAsync(car);
            await _carRepository.SaveChangesAsync();
            await InvalidateCarCache(); // Xóa cache khi thêm xe mới
        }

        public async Task UpdateCarAsync(Car car)
        {
            _carRepository.Update(car);
            await _carRepository.SaveChangesAsync();
            await InvalidateCarCache(); // Xóa cache khi cập nhật xe
        }

        public async Task DeleteCarAsync(int id)
        {
            var car = await _context.Cars.FirstOrDefaultAsync(c => c.CarID == id);
            if (car != null)
            {
                car.IsDeleted = true;
                car.DeletedAt = DateTime.UtcNow;
                _context.Cars.Update(car);
                await _context.SaveChangesAsync();
                await InvalidateCarCache(); // Xóa cache khi xóa xe
            }
        }

        public async Task AddReviewAsync(Review review)
        {
            await _reviewRepository.AddAsync(review);
            await _reviewRepository.SaveChangesAsync();
        }

        public async Task AddCarImageAsync(CarImage image)
        {
            await _context.CarImages.AddAsync(image);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Xóa toàn bộ cache liên quan đến danh sách xe.
        /// Gọi khi có thay đổi dữ liệu (Add/Update/Delete) để đảm bảo tính nhất quán.
        /// </summary>
        private async Task InvalidateCarCache()
        {
            await _cache.RemoveAsync(CacheKeyAllCars);
            await _cache.RemoveAsync(CacheKeyAvailableCars);
        }
    }
}
