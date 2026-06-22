using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CarRentalSystem.Tests.IntegrationTests
{
    public class ReservationConcurrencyTests
    {
        [Fact]
        [Trait("Module", "Reservation")]
        [Trait("Type", "Concurrency")]
        public async Task CreateHoldAsync_AllowsOnlyOneUserForSameCarAndWindow()
        {
            var databaseName = $"reservation_{Guid.NewGuid():N}";
            var connectionString = $"Data Source=file:{databaseName}?mode=memory&cache=shared";
            await using var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();

            var options = CreateOptions(connectionString);
            await using (var setup = new SqliteReservationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                SeedReservationData(setup);
                await setup.SaveChangesAsync();
            }

            var start = DateTime.UtcNow.AddDays(7);
            var end = start.AddDays(1);

            var first = CreateHoldSafelyAsync(options, userId: 2, start, end);
            var second = CreateHoldSafelyAsync(options, userId: 3, start, end);
            var results = await Task.WhenAll(first, second);

            results.Count(r => r.IsSuccess).Should().Be(1);
            results.Count(r => !r.IsSuccess).Should().Be(1);

            await using var verify = new SqliteReservationDbContext(options);
            var activeHolds = await verify.BookingHolds
                .Where(h => h.CarID == 1 &&
                    h.Status == BookingHoldStatusConstants.Active &&
                    h.StartDate < end &&
                    h.EndDate > start)
                .CountAsync();
            activeHolds.Should().Be(1);
        }

        private static DbContextOptions<ApplicationDbContext> CreateOptions(string connectionString)
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString)
                .Options;
        }

        private static async Task<BookingHoldResult> CreateHoldSafelyAsync(
            DbContextOptions<ApplicationDbContext> options,
            int userId,
            DateTime start,
            DateTime end)
        {
            try
            {
                await using var context = new SqliteReservationDbContext(options);
                var service = new ReservationService(context);
                return await service.CreateHoldAsync(userId, carId: 1, start, end);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException)
            {
                return new BookingHoldResult(false, null, ex.InnerException.Message);
            }
            catch (SqliteException ex)
            {
                return new BookingHoldResult(false, null, ex.Message);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("transaction", StringComparison.OrdinalIgnoreCase))
            {
                return new BookingHoldResult(false, null, ex.Message);
            }
        }

        private static void SeedReservationData(ApplicationDbContext context)
        {
            context.BookingStatuses.AddRange(
                new BookingStatus { BookingStatusID = 1, Name = BookingStatusConstants.Pending },
                new BookingStatus { BookingStatusID = 2, Name = BookingStatusConstants.Confirmed },
                new BookingStatus { BookingStatusID = 3, Name = BookingStatusConstants.Active },
                new BookingStatus { BookingStatusID = 4, Name = BookingStatusConstants.Completed },
                new BookingStatus { BookingStatusID = 5, Name = BookingStatusConstants.Cancelled },
                new BookingStatus { BookingStatusID = 6, Name = BookingStatusConstants.Unpaid });

            context.CarStatuses.Add(new CarStatus { CarStatusID = 1, Name = "Available" });
            context.Locations.Add(new Location { LocationID = 1, Name = "Ho Chi Minh", Latitude = 10.7769, Longitude = 106.7009 });
            context.CarBrands.Add(new CarBrand { BrandID = 1, Name = "Toyota" });
            context.CarTypes.Add(new CarType { CarTypeID = 1, Name = "Sedan" });
            context.Users.AddRange(
                new User
                {
                    UserID = 2,
                    Name = "Customer One",
                    Email = "one@example.test",
                    PasswordHash = "hash",
                    Role = "Customer",
                    IsEmailVerified = true,
                    IsVerified = true
                },
                new User
                {
                    UserID = 3,
                    Name = "Customer Two",
                    Email = "two@example.test",
                    PasswordHash = "hash",
                    Role = "Customer",
                    IsEmailVerified = true,
                    IsVerified = true
                });
            context.Cars.Add(new Car
            {
                CarID = 1,
                CarName = "Concurrent Test Car",
                BrandID = 1,
                LicensePlate = "51A-99999",
                VIN = "VINCONCURRENCY000",
                PricePerDay = 1_000_000m,
                PricePerHour = 120_000m,
                CarStatusID = 1,
                LocationID = 1,
                CarTypeID = 1,
                Seats = 5,
                DefaultDeposit = 5_000_000m
            });
        }

        private sealed class SqliteReservationDbContext : ApplicationDbContext
        {
            public SqliteReservationDbContext(DbContextOptions<ApplicationDbContext> options)
                : base(options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
                {
                    var columnType = property.GetColumnType();
                    if (columnType?.Contains("max", StringComparison.OrdinalIgnoreCase) == true)
                        property.SetColumnType(null);
                }
            }
        }
    }
}
