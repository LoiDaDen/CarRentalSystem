using CarRentalSystem.Authorization;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PortalPolicyNames.RequireAdmin)]
public class CarApprovalController : AdminAreaControllerBase
{
    private readonly ICarChangeRequestService _requests;

    public CarApprovalController(ICarChangeRequestService requests, IAreaService areaService)
        : base(areaService)
    {
        _requests = requests;
    }

    public async Task<IActionResult> Index(string status = CarApprovalStatusConstants.Pending)
    {
        ViewBag.CanApprove = true;
        ViewBag.CurrentRole = UserRoleConstants.Admin;
        ViewBag.CurrentStage = CarChangeApprovalStageConstants.Admin;
        ViewBag.Status = status;
        return View(await _requests.GetQueueAsync(GetCurrentUserId(), UserRoleConstants.Admin, status));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var result = await _requests.ApproveAsync(id, GetCurrentUserId(), UserRoleConstants.Admin);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        var result = await _requests.RejectAsync(id, GetCurrentUserId(), UserRoleConstants.Admin, reason);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
