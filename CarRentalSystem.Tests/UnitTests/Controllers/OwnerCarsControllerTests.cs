using System.Security.Claims;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using CarRentalSystem.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    public class OwnerCarsControllerTests
    {
        [Fact]
        [Trait("Module", "OwnerPortal")]
        [Trait("Type", "Security")]
        public void OwnerCarsController_RequiresAuthenticatedUser()
        {
            typeof(OwnerCarsController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Should().NotBeEmpty();
        }

        [Fact]
        [Trait("Module", "OwnerPortal")]
        [Trait("Type", "Security")]
        public async Task Index_OnlyShowsCarsOwnedByCurrentUser()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var (ownCar, _) = await SeedOwnerPortalDataAsync(context);
            var controller = CreateController(context, userId: 2);

            var result = await controller.Index();

            var model = result.Should().BeOfType<ViewResult>().Which.Model
                .Should().BeOfType<OwnerCarsDashboardViewModel>().Subject;
            model.Cars.Should().ContainSingle(c => c.CarID == ownCar.CarID);
            model.Cars.Should().OnlyContain(c => c.LicensePlate == ownCar.LicensePlate);
        }

        [Fact]
        [Trait("Module", "OwnerPortal")]
        [Trait("Type", "Security")]
        public async Task Calendar_OnlyShowsBookingsForCurrentOwnersCars()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var (ownCar, _) = await SeedOwnerPortalDataAsync(context);
            var controller = CreateController(context, userId: 2);

            var result = await controller.Calendar(DateTime.Today, DateTime.Today.AddDays(7));

            var model = result.Should().BeOfType<ViewResult>().Which.Model
                .Should().BeOfType<OwnerCalendarViewModel>().Subject;
            model.Bookings.Should().ContainSingle();
            model.Bookings.Single().CarID.Should().Be(ownCar.CarID);
        }

        [Fact]
        [Trait("Module", "OwnerPortal")]
        [Trait("Type", "Security")]
        public async Task Tracking_OnlyShowsCurrentOwnersCarsAndGps()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var (ownCar, _) = await SeedOwnerPortalDataAsync(context);
            var controller = CreateController(context, userId: 2);

            var result = await controller.Tracking();

            var model = result.Should().BeOfType<ViewResult>().Which.Model
                .Should().BeOfType<OwnerTrackingViewModel>().Subject;
            model.Cars.Should().ContainSingle(c => c.CarID == ownCar.CarID);
            model.MapPoints.Should().ContainSingle(p => p.CarID == ownCar.CarID);
        }

        [Fact]
        [Trait("Module", "OwnerPortal")]
        [Trait("Type", "Security")]
        public async Task Wallet_OnlyUsesCurrentOwnersBookings()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            await SeedOwnerPortalDataAsync(context);
            var controller = CreateController(context, userId: 2);

            var result = await controller.Wallet();

            var model = result.Should().BeOfType<ViewResult>().Which.Model
                .Should().BeOfType<OwnerWalletViewModel>().Subject;
            model.Balance.Should().Be(123000m);
            model.GrossRevenue.Should().Be(1000000m);
            model.BookingRows.Should().ContainSingle();
            model.BookingRows.Single().LicensePlate.Should().Be("51A-OWN01");
        }

        private static OwnerCarsController CreateController(CarRentalSystem.Data.ApplicationDbContext context, int userId)
        {
            var controller = new OwnerCarsController(context, Mock.Of<IFileUploadValidationService>());
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, $"User {userId}")
                }, "TestAuth"))
            };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            return controller;
        }

        private static async Task<(Car OwnCar, Car OtherCar)> SeedOwnerPortalDataAsync(CarRentalSystem.Data.ApplicationDbContext context)
        {
            var ownCar = new Car
            {
                OwnerID = 2,
                CarName = "Owner test car",
                BrandID = 1,
                CarTypeID = 1,
                LocationID = 1,
                CarStatusID = 1,
                LicensePlate = "51A-OWN01",
                VIN = "OWNCARVIN00000001",
                Seats = 5,
                PricePerDay = 1000000m,
                PricePerHour = 125000m,
                FuelType = "Xăng",
                Transmission = "Tự động",
                IsApproved = true,
                Latitude = 10.77,
                Longitude = 106.70,
                CommissionRate = 20m
            };
            var otherCar = new Car
            {
                OwnerID = 3,
                CarName = "Other owner car",
                BrandID = 1,
                CarTypeID = 1,
                LocationID = 1,
                CarStatusID = 1,
                LicensePlate = "51A-OTH01",
                VIN = "OTHCARVIN00000001",
                Seats = 5,
                PricePerDay = 900000m,
                PricePerHour = 112500m,
                FuelType = "Xăng",
                Transmission = "Tự động",
                IsApproved = true,
                Latitude = 10.78,
                Longitude = 106.71,
                CommissionRate = 20m
            };
            context.Cars.AddRange(ownCar, otherCar);
            await context.SaveChangesAsync();

            context.CarImages.Add(new CarImage { CarID = ownCar.CarID, ImageUrl = "/images/test-own.jpg" });
            context.VehicleTrackings.AddRange(
                new VehicleTracking { CarID = ownCar.CarID, Latitude = 10.77, Longitude = 106.70, Timestamp = DateTime.UtcNow },
                new VehicleTracking { CarID = otherCar.CarID, Latitude = 10.78, Longitude = 106.71, Timestamp = DateTime.UtcNow });
            context.Wallets.AddRange(
                new Wallet { UserID = 2, Balance = 123000m },
                new Wallet { UserID = 3, Balance = 999000m });

            var completed = await context.BookingStatuses.SingleAsync(s => s.Name == BookingStatusConstants.Completed);
            context.Bookings.AddRange(
                new Booking
                {
                    UserID = 2,
                    CarID = ownCar.CarID,
                    StartDate = DateTime.Today.AddDays(1),
                    EndDate = DateTime.Today.AddDays(2),
                    BookingStatusID = completed.BookingStatusID,
                    PickupLocationID = 1,
                    ReturnLocationID = 1,
                    TotalHours = 24,
                    TotalPrice = 1000000m
                },
                new Booking
                {
                    UserID = 2,
                    CarID = otherCar.CarID,
                    StartDate = DateTime.Today.AddDays(1),
                    EndDate = DateTime.Today.AddDays(2),
                    BookingStatusID = completed.BookingStatusID,
                    PickupLocationID = 1,
                    ReturnLocationID = 1,
                    TotalHours = 24,
                    TotalPrice = 900000m
                });

            context.PhysicalContracts.AddRange(
                new PhysicalContract { OwnerID = 2, CarID = ownCar.CarID, FileUrl = "/docs/own.pdf", FileKind = "Pdf" },
                new PhysicalContract { OwnerID = 3, CarID = otherCar.CarID, FileUrl = "/docs/other.pdf", FileKind = "Pdf" });

            await context.SaveChangesAsync();
            return (ownCar, otherCar);
        }
    }
}
