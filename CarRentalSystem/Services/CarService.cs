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

        public CarService(IRepository<Car> carRepository, IRepository<Review> reviewRepository, ApplicationDbContext context)
        {
            _carRepository = carRepository;
            _reviewRepository = reviewRepository;
            _context = context;
        }

        public async Task<IEnumerable<Car>> GetAllCarsAsync()
        {
            return await _carRepository.Query()
                .Include(c => c.Location)
                .Include(c => c.CarType)
                .Include(c => c.CarStatus)
                .Include(c => c.CarImages)
                .Include(c => c.CarBrand) // Added CarBrand
                .ToListAsync();
        }

        public async Task<IEnumerable<Car>> GetAvailableCarsAsync()
        {
            return await _context.Cars
                .Include(c => c.CarImages)
                .Include(c => c.CarStatus)
                .Include(c => c.CarType)
                .Include(c => c.Location)
                .Include(c => c.CarBrand)
                .Where(c => c.CarStatus!.Name == "Available")
                .ToListAsync();
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
        }

        public async Task UpdateCarAsync(Car car)
        {
            _carRepository.Update(car);
            await _carRepository.SaveChangesAsync();
        }

        public async Task DeleteCarAsync(int id)
        {
            var car = await _carRepository.GetByIdAsync(id);
            if (car != null)
            {
                _carRepository.Delete(car);
                await _carRepository.SaveChangesAsync();
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
    }
}
