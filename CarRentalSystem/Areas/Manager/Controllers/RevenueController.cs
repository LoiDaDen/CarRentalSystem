using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = PortalPolicyNames.RequireManager)]
public sealed class RevenueController : InternalPortalControllerBase
{
    private readonly IManagerReportService _reports;

    public RevenueController(IManagerReportService reports)
    {
        _reports = reports;
    }

    public async Task<IActionResult> Index() => View(await _reports.GetSnapshotAsync(CurrentUserId));
}
