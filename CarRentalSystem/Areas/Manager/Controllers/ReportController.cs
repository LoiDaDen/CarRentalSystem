using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = PortalPolicyNames.RequireManager)]
public sealed class ReportController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInternalPortalScopeService _scope;
    private readonly IManagerReportService _reports;

    public ReportController(ApplicationDbContext context, IInternalPortalScopeService scope, IManagerReportService reports)
    {
        _context = context;
        _scope = scope;
        _reports = reports;
    }

    public async Task<IActionResult> Index()
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(CurrentUserId, CurrentRole);
        var query = _context.Bookings.AsNoTracking().Where(booking => booking.Car != null && locations.Contains(booking.Car.LocationID));
        var statuses = await query
            .GroupBy(booking => booking.BookingStatus != null ? booking.BookingStatus.Name : "Unknown")
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count);
        var recent = await query.OrderByDescending(booking => booking.CreatedAt).Take(20)
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
        return View(new ManagerReportViewModel(await _reports.GetSnapshotAsync(CurrentUserId), statuses, recent));
    }
}
