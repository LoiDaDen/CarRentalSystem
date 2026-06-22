using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = PortalPolicyNames.RequireStaff)]
public sealed class ReturnController : InternalPortalControllerBase
{
    private readonly IHandoverWorkflowService _workflow;
    private readonly IInternalPortalScopeService _scope;

    public ReturnController(IHandoverWorkflowService workflow, IInternalPortalScopeService scope)
    {
        _workflow = workflow;
        _scope = scope;
    }

    public async Task<IActionResult> Index() =>
        View(await _workflow.GetQueueAsync(CurrentUserId, CurrentRole, "CheckOut"));

    public async Task<IActionResult> Capture(int bookingId)
    {
        if (!await _scope.CanAccessBookingAsync(CurrentUserId, UserRoleConstants.Staff, bookingId))
            return Forbid();

        return RedirectToAction("Capture", "Handover", new { area = "", bookingId, type = "CheckOut" });
    }
}
