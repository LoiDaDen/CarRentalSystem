using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = PortalPolicyNames.RequireManager)]
public sealed class DashboardController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IManagerReportService _reports;
    private readonly IInternalPortalScopeService _scope;

    public DashboardController(ApplicationDbContext context, IManagerReportService reports, IInternalPortalScopeService scope)
    {
        _context = context;
        _reports = reports;
        _scope = scope;
    }

    public async Task<IActionResult> Index()
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(CurrentUserId, CurrentRole);
        var alerts = await _context.Bookings.AsNoTracking()
            .Where(booking => booking.Car != null && locations.Contains(booking.Car.LocationID) &&
                ((booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.NeedReview) ||
                 (booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.Active && booking.EndDate < DateTime.UtcNow)))
            .OrderBy(booking => booking.EndDate)
            .Take(12)
            .Select(booking => new PortalBookingRowViewModel(
                booking.BookingID,
                booking.User != null ? booking.User.Name : "Khách hàng",
                "Đã ẩn",
                booking.Car != null ? booking.Car.CarName : "Xe",
                booking.Car != null ? booking.Car.LicensePlate : string.Empty,
                booking.StartDate,
                booking.EndDate,
                booking.BookingStatus != null ? booking.BookingStatus.Name : "Unknown",
                booking.TotalPrice,
                booking.AssignedStaffUserID,
                booking.AssignedStaffUser != null ? booking.AssignedStaffUser.Name : null))
            .ToListAsync();

        return View(new ManagerDashboardViewModel(await _reports.GetSnapshotAsync(CurrentUserId), alerts));
    }
}
