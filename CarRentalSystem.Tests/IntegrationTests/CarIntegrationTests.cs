using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.IntegrationTests
{
    /// <summary>
    /// Integration tests cho CarService — CRUD workflow đầy đủ với InMemory Database
    /// </summary>
    public class CarIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly CarService _carService;

        public CarIntegrationTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();

            var carRepo = new Repository<Car>(_context);
            var reviewRepo = new Repository<Review>(_context);
            _carService = new CarService(carRepo, reviewRepo, _context, new Helpers.NullCacheService());
        }

        // =============================================
        // FULL PROPERTIES TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Integration")]
        public async Task AddAndRetrieveCar_FullProperties()
        {
            // Arrange
            var newCar = new Car
            {
                CarName = "VinFast VF8",
                BrandID = 1,
                LicensePlate = "51G-88888",
                VIN = "VINVF8TEST001",
                PricePerDay = 2000000,
                PricePerHour = 200000,
                CarStatusID = 1,
                LocationID = 1,
                CarTypeID = 2, // SUV
                Seats = 5,
                FuelType = "Điện",
                Transmission = "Tự động",
                IsElectric = true,
                BatteryCapacity = 87,
                RangePerCharge = 400,
                IsPremium = true,
                DefaultDeposit = 20000000,
                Features = "Bluetooth,GPS,Camera 360,AutoPilot"
            };

            // Act
            await _carService.AddCarAsync(newCar);
            var retrieved = await _carService.GetCarByIdAsync(newCar.CarID);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved!.CarName.Should().Be("VinFast VF8");
            retrieved.IsElectric.Should().BeTrue("vì xe điện");
            retrieved.BatteryCapacity.Should().Be(87);
            retrieved.IsPremium.Should().BeTrue();
            retrieved.FuelType.Should().Be("Điện");
            retrieved.Features.Should().Contain("AutoPilot");
        }

        // =============================================
        // CAR IMAGE TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Integration")]
        public async Task AddCarImage_PersistsToDatabase()
        {
            // Arrange
            var image = new CarImage
            {
                CarID = 1,
                ImageUrl = "/uploads/cars/camry-front.jpg"
            };

            // Act
            await _carService.AddCarImageAsync(image);

            // Assert
            var images = await _context.CarImages.Where(i => i.CarID == 1).ToListAsync();
            var initialImageCount = 0; // seed data does not add images
            images.Should().HaveCountGreaterThan(initialImageCount, "vì ảnh mới phải được lưu");
            images.Last().ImageUrl.Should().Be("/uploads/cars/camry-front.jpg");
        }

        // =============================================
        // FILTER TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Integration")]
        public async Task FilterCarsByLocation_ReturnsCorrectCars()
        {
            // Arrange — Thêm xe ở Hà Nội (LocationID=2)
            var hanoiCar = new Car
            {
                CarName = "Mazda CX-5 HN",
                BrandID = 1,
                LicensePlate = "30A-11111",
                VIN = "VINHN001",
                PricePerDay = 1200000,
                PricePerHour = 120000,
                CarStatusID = 1,
                LocationID = 2, // Hà Nội
                CarTypeID = 2,
                Seats = 5
            };
            await _carService.AddCarAsync(hanoiCar);

            // Act — Lấy tất cả xe, filter theo LocationID
            var allCars = await _carService.GetAllCarsAsync();
            var hcmCars = allCars.Where(c => c.LocationID == 1).ToList();
            var hnCars = allCars.Where(c => c.LocationID == 2).ToList();

            // Assert — dùng AtLeast để tránh brittle assertion khi test chạy sau test khác trong class
            hcmCars.Should().HaveCountGreaterThan(0, "vì phải có xe ở HCM (LocationID=1)");
            hnCars.Should().HaveCountGreaterThan(0, "vì phải có xe mới thêm ở HN (LocationID=2)");
            hnCars.Any(c => c.CarName == "Mazda CX-5 HN").Should().BeTrue();
        }

        // =============================================
        // CAR STATUS TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Integration")]
        public async Task UpdateCarStatus_Available_ToRented()
        {
            // Arrange
            var car = await _context.Cars.FindAsync(1);
            car.Should().NotBeNull();
            car!.CarStatusID.Should().Be(1, "ban đầu xe phải ở trạng thái Available");

            // Act
            car.CarStatusID = 2; // Rented
            await _carService.UpdateCarAsync(car);

            // Assert
            var updatedCar = await _context.Cars.FindAsync(1);
            updatedCar!.CarStatusID.Should().Be(2, "vì xe đã được chuyển sang trạng thái Rented");
        }

        // =============================================
        // DELETE WITH RELATED DATA TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Integration")]
        public async Task DeleteCar_ExistingCar_RemovesSuccessfully()
        {
            // Arrange — Thêm xe mới để xóa (tránh ảnh hưởng FK)
            var tempCar = new Car
            {
                CarName = "Xe Tam Thoi",
                BrandID = 1,
                LicensePlate = "51Z-99999",
                VIN = "VINTEMP001",
                PricePerDay = 500000,
                PricePerHour = 50000,
                CarStatusID = 1,
                LocationID = 1,
                CarTypeID = 1,
                Seats = 4
            };
            await _carService.AddCarAsync(tempCar);
            var carId = tempCar.CarID;

            // Thêm ảnh cho xe này
            await _carService.AddCarImageAsync(new CarImage
            {
                CarID = carId,
                ImageUrl = "/uploads/temp.jpg"
            });

            var initialCarCount = await _context.Cars.CountAsync();

            // Act
            await _carService.DeleteCarAsync(carId);

            // Assert
            var count = await _context.Cars.CountAsync();
            count.Should().Be(initialCarCount - 1, "vì xe đã bị xóa");
            var deleted = await _context.Cars.FindAsync(carId);
            deleted.Should().BeNull("vì xe đã bị xóa khỏi database");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
