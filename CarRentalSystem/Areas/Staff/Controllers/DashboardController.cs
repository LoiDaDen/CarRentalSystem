using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = PortalPolicyNames.RequireStaff)]
public sealed class DashboardController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInternalPortalScopeService _scope;

    public DashboardController(ApplicationDbContext context, IInternalPortalScopeService scope)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<IActionResult> Index()
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(CurrentUserId, CurrentRole);
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var bookings = _context.Bookings.AsNoTracking()
            .Where(booking => booking.Car != null && locations.Contains(booking.Car.LocationID));

        var tasks = await bookings
            .Where(booking => booking.AssignedStaffUserID == CurrentUserId)
            .OrderBy(booking => booking.StartDate)
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

        var model = new StaffDashboardViewModel(
            await bookings.CountAsync(booking => booking.StartDate >= today && booking.StartDate < tomorrow),
            await bookings.CountAsync(booking => booking.StartDate < tomorrow && booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.Confirmed),
            await bookings.CountAsync(booking => booking.EndDate < tomorrow && booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.Active),
            await _context.SupportTickets.AsNoTracking().CountAsync(ticket => ticket.AssignedToUserID == CurrentUserId && ticket.Status != SupportTicketStatusConstants.Closed && ticket.Status != SupportTicketStatusConstants.Resolved),
            tasks);

        return View(model);
    }
}
