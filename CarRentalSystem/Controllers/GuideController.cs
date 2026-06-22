using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers;

public sealed class GuideController : Controller
{
    private static readonly Dictionary<string, (string Name, string Icon, string Color, string File)> PublicGuides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["customer"] = ("Khách hàng", "bi-person-fill", "#8b5cf6", "02-customer-guide.md"),
            ["driver"] = ("Tài xế", "bi-steering-wheel", "#3b82f6", "04-driver-guide.md")
        };

    private static readonly Dictionary<string, string> InternalPanels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"] = "Admin",
            ["partner"] = "Partner",
            ["staff"] = "Staff",
            ["manager"] = "Manager"
        };

    private readonly IWebHostEnvironment _environment;
    private readonly ITutorialService _tutorials;

    public GuideController(IWebHostEnvironment environment, ITutorialService tutorials)
    {
        _environment = environment;
        _tutorials = tutorials;
    }

    public async Task<IActionResult> Index(string role = "customer", CancellationToken cancellationToken = default)
    {
        if (InternalPanels.TryGetValue(role, out var panel))
        {
            if (User.Identity?.IsAuthenticated != true)
                return Challenge();
            if (!await _tutorials.CanAccessPanelAsync(User, panel, cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden);

            var tutorial = await _tutorials.GetTutorialAsync(User, panel, manual: true, cancellationToken);
            if (tutorial == null)
                return StatusCode(StatusCodes.Status403Forbidden);

            return View(new GuideViewModel
            {
                Role = role.ToLowerInvariant(),
                RoleName = tutorial.RoleName,
                RoleIcon = tutorial.Icon,
                RoleColor = "#2563eb",
                IsInternal = true,
                Tutorial = tutorial,
                QuickGuides = await _tutorials.GetAllQuickGuidesAsync(User, panel, cancellationToken)
            });
        }

        var selectedRole = PublicGuides.TryGetValue(role, out var requestedGuide)
            ? role.ToLowerInvariant()
            : "customer";
        var guide = PublicGuides[selectedRole];
        var guidePath = Path.Combine(_environment.ContentRootPath, "docs", "guides", guide.File);

        return View(new GuideViewModel
        {
            Role = selectedRole,
            RoleName = guide.Name,
            RoleIcon = guide.Icon,
            RoleColor = guide.Color,
            MarkdownContent = System.IO.File.Exists(guidePath)
                ? await System.IO.File.ReadAllTextAsync(guidePath, cancellationToken)
                : "# Hướng dẫn đang được cập nhật"
        });
    }
}
