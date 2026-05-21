using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace CarRentalSystem.Tests.Helpers
{
    /// <summary>
    /// Factory tạo Mock IRepository hỗ trợ EF Core async operations (FirstOrDefaultAsync, ToListAsync...).
    /// Sử dụng TestAsyncEnumerable để giả lập IAsyncQueryProvider.
    /// </summary>
    public static class MockRepositoryHelper
    {
        /// <summary>
        /// Tạo Mock IRepository với List dữ liệu, hỗ trợ đầy đủ EF Core async LINQ.
        /// </summary>
        public static Mock<IRepository<T>> Create<T>(List<T> data) where T : class
        {
            var mock = new Mock<IRepository<T>>();

            // TestAsyncEnumerable hỗ trợ FirstOrDefaultAsync, AnyAsync, ToListAsync
            mock.Setup(r => r.Query()).Returns(new TestAsyncEnumerable<T>(data));
            mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data);
            mock.Setup(r => r.AddAsync(It.IsAny<T>()))
                .Callback<T>(entity => data.Add(entity))
                .Returns(Task.CompletedTask);
            mock.Setup(r => r.Update(It.IsAny<T>()));
            mock.Setup(r => r.Delete(It.IsAny<T>()))
                .Callback<T>(entity => data.Remove(entity));
            mock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            return mock;
        }

        /// <summary>
        /// Tạo ApplicationDbContext dùng InMemory database (dùng cho CarServiceTests).
        /// </summary>
        public static ApplicationDbContext CreateInMemoryDbContext(string dbName = "")
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: string.IsNullOrEmpty(dbName)
                    ? $"TestDb_{System.Guid.NewGuid()}"
                    : dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        /// <summary>
        /// Seed dữ liệu mẫu đầy đủ vào InMemory DB.
        /// </summary>
        public static ApplicationDbContext CreateSeededDbContext()
        {
            var context = CreateInMemoryDbContext();

            context.BookingStatuses.AddRange(
                new BookingStatus { BookingStatusID = 1, Name = "Draft" },
                new BookingStatus { BookingStatusID = 2, Name = "Pending" },
                new BookingStatus { BookingStatusID = 3, Name = "Confirmed" },
                new BookingStatus { BookingStatusID = 4, Name = "Cancelled" },
                new BookingStatus { BookingStatusID = 5, Name = "Active" },
                new BookingStatus { BookingStatusID = 6, Name = "Completed" },
                new BookingStatus { BookingStatusID = 7, Name = "Unpaid" },
                new BookingStatus { BookingStatusID = 8, Name = "Rejected" },
                new BookingStatus { BookingStatusID = 9, Name = "Expired" },
                new BookingStatus { BookingStatusID = 10, Name = "NEED_REVIEW" }
            );

            context.CarStatuses.AddRange(
                new CarStatus { CarStatusID = 1, Name = "Available" },
                new CarStatus { CarStatusID = 2, Name = "Rented" },
                new CarStatus { CarStatusID = 3, Name = "Maintenance" }
            );

            context.Locations.AddRange(
                new Location { LocationID = 1, Name = "Hồ Chí Minh", Latitude = 10.7769, Longitude = 106.7009 },
                new Location { LocationID = 2, Name = "Hà Nội", Latitude = 21.0285, Longitude = 105.8542 }
            );

            context.CarBrands.AddRange(
                new CarBrand { BrandID = 1, Name = "Toyota" },
                new CarBrand { BrandID = 2, Name = "Honda" }
            );

            context.CarTypes.AddRange(
                new CarType { CarTypeID = 1, Name = "Sedan" },
                new CarType { CarTypeID = 2, Name = "SUV" }
            );

            context.Cars.AddRange(
                new Car
                {
                    CarID = 1, CarName = "Camry 2024", BrandID = 1,
                    LicensePlate = "51A-12345", VIN = "VIN0001",
                    PricePerDay = 1000000, PricePerHour = 100000,
                    CarStatusID = 1, LocationID = 1, CarTypeID = 1,
                    Seats = 5, DefaultDeposit = 5000000
                },
                new Car
                {
                    CarID = 2, CarName = "CR-V 2024", BrandID = 2,
                    LicensePlate = "51B-67890", VIN = "VIN0002",
                    PricePerDay = 1500000, PricePerHour = 150000,
                    CarStatusID = 1, LocationID = 1, CarTypeID = 2,
                    Seats = 7, DefaultDeposit = 8000000
                }
            );

            context.Users.AddRange(
                new User
                {
                    UserID = 1, Name = "Admin Test", Email = "admin@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Role = "Admin", IsEmailVerified = true, IsVerified = true,
                    Phone = "0900000001"
                },
                new User
                {
                    UserID = 2, Name = "Customer Test", Email = "customer@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                    Role = "Customer", IsEmailVerified = true, IsVerified = true,
                    Phone = "0900000002", CitizenId = "012345678901"
                },
                new User
                {
                    UserID = 3, Name = "Unverified User", Email = "unverified@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
                    Role = "Customer", IsEmailVerified = false, IsVerified = false,
                    Phone = "0900000003"
                }
            );

            context.SaveChanges();
            return context;
        }
    }
}
