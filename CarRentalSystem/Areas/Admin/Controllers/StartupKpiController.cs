using CarRentalSystem.Areas.Admin.Models;
using CarRentalSystem.Authorization;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CarRentalSystem.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PortalPolicyNames.RequireAdmin)]
public sealed class StartupKpiController : Controller
{
    private readonly IStartupKpiService _startupKpi;
    private readonly StartupKpiOptions _options;

    public StartupKpiController(
        IStartupKpiService startupKpi,
        IOptions<StartupKpiOptions> options)
    {
        _startupKpi = startupKpi;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var periodStartUtc = DateTime.SpecifyKind(_options.PeriodStartUtc, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddMonths(Math.Max(1, _options.PeriodMonths));
        var snapshot = await _startupKpi.GetSnapshotAsync(periodStartUtc, periodEndUtc, cancellationToken);

        return View(new StartupKpiViewModel
        {
            Snapshot = snapshot,
            Targets = _options
        });
    }
}
