using CarRentalSystem.Areas.Admin.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services;

public class CoreRentalWorkflowRegressionTests
{
    [Theory]
    [InlineData(true, "Approved", "Available", true, false, true)]
    [InlineData(false, "Pending", "Pending", true, false, false)]
    [InlineData(false, "Rejected", "Pending", true, false, false)]
    [InlineData(true, "Approved", "Maintenance", true, false, false)]
    [InlineData(true, "Approved", "Available", false, true, false)]
    public void RentabilityPolicy_RequiresApprovedAvailableSelfDriveCar(
        bool isApproved,
        string approvalStatus,
        string carStatus,
        bool selfDrive,
        bool withDriver,
        bool expected)
    {
        var car = CreateCar(carStatus);
        car.IsApproved = isApproved;
        car.ApprovalStatus = approvalStatus;
        car.IsSelfDriveAllowed = selfDrive;
        car.IsWithDriverAllowed = withDriver;

        CarRentabilityPolicy.IsSelfDriveRentable(car).Should().Be(expected);
    }

    [Fact]
    public async Task Reservation_RejectsPendingCarBeforeCreatingHold()
    {
        await using var context = CreateContext();
        var car = CreateCar(CarStatusConstants.Pending);
        car.IsApproved = false;
        car.ApprovalStatus = CarApprovalStatusConstants.Pending;
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var result = await new ReservationService(context)
            .CheckCarAvailabilityAsync(car.CarID, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Contain("chưa được phê duyệt");
        (await context.BookingHolds.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AdminApproval_SetsApprovedAndAvailable()
    {
        await using var context = CreateContext();
        context.CarStatuses.AddRange(
            new CarStatus { Name = CarStatusConstants.Pending },
            new CarStatus { Name = CarStatusConstants.Available });
        await context.SaveChangesAsync();

        var car = CreateCar(CarStatusConstants.Pending);
        car.CarStatusID = await context.CarStatuses
            .Where(status => status.Name == CarStatusConstants.Pending)
            .Select(status => status.CarStatusID)
            .FirstAsync();
        car.CarStatus = null;
        car.IsApproved = false;
        car.ApprovalStatus = CarApprovalStatusConstants.Pending;
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var requests = CreateChangeRequestService(context);
        var request = await requests.CreateAsync(car, CarChangeOperationConstants.Create, ownerId: 1, snapshot: null);
        request.ApprovalSteps.Should().ContainSingle();
        request.Status.Should().Be(CarChangeRequestStatusConstants.PendingAdmin);
        var adminQueue = await requests.GetQueueAsync(99, UserRoleConstants.Admin);
        adminQueue.Should().ContainSingle(item => item.CarChangeRequestID == request.CarChangeRequestID);

        var controller = CreateApprovalController(requests);
        await controller.Approve(request.CarChangeRequestID);

        context.ChangeTracker.Clear();
        var saved = await context.Cars.Include(item => item.CarStatus).SingleAsync();
        saved.IsApproved.Should().BeTrue();
        saved.ApprovalStatus.Should().Be(CarApprovalStatusConstants.Approved);
        saved.CarStatus!.Name.Should().Be(CarStatusConstants.Available);
    }

    [Fact]
    public async Task StaffAreaApproval_AppliesRequestInSingleStep()
    {
        await using var context = CreateContext();
        context.CarStatuses.AddRange(
            new CarStatus { Name = CarStatusConstants.Pending },
            new CarStatus { Name = CarStatusConstants.Available });
        await context.SaveChangesAsync();

        var car = CreateCar(CarStatusConstants.Pending);
        car.CarStatusID = await context.CarStatuses
            .Where(status => status.Name == CarStatusConstants.Pending)
            .Select(status => status.CarStatusID)
            .FirstAsync();
        car.CarStatus = null;
        car.IsApproved = false;
        car.ApprovalStatus = CarApprovalStatusConstants.Pending;
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var areaService = new Mock<IAreaService>();
        areaService.Setup(service => service.GetAreaIdForLocationAsync(car.LocationID)).ReturnsAsync(7);
        areaService.Setup(service => service.GetAreaIdsForUserAsync(10)).ReturnsAsync(new List<int> { 7 });
        areaService.Setup(service => service.IsUserInAreaAsync(10, 7)).ReturnsAsync(true);

        var requests = CreateChangeRequestService(context, areaService.Object);
        var request = await requests.CreateAsync(car, CarChangeOperationConstants.Create, ownerId: 1, snapshot: null);

        request.ApprovalSteps.Should().ContainSingle(step => step.Stage == CarChangeApprovalStageConstants.Staff);
        request.Status.Should().Be(CarChangeRequestStatusConstants.PendingStaff);
        var queue = await requests.GetQueueAsync(10, UserRoleConstants.Staff);
        queue.Should().ContainSingle(item => item.CarChangeRequestID == request.CarChangeRequestID);

        var result = await requests.ApproveAsync(request.CarChangeRequestID, 10, UserRoleConstants.Staff);

        result.Success.Should().BeTrue();
        context.ChangeTracker.Clear();
        var saved = await context.Cars.Include(item => item.CarStatus).SingleAsync();
        saved.IsApproved.Should().BeTrue();
        saved.ApprovalStatus.Should().Be(CarApprovalStatusConstants.Approved);
        saved.CarStatus!.Name.Should().Be(CarStatusConstants.Available);
        (await context.CarChangeRequests.SingleAsync()).Status.Should().Be(CarChangeRequestStatusConstants.Approved);
    }

    [Fact]
    public async Task AdminRejection_StoresReasonAndKeepsCarNonRentable()
    {
        await using var context = CreateContext();
        context.CarStatuses.Add(new CarStatus { Name = CarStatusConstants.Pending });
        await context.SaveChangesAsync();

        var car = CreateCar(CarStatusConstants.Pending);
        car.CarStatusID = await context.CarStatuses.Select(status => status.CarStatusID).FirstAsync();
        car.CarStatus = null;
        car.IsApproved = false;
        car.ApprovalStatus = CarApprovalStatusConstants.Pending;
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var requests = CreateChangeRequestService(context);
        var request = await requests.CreateAsync(car, CarChangeOperationConstants.Create, ownerId: 1, snapshot: null);
        var controller = CreateApprovalController(requests);
        await controller.Reject(request.CarChangeRequestID, "Thiếu giấy đăng kiểm");

        context.ChangeTracker.Clear();
        var saved = await context.Cars.Include(item => item.CarStatus).SingleAsync();
        saved.IsApproved.Should().BeFalse();
        saved.ApprovalStatus.Should().Be(CarApprovalStatusConstants.Rejected);
        saved.ApprovalRejectionReason.Should().Be("Thiếu giấy đăng kiểm");
        CarRentabilityPolicy.IsSelfDriveRentable(saved).Should().BeFalse();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"core-rental-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static CarChangeRequestService CreateChangeRequestService(ApplicationDbContext context, IAreaService? areaServiceOverride = null)
    {
        var areaService = new Mock<IAreaService>();
        areaService
            .Setup(service => service.GetAreaIdForLocationAsync(It.IsAny<int>()))
            .ReturnsAsync((int?)null);

        var auditTrail = new Mock<IAuditTrailService>();
        auditTrail
            .Setup(service => service.RecordAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLog log, CancellationToken _) => log);

        return new CarChangeRequestService(context, areaServiceOverride ?? areaService.Object, auditTrail.Object);
    }

    private static CarApprovalController CreateApprovalController(ICarChangeRequestService requests)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "99"),
                new Claim(ClaimTypes.Role, UserRoleConstants.Admin)
            }, "TestAuth"))
        };

        var controller = new CarApprovalController(requests, Mock.Of<IAreaService>());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        return controller;
    }

    private static Car CreateCar(string statusName)
    {
        return new Car
        {
            CarName = "Regression Car",
            OwnerID = 1,
            BrandID = 1,
            CarTypeID = 1,
            LocationID = 1,
            CarStatusID = 1,
            CarStatus = new CarStatus { CarStatusID = 1, Name = statusName },
            LicensePlate = $"TEST-{Guid.NewGuid():N}"[..12],
            VIN = $"TEST{Guid.NewGuid():N}"[..17],
            Seats = 5,
            PricePerDay = 1_000_000m,
            PricePerHour = 120_000m,
            IsApproved = true,
            ApprovalStatus = CarApprovalStatusConstants.Approved,
            IsSelfDriveAllowed = true,
            IsWithDriverAllowed = false
        };
    }
}
