using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.ViewComponents;

public sealed class TutorialPopupViewComponent : ViewComponent
{
    private readonly ITutorialService _tutorials;

    public TutorialPopupViewComponent(ITutorialService tutorials)
    {
        _tutorials = tutorials;
    }

    public async Task<IViewComponentResult> InvokeAsync(string panel)
    {
        if (!await _tutorials.CanAccessPanelAsync(UserClaimsPrincipal, panel))
            return Content(string.Empty);

        return View(new TutorialPopupViewModel
        {
            Panel = panel,
            DetailUrl = $"/Guide?role={panel.ToLowerInvariant()}"
        });
    }
}
