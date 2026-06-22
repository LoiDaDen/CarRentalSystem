using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = PortalPolicyNames.RequireStaff)]
public sealed class CarApprovalController : InternalPortalControllerBase
{
    private readonly ICarChangeRequestService _requests;

    public CarApprovalController(ICarChangeRequestService requests)
    {
        _requests = requests;
    }

    public async Task<IActionResult> Index(string status = CarApprovalStatusConstants.Pending)
    {
        ViewBag.CanApprove = true;
        ViewBag.CurrentRole = UserRoleConstants.Staff;
        ViewBag.CurrentStage = CarChangeApprovalStageConstants.Staff;
        ViewBag.Status = status;
        return View(await _requests.GetQueueAsync(CurrentUserId, UserRoleConstants.Staff, status));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var result = await _requests.ApproveAsync(id, CurrentUserId, UserRoleConstants.Staff);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        var result = await _requests.RejectAsync(id, CurrentUserId, UserRoleConstants.Staff, reason);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
