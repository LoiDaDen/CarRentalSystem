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
public sealed class BookingController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInternalPortalScopeService _scope;
    private readonly IBookingTransitionService _transitions;

    public BookingController(ApplicationDbContext context, IInternalPortalScopeService scope, IBookingTransitionService transitions)
    {
        _context = context;
        _scope = scope;
        _transitions = transitions;
    }

    public async Task<IActionResult> Index(string? status = null)
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(CurrentUserId, CurrentRole);
        var query = _context.Bookings.AsNoTracking()
            .Where(booking => booking.Car != null && locations.Contains(booking.Car.LocationID));
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(booking => booking.BookingStatus != null && booking.BookingStatus.Name == status);

        return View(await Project(query.OrderByDescending(booking => booking.CreatedAt).Take(200)).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _scope.CanAccessBookingAsync(CurrentUserId, CurrentRole, id))
            return Forbid();

        var item = await Project(_context.Bookings.AsNoTracking().Where(booking => booking.BookingID == id)).FirstOrDefaultAsync();
        return item == null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(int id, string targetStatus)
    {
        if (!await _scope.CanAccessBookingAsync(CurrentUserId, CurrentRole, id))
            return Forbid();
        if (targetStatus is not BookingStatusConstants.Active and not BookingStatusConstants.Completed)
            return Forbid();

        var result = await _transitions.TransitionAsync(id, targetStatus, CurrentUserId, CurrentRole, "Staff portal operation.");
        TempData[result.IsSuccess ? "Success" : "Error"] = result.ErrorMessage ?? "Đã cập nhật booking.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static IQueryable<PortalBookingRowViewModel> Project(IQueryable<Booking> query) =>
        query.Select(booking => new PortalBookingRowViewModel(
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
            booking.AssignedStaffUser != null ? booking.AssignedStaffUser.Name : null));
}
