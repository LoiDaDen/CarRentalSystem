using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = PortalPolicyNames.RequireManager)]
public sealed class AreaController : InternalPortalControllerBase
{
    private readonly IAreaService _areas;

    public AreaController(IAreaService areas)
    {
        _areas = areas;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCurrent(int areaId, string? returnUrl)
    {
        if (!await _areas.IsAreaAllowedForUserAsync(CurrentUserId, areaId))
            return Forbid();

        Response.Cookies.Append("CurrentAreaId", areaId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = TimeSpan.FromDays(30)
        });
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Dashboard");
    }
}
