using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services;

public sealed class InternalPortalServicesTests
{
    [Fact]
    [Trait("Module", "InternalPortal")]
    [Trait("Type", "Unit")]
    public async Task AssignAsync_ManagerAndStaffInBookingArea_AssignsAndAudits()
    {
        await using var context = CreateAssignmentContext();
        var areaService = CreateAreaService(isManager: true);
        var audit = CreateAuditTrail();
        var service = new BookingAssignmentService(context, areaService.Object, audit.Object);

        var result = await service.AssignAsync(100, 11, 10);

        result.IsSuccess.Should().BeTrue();
        var booking = await context.Bookings.SingleAsync(item => item.BookingID == 100);
        booking.AssignedStaffUserID.Should().Be(11);
        booking.AssignedByUserID.Should().Be(10);
        booking.AssignedAtUtc.Should().NotBeNull();
        audit.Verify(item => item.RecordAsync(
            It.Is<AuditLog>(log => log.Action == "AssignBooking" && log.EntityId == 100 && log.UserId == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Module", "InternalPortal")]
    [Trait("Type", "Security")]
    public async Task AssignAsync_StaffOutsideBookingArea_IsRejected()
    {
        await using var context = CreateAssignmentContext();
        var areaService = CreateAreaService(isManager: true);
        var audit = CreateAuditTrail();
        var service = new BookingAssignmentService(context, areaService.Object, audit.Object);

        var result = await service.AssignAsync(100, 12, 10);

        result.IsSuccess.Should().BeFalse();
        (await context.Bookings.SingleAsync(item => item.BookingID == 100)).AssignedStaffUserID.Should().BeNull();
        audit.Verify(item => item.RecordAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Module", "InternalPortal")]
    [Trait("Type", "Security")]
    public async Task AssignAsync_ManagerOutsideBookingArea_IsRejected()
    {
        await using var context = CreateAssignmentContext();
        var areaService = CreateAreaService(isManager: false);
        var audit = CreateAuditTrail();
        var service = new BookingAssignmentService(context, areaService.Object, audit.Object);

        var result = await service.AssignAsync(100, 11, 10);

        result.IsSuccess.Should().BeFalse();
        (await context.Bookings.SingleAsync(item => item.BookingID == 100)).AssignedStaffUserID.Should().BeNull();
        audit.Verify(item => item.RecordAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ApplicationDbContext CreateAssignmentContext()
    {
        var context = MockRepositoryHelper.CreateSeededDbContext();
        context.Users.AddRange(
            new User
            {
                UserID = 10,
                Name = "Manager",
                Email = "manager@portal.test",
                PasswordHash = "x",
                Phone = "0900000010",
                Role = UserRoleConstants.Manager,
                IsEmailVerified = true
            },
            new User
            {
                UserID = 11,
                Name = "Staff Area One",
                Email = "staff-one@portal.test",
                PasswordHash = "x",
                Phone = "0900000011",
                Role = UserRoleConstants.Staff,
                IsEmailVerified = true
            },
            new User
            {
                UserID = 12,
                Name = "Staff Area Two",
                Email = "staff-two@portal.test",
                PasswordHash = "x",
                Phone = "0900000012",
                Role = UserRoleConstants.Staff,
                IsEmailVerified = true
            });
        context.Areas.AddRange(
            new Area { AreaID = 1, Name = "Area One", Code = "A1", Province = "HCM" },
            new Area { AreaID = 2, Name = "Area Two", Code = "A2", Province = "HN" });
        context.AreaStaffs.AddRange(
            new AreaStaff { AreaID = 1, UserID = 10, RoleInArea = AreaStaffRoleConstants.Manager },
            new AreaStaff { AreaID = 1, UserID = 11, RoleInArea = AreaStaffRoleConstants.Staff },
            new AreaStaff { AreaID = 2, UserID = 12, RoleInArea = AreaStaffRoleConstants.Staff });
        context.Bookings.Add(new Booking
        {
            BookingID = 100,
            UserID = 2,
            CarID = 1,
            PickupLocationID = 1,
            ReturnLocationID = 1,
            StartDate = DateTime.UtcNow.AddHours(1),
            EndDate = DateTime.UtcNow.AddDays(1),
            TotalPrice = 1_000_000,
            TotalHours = 24,
            BookingStatusID = 2
        });
        context.SaveChanges();
        return context;
    }

    private static Mock<IAreaService> CreateAreaService(bool isManager)
    {
        var service = new Mock<IAreaService>();
        service.Setup(item => item.GetAreaIdForLocationAsync(1)).ReturnsAsync(1);
        service.Setup(item => item.IsUserAreaManagerAsync(10, 1)).ReturnsAsync(isManager);
        return service;
    }

    private static Mock<IAuditTrailService> CreateAuditTrail()
    {
        var audit = new Mock<IAuditTrailService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLog log, CancellationToken _) => log);
        return audit;
    }
}
