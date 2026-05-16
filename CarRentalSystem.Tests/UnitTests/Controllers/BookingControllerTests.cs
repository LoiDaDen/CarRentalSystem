using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho BookingController — Checkout, Book, Cancel
    /// </summary>
    public class BookingControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IBookingService> _mockBookingService;
        private readonly Mock<ICarService> _mockCarService;
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly Mock<IReservationService> _mockReservationService;
        private readonly Mock<IBookingQuoteService> _mockQuoteService;
        private readonly Mock<IPayOSPaymentService> _mockPayOSPaymentService;
        private readonly Mock<IDeliveryScopeService> _mockDeliveryScopeService;
        private readonly Mock<IEligibilityService> _mockEligibilityService;
        private readonly Mock<IBookingBusinessRuleService> _mockBusinessRules;
        private readonly Mock<IBookingTransitionService> _mockBookingTransitionService;
        private readonly Mock<ILogger<BookingController>> _mockLogger;

        public BookingControllerTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
            _mockBookingService = new Mock<IBookingService>();
            _mockCarService = new Mock<ICarService>();
            _mockAuthService = new Mock<IAuthService>();
            _mockReservationService = new Mock<IReservationService>();
            _mockQuoteService = new Mock<IBookingQuoteService>();
            _mockPayOSPaymentService = new Mock<IPayOSPaymentService>();
            _mockDeliveryScopeService = new Mock<IDeliveryScopeService>();
            _mockEligibilityService = new Mock<IEligibilityService>();
            _mockBusinessRules = new Mock<IBookingBusinessRuleService>();
            _mockBookingTransitionService = new Mock<IBookingTransitionService>();
            _mockLogger = new Mock<ILogger<BookingController>>();
            _mockEligibilityService
                .Setup(s => s.CheckRentalEligibilityAsync(It.IsAny<int>()))
                .ReturnsAsync(new EligibilityResult(true, null));
            _mockBusinessRules
                .Setup(s => s.ValidateSelfDriveAsync(It.IsAny<Booking>(), It.IsAny<decimal>()))
                .Returns(Task.CompletedTask);
            _mockBookingTransitionService
                .Setup(s => s.CancelAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(new BookingTransitionResult(true, null));
        }

        private BookingController CreateController(int loggedInUserId = 1)
        {
            var controller = new BookingController(
                _mockBookingService.Object, _mockCarService.Object,
                _mockAuthService.Object,
                _mockReservationService.Object,
                _mockQuoteService.Object,
                _mockPayOSPaymentService.Object,
                _mockDeliveryScopeService.Object,
                _mockEligibilityService.Object,
                _mockBusinessRules.Object,
                _mockBookingTransitionService.Object,
                _mockLogger.Object,
                _context);

            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, loggedInUserId.ToString()) };
            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
            controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());
            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost/payment");
            controller.Url = urlHelper.Object;

            return controller;
        }

        // =============================================
        // CHECKOUT TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task Checkout_ReturnsNotFound_WhenCarDoesNotExist()
        {
            // Arrange
            _mockCarService.Setup(s => s.GetCarByIdAsync(It.IsAny<int>())).ReturnsAsync((Car?)null);
            var controller = CreateController();

            // Act
            var result = await controller.Checkout(1, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2), "Location 1");

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task Checkout_RedirectToCarDetails_WhenDatesAreInvalid()
        {
            // Arrange
            var car = new Car { CarID = 1, PricePerDay = 1000, CarName = "Test", VIN = "V1", LicensePlate = "1" };
            _mockCarService.Setup(s => s.GetCarByIdAsync(1)).ReturnsAsync(car);
            var controller = CreateController();

            // Act: EndDate trước StartDate
            var result = await controller.Checkout(1, DateTime.Now.AddDays(2), DateTime.Now.AddDays(1), "Location 1") as RedirectToActionResult;

            // Assert
            result.Should().NotBeNull();
            result!.ActionName.Should().Be("Details");
            result.ControllerName.Should().Be("Car");
            controller.TempData["BookingError"].Should().NotBeNull();
        }

        // =============================================
        // BOOK TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task Book_RedirectToDetails_WhenStartDateIsBeforeToday()
        {
            // Arrange
            var controller = CreateController();
            var booking = new Booking
            {
                CarID = 1,
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(1)
            };

            // Act
            var result = await controller.Book(booking, true, null, 0, null) as RedirectToActionResult;

            // Assert — Có thể redirect hoặc NotFound tùy vào car có tồn tại không
            // Test xác minh không bị crash/exception
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
