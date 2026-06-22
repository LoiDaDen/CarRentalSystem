using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = PortalPolicyNames.RequireStaff)]
public sealed class SupportController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInternalPortalScopeService _scope;
    private readonly ISupportTicketService _tickets;

    public SupportController(ApplicationDbContext context, IInternalPortalScopeService scope, ISupportTicketService tickets)
    {
        _context = context;
        _scope = scope;
        _tickets = tickets;
    }

    public async Task<IActionResult> Index(string? status = null)
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(CurrentUserId, CurrentRole);
        var query = _context.SupportTickets.AsNoTracking()
            .Where(ticket => ticket.AssignedToUserID == CurrentUserId ||
                (ticket.Booking != null && ticket.Booking.Car != null && locations.Contains(ticket.Booking.Car.LocationID)));
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(ticket => ticket.Status == status);

        return View(await query.OrderByDescending(ticket => ticket.CreatedAt).Take(200).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? resolution)
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(CurrentUserId, CurrentRole);
        var allowed = await _context.SupportTickets.AsNoTracking().AnyAsync(ticket =>
            ticket.SupportTicketID == id &&
            (ticket.AssignedToUserID == CurrentUserId ||
             (ticket.Booking != null && ticket.Booking.Car != null && locations.Contains(ticket.Booking.Car.LocationID))));
        if (!allowed)
            return Forbid();

        var result = await _tickets.UpdateStatusAsync(id, status, CurrentUserId, CurrentRole, resolution);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
