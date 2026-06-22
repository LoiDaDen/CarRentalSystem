using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.ViewComponents;

public sealed class QuickGuideViewComponent : ViewComponent
{
    private readonly ITutorialService _tutorials;

    public QuickGuideViewComponent(ITutorialService tutorials)
    {
        _tutorials = tutorials;
    }

    public async Task<IViewComponentResult> InvokeAsync(string panel)
    {
        var area = ViewContext.RouteData.Values["area"]?.ToString() ?? string.Empty;
        var controller = ViewContext.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = ViewContext.RouteData.Values["action"]?.ToString() ?? string.Empty;
        var guide = await _tutorials.GetQuickGuideAsync(UserClaimsPrincipal, panel, area, controller, action);
        if (guide == null)
            return Content(string.Empty);

        return View(new QuickGuideViewModel
        {
            Panel = panel,
            DetailUrl = $"/Guide?role={panel.ToLowerInvariant()}",
            Guide = guide
        });
    }
}
