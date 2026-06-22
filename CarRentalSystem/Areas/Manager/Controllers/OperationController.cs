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
public sealed class OperationController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInternalPortalScopeService _scope;
    private readonly IHandoverWorkflowService _handover;

    public OperationController(ApplicationDbContext context, IInternalPortalScopeService scope, IHandoverWorkflowService handover)
    {
        _context = context;
        _scope = scope;
        _handover = handover;
    }

    public async Task<IActionResult> Index()
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(CurrentUserId, CurrentRole);
        return View(new ManagerOperationViewModel(
            await _handover.GetQueueAsync(CurrentUserId, CurrentRole, "CheckIn"),
            await _handover.GetQueueAsync(CurrentUserId, CurrentRole, "CheckOut"),
            await _context.Alerts.AsNoTracking().CountAsync(alert => !alert.IsDeleted && locations.Contains(alert.Car!.LocationID) && alert.Status != "Resolved"),
            await _context.Cars.AsNoTracking().CountAsync(car => !car.IsDeleted && locations.Contains(car.LocationID) && car.CarStatus != null && car.CarStatus.Name == CarStatusConstants.Maintenance)));
    }
}
