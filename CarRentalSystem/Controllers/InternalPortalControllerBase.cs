using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers;

public abstract class InternalPortalControllerBase : Controller
{
    protected int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;

    protected string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
