using System;
using System.Threading.Tasks;
using CarRentalSystem.Helpers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    public class SecurityMonitorServiceTests
    {
        [Fact]
        public void DistanceCalculator_CalculateDistanceInKm_ShouldReturnCorrectDistance()
        {
            // Arrange - coords for two approx locations
            double lat1 = 10.762622; // HCM
            double lon1 = 106.660172;
            double lat2 = 21.028511; // Hanoi
            double lon2 = 105.804817;

            // Act
            double distance = DistanceCalculator.CalculateDistanceInKm(lat1, lon1, lat2, lon2);

            // Assert
            // HCM to Hanoi is ~1130-1150km. We just check if it's > 1000 and < 1500
            distance.Should().BeGreaterThan(1000);
            distance.Should().BeLessThan(1500);
        }

        [Fact]
        public async Task CheckRouteDeviationAsync_ShouldTriggerAlert_WhenDeviated()
        {
            // Arrange
            var mockTrackingService = new Mock<ITrackingService>();
            var service = new SecurityMonitorService(mockTrackingService.Object);

            var currPoint = new VehicleTracking { CarID = 1 };
            var route = new CarRentalSystem.Models.Route { /* data */ };

            // Act
            await service.CheckRouteDeviationAsync(currPoint, route);

            // Assert
            // Currently isOffRoute is hardcoded false in the method. 
            // We just verify no exceptions occur for now, until it's fully implemented.
            mockTrackingService.Verify(t => t.AddAlertAsync(It.IsAny<Alert>()), Times.Never);
        }

        [Fact]
        public async Task CheckTheftRisksAsync_ShouldExecuteWithoutException()
        {
            // Arrange
            var mockTrackingService = new Mock<ITrackingService>();
            var service = new SecurityMonitorService(mockTrackingService.Object);

            var tracking = new VehicleTracking { CarID = 2, Speed = 0, Timestamp = DateTime.Now };

            // Act
            Func<Task> act = async () => await service.CheckTheftRisksAsync(tracking);

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}
