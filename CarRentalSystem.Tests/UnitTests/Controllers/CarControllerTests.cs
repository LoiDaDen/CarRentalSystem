using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;
using System.Security.Claims;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho CarController — Danh sách xe, Chi tiết, Đánh giá
    /// </summary>
    public class CarControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ICarService> _mockCarService;

        public CarControllerTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
            _mockCarService = new Mock<ICarService>();
        }

        private CarController CreateController(ClaimsPrincipal? user = null)
        {
            var controller = new CarController(_mockCarService.Object, _context);
            var httpContext = new DefaultHttpContext();
            if (user != null) httpContext.User = user;

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            return controller;
        }

        // =============================================
        // INDEX TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task Index_ReturnsView_WithFilteredCars()
        {
            // Arrange
            var controller = CreateController();

            // Act
            var result = await controller.Index(
                brandId: null, carTypeId: null, seats: 5, minPrice: null, maxPrice: null,
                location: null, fuelType: null, transmission: null, category: null,
                isPremium: null, isSale: null, startDate: null, endDate: null,
                sort: "price_asc", contractId: null) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            var model = result!.Model as IEnumerable<Car>;
            model.Should().NotBeNull();
        }

        // =============================================
        // DETAILS TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task Details_ReturnsNotFound_WhenCarIsNull()
        {
            // Arrange
            _mockCarService.Setup(s => s.GetCarByIdAsync(99)).ReturnsAsync((Car?)null);
            var controller = CreateController();

            // Act
            var result = await controller.Details(99);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task Details_ReturnsViewWithViewModel_WhenCarExists()
        {
            // Arrange
            var car = new Car { CarID = 1, CarName = "Honda City", LicensePlate = "B", VIN = "2", PricePerDay = 1000000 };
            _mockCarService.Setup(s => s.GetCarByIdAsync(1)).ReturnsAsync(car);
            var controller = CreateController();

            // Act
            var result = await controller.Details(1) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            var model = result!.Model;
            model.Should().NotBeNull();
            model!.GetType().GetProperty("Car")!.GetValue(model).Should().Be(car);
        }

        // =============================================
        // REVIEW TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Car")]
        [Trait("Type", "Unit")]
        public async Task AddReview_ReturnsRedirect_WhenNotCompletedBooking()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "1") };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);
            var controller = CreateController(principal);

            // Act
            var result = await controller.AddReview(1, 5, "Good") as RedirectToActionResult;

            // Assert
            result.Should().NotBeNull();
            result!.ActionName.Should().Be("Details");
            controller.TempData["BookingError"].Should().NotBeNull();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
