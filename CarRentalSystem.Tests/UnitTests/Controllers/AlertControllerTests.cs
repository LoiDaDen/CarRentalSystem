using System.Collections.Generic;
using System.Threading.Tasks;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho AlertController — Xem danh sách cảnh báo, Resolve
    /// </summary>
    public class AlertControllerTests
    {
        private readonly Mock<ITrackingService> _mockService;
        private readonly Mock<ICarService> _mockCarService;
        private readonly AlertController _controller;

        public AlertControllerTests()
        {
            _mockService = new Mock<ITrackingService>();
            _mockCarService = new Mock<ICarService>();
            _mockService
                .Setup(s => s.GetAlertsAsync(null, null, null, null, null))
                .ReturnsAsync(new List<Alert>());
            _mockCarService.Setup(s => s.GetAllCarsAsync()).ReturnsAsync(new List<Car>());
            _controller = new AlertController(_mockService.Object, _mockCarService.Object);
        }

        // =============================================
        // INDEX TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Alert")]
        [Trait("Type", "Unit")]
        public async Task Index_ReturnsView_WithActiveAlerts()
        {
            // Arrange
            var activeAlerts = new List<Alert>
            {
                new Alert { AlertID = 1, Status = "Active" }
            };
            _mockService.Setup(s => s.GetAlertsAsync(null, null, null, null, null)).ReturnsAsync(activeAlerts);

            // Act
            var result = await _controller.Index(null, null, null, null, null) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.Model.Should().BeEquivalentTo(activeAlerts);
        }

        // =============================================
        // RESOLVE TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Alert")]
        [Trait("Type", "Unit")]
        public async Task Resolve_ShouldCallResolveAndRedirect()
        {
            // Act
            var result = await _controller.Resolve(5) as RedirectToActionResult;

            // Assert
            _mockService.Verify(s => s.ResolveAlertAsync(5), Times.Once);
            result.Should().NotBeNull();
            result!.ActionName.Should().Be("Index");
        }
    }
}
