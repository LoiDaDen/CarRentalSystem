using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    /// <summary>
    /// Unit tests cho TrackingService — GPS tracking, Alerts, Resolve
    /// </summary>
    public class TrackingServiceTests
    {
        private readonly List<VehicleTracking> _trackings;
        private readonly List<Alert> _alerts;
        private readonly TrackingService _trackingService;

        public TrackingServiceTests()
        {
            // Setup tracking data — 2 bản ghi cho xe CarID=10
            _trackings = new List<VehicleTracking>
            {
                new VehicleTracking
                {
                    TrackingID = 1,
                    CarID = 10,
                    Latitude = 10.762622,
                    Longitude = 106.660172,
                    Speed = 40,
                    Timestamp = DateTime.Now.AddHours(-3)
                },
                new VehicleTracking
                {
                    TrackingID = 2,
                    CarID = 10,
                    Latitude = 10.775622,
                    Longitude = 106.670172,
                    Speed = 60,
                    Timestamp = DateTime.Now.AddHours(-1)
                },
            };

            // Setup alerts — 1 active, 1 resolved
            _alerts = new List<Alert>
            {
                new Alert
                {
                    AlertID = 1,
                    CarID = 10,
                    AlertType = "OffRoute",
                    Status = "New",
                    Description = "Vehicle is >5km away from planned route.",
                    AlertTime = DateTime.Now.AddHours(-2)
                },
                new Alert
                {
                    AlertID = 2,
                    CarID = 10,
                    AlertType = "SuspiciousStop",
                    Status = "Resolved",
                    Description = "Vehicle has not moved for over 2 hours.",
                    AlertTime = DateTime.Now.AddHours(-5)
                },
            };

            var trackingRepo = MockRepositoryHelper.Create(_trackings);
            var alertRepo = MockRepositoryHelper.Create(_alerts);

            // Setup GetByIdAsync cho alert
            alertRepo.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(() => _alerts.FirstOrDefault(a => a.AlertID == 1));
            alertRepo.Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((Alert?)null);

            _trackingService = new TrackingService(
                trackingRepo.Object,
                alertRepo.Object,
                MockRepositoryHelper.CreateSeededDbContext());
        }

        // =============================================
        // ADD TRACKING TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task AddTracking_NewTracking_PersistsToRepository()
        {
            // Arrange
            var initialCount = _trackings.Count;
            var newTracking = new VehicleTracking
            {
                TrackingID = 0,
                CarID = 10,
                Latitude = 10.780000,
                Longitude = 106.680000,
                Speed = 55,
                Timestamp = DateTime.Now
            };

            // Act
            await _trackingService.AddTrackingAsync(newTracking);

            // Assert
            _trackings.Count.Should().Be(initialCount + 1, "vì bản ghi tracking mới phải được lưu vào repository");
        }

        // =============================================
        // GET CAR HISTORY TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task GetCarHistory_ExistingCar_ReturnsOrderedByTimestamp()
        {
            // Act
            var result = await _trackingService.GetCarHistoryAsync(10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2, "vì xe CarID=10 có 2 bản ghi tracking");
            result.First().Timestamp.Should().BeAfter(result.Last().Timestamp,
                "vì kết quả phải sắp xếp theo thời gian giảm dần (mới nhất trước)");
        }

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task GetCarHistory_NonExistentCar_ReturnsEmpty()
        {
            // Act
            var result = await _trackingService.GetCarHistoryAsync(9999);

            // Assert
            result.Should().BeEmpty("vì xe CarID=9999 không có bản ghi tracking nào");
        }

        // =============================================
        // GET LATEST LOCATION TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task GetLatestLocation_ExistingCar_ReturnsNewest()
        {
            // Act
            var result = await _trackingService.GetLatestLocationAsync(10);

            // Assert
            result.Should().NotBeNull();
            result!.TrackingID.Should().Be(2, "vì bản ghi ID=2 có Timestamp mới nhất");
            result.Speed.Should().Be(60);
        }

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task GetLatestLocation_NoData_ReturnsNull()
        {
            // Act
            var result = await _trackingService.GetLatestLocationAsync(9999);

            // Assert
            result.Should().BeNull("vì xe CarID=9999 chưa có bản ghi tracking nào");
        }

        // =============================================
        // ALERT TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task AddAlert_NewAlert_PersistsToRepository()
        {
            // Arrange
            var initialCount = _alerts.Count;
            var newAlert = new Alert
            {
                AlertID = 0,
                CarID = 10,
                AlertType = "GpsLost",
                Status = "New",
                Description = "GPS signal lost for vehicle."
            };

            // Act
            await _trackingService.AddAlertAsync(newAlert);

            // Assert
            _alerts.Count.Should().Be(initialCount + 1, "vì alert mới phải được lưu vào repository");
        }

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task GetActiveAlerts_ReturnsOnlyUnresolved()
        {
            // Act
            var result = await _trackingService.GetActiveAlertsAsync();

            // Assert
            result.Should().NotBeNull();
            result.All(a => a.Status != "Resolved").Should().BeTrue(
                "vì chỉ trả về các alert chưa được giải quyết");
        }

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task GetActiveAlerts_NoActive_ReturnsEmpty()
        {
            // Arrange — resolve tất cả alerts
            foreach (var alert in _alerts)
                alert.Status = "Resolved";

            // Act
            var result = await _trackingService.GetActiveAlertsAsync();

            // Assert
            result.Should().BeEmpty("vì tất cả alert đã được giải quyết");
        }

        // =============================================
        // RESOLVE ALERT TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task ResolveAlert_ExistingAlert_SetsStatusToResolved()
        {
            // Act
            await _trackingService.ResolveAlertAsync(1);

            // Assert
            _alerts.First(a => a.AlertID == 1).Status.Should().Be("Resolved",
                "vì alert phải được đánh dấu đã giải quyết");
        }

        [Fact]
        [Trait("Module", "Tracking")]
        [Trait("Type", "Unit")]
        public async Task ResolveAlert_NonExistentAlert_DoesNotThrow()
        {
            // Act
            var act = async () => await _trackingService.ResolveAlertAsync(999);

            // Assert
            await act.Should().NotThrowAsync("vì resolve alert không tồn tại phải được xử lý gracefully");
        }
    }
}
