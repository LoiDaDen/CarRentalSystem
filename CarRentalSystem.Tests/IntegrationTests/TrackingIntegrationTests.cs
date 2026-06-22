using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.IntegrationTests
{
    public class TrackingIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly TrackingService _trackingService;

        public TrackingIntegrationTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();

            var trackingRepo = new Repository<VehicleTracking>(_context);
            var alertRepo = new Repository<Alert>(_context);

            _trackingService = new TrackingService(trackingRepo, alertRepo, _context);
        }

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Integration")]
        public async Task AddTrackingAsync_ShouldPersistToDatabase()
        {
            // Arrange
            var tracking = new VehicleTracking
            {
                CarID = 1,
                Latitude = 10.7769,
                Longitude = 106.7009,
                Timestamp = DateTime.Now,
                Speed = 40.5
            };

            // Act
            await _trackingService.AddTrackingAsync(tracking);

            // Assert
            var retrieved = await _context.VehicleTrackings.FirstOrDefaultAsync(t => t.CarID == 1);
            retrieved.Should().NotBeNull();
            retrieved.Latitude.Should().Be(10.7769);
            retrieved.Speed.Should().Be(40.5);
        }

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Integration")]
        public async Task SetupAlertsAndResolve_ShouldWorkCorrectly()
        {
            // Arrange
            var alert1 = new Alert
            {
                CarID = 1,
                AlertType = "OffRoute",
                Status = "Active",
                Description = "Test Alert 1",
                AlertTime = DateTime.Now
            };

            var alert2 = new Alert
            {
                CarID = 2,
                AlertType = "Speeding",
                Status = "Active",
                Description = "Test Alert 2",
                AlertTime = DateTime.Now
            };

            await _trackingService.AddAlertAsync(alert1);
            await _trackingService.AddAlertAsync(alert2);

            // Act: Resolve alert 1
            await _trackingService.ResolveAlertAsync(alert1.AlertID);

            // Assert
            var activeAlerts = await _trackingService.GetActiveAlertsAsync();
            activeAlerts.Should().HaveCount(1);
            activeAlerts.First().CarID.Should().Be(2);

            var resolvedAlert = await _context.Alerts.FindAsync(alert1.AlertID);
            resolvedAlert.Status.Should().Be("Resolved");
        }

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Integration")]
        public async Task CheckForAlertsAsync_ShouldDoNothing_IfNoActiveBooking()
        {
            // Arrange
            // We have no active booking seeded for CarID = 3 with status "In-Progress"
            int carId = 3;
            double lat = 10.0;
            double lon = 106.0;

            // Act
            await _trackingService.CheckForAlertsAsync(carId, lat, lon);

            // Assert
            var alerts = await _context.Alerts.Where(a => a.CarID == carId).ToListAsync();
            alerts.Should().BeEmpty();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
