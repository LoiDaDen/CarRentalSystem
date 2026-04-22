using CarRentalSystem.Models;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using CarRentalSystem.Services;

namespace CarRentalSystem.Tests.UnitTests
{
    /// <summary>
    /// Unit tests cho CarService — CRUD xe, lọc xe Available
    /// Dùng EF Core InMemory DB vì CarService phụ thuộc vào ApplicationDbContext
    /// </summary>
    public class CarServiceTests : IDisposable
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly CarService _carService;

        public CarServiceTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();

            var carRepo = new Repositories.Repository<Car>(_context);
            var reviewRepo = new Repositories.Repository<Review>(_context);
            _carService = new CarService(carRepo, reviewRepo, _context, new Helpers.NullCacheService());
        }

        // =============================================
        // GET CARS TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task GetAvailableCars_ReturnsOnlyCarsWithAvailableStatus()
        {
            // Arrange: Đánh dấu một xe là "Rented"
            var car = await _context.Cars.FirstAsync();
            car.CarStatusID = 2; // Rented
            await _context.SaveChangesAsync();

            // Act
            var result = await _carService.GetAvailableCarsAsync();

            // Assert
            result.Should().NotContain(c => c.CarStatusID == 2,
                "vì xe đang được thuê không được hiển thị trong danh sách xe có sẵn");
            result.Should().OnlyContain(c => c.CarStatus!.Name == "Available");
        }

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task GetAllCars_ReturnsAllCarsIncludingRented()
        {
            // Act
            var result = await _carService.GetAllCarsAsync();

            // Assert — dùng relative count thay vì absolute
            var seededCount = await _context.Cars.CountAsync();
            result.Should().HaveCount(seededCount, "vì kết quả phải chứa tất cả xe đã seed");
        }

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task GetCarById_ExistingId_ReturnsCar()
        {
            // Act
            var result = await _carService.GetCarByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.CarName.Should().Be("Camry 2024");
        }

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task GetCarById_NonExistentId_ReturnsNull()
        {
            // Act
            var result = await _carService.GetCarByIdAsync(9999);

            // Assert
            result.Should().BeNull("vì xe với ID=9999 không tồn tại");
        }

        // =============================================
        // ADD CAR TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task AddCar_NewCar_PersistsToDatabase()
        {
            // Arrange
            var initialCount = await _context.Cars.CountAsync();
            var newCar = new Car
            {
                CarName = "Xe Moi Test",
                BrandID = 1,
                LicensePlate = "51C-99999",
                VIN = "VINTEST001",
                PricePerDay = 800000,
                PricePerHour = 80000,
                CarStatusID = 1,
                LocationID = 1,
                CarTypeID = 1,
                Seats = 5
            };

            // Act
            await _carService.AddCarAsync(newCar);

            // Assert
            var count = await _context.Cars.CountAsync();
            count.Should().Be(initialCount + 1, "vì xe mới phải được lưu vào database");
        }

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task AddCar_NewCar_HasCorrectProperties()
        {
            // Arrange
            var newCar = new Car
            {
                CarName = "Honda Civic Test",
                BrandID = 2,
                LicensePlate = "51D-55555",
                VIN = "VINTEST002",
                PricePerDay = 900000,
                PricePerHour = 90000,
                CarStatusID = 1,
                LocationID = 2,
                CarTypeID = 1,
                Seats = 5
            };

            // Act
            await _carService.AddCarAsync(newCar);

            // Assert
            var savedCar = await _context.Cars.FirstOrDefaultAsync(c => c.LicensePlate == "51D-55555");
            savedCar.Should().NotBeNull();
            savedCar!.PricePerDay.Should().Be(900000);
            savedCar.CarName.Should().Be("Honda Civic Test");
        }

        // =============================================
        // UPDATE CAR TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task UpdateCar_ExistingCar_SavesChanges()
        {
            // Arrange
            var car = await _context.Cars.FirstAsync();
            var originalPrice = car.PricePerDay;
            car.PricePerDay = 2000000;

            // Act
            await _carService.UpdateCarAsync(car);

            // Assert
            var updatedCar = await _context.Cars.FindAsync(car.CarID);
            updatedCar!.PricePerDay.Should().Be(2000000,
                "vì giá thuê đã được cập nhật thành 2.000.000 VND/ngày");
        }

        // =============================================
        // DELETE CAR TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task DeleteCar_ExistingId_RemovesFromDatabase()
        {
            // Arrange
            var initialCount = await _context.Cars.CountAsync();

            // Act
            await _carService.DeleteCarAsync(1);

            // Assert
            // Clear the change tracker so CountAsync/FirstOrDefaultAsync hit the data source.
            // In a real SQL DB, FindAsync would not see deleted rows after SaveChanges,
            // but the InMemory provider caches deleted entities in the tracker.
            _context.ChangeTracker.Clear();
            var count = await _context.Cars.CountAsync();
            count.Should().Be(initialCount - 1, "vì xe với ID=1 đã bị xóa");

            // Reload from DB to verify the entity was actually removed (not just in change tracker)
            var deletedCar = await _context.Cars.FirstOrDefaultAsync(c => c.CarID == 1);
            deletedCar.Should().BeNull("vì xe đã bị xóa khỏi database");
        }

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task DeleteCar_NonExistentId_DoesNotThrow()
        {
            // Act
            var act = async () => await _carService.DeleteCarAsync(9999);

            // Assert
            await act.Should().NotThrowAsync("vì xóa xe không tồn tại phải được xử lý gracefully");
        }

        // =============================================
        // ADD REVIEW TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task AddReview_NewReview_PersistsToDatabase()
        {
            // Arrange
            var review = new Review
            {
                CarID = 1,
                UserID = 2,
                Rating = 5,
                Comment = "Xe rất tốt, sạch sẽ và tiết kiệm nhiên liệu!",
                CreatedAt = DateTime.Now
            };

            // Act
            await _carService.AddReviewAsync(review);

            // Assert
            var reviews = await _context.Reviews.ToListAsync();
            reviews.Should().ContainSingle("vì đây là review đầu tiên");
            reviews[0].Rating.Should().Be(5);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
