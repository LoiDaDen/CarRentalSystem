using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = PortalPolicyNames.RequireManager)]
public sealed class ProfileController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAreaService _areas;

    public ProfileController(ApplicationDbContext context, IAreaService areas)
    {
        _context = context;
        _areas = areas;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.UserID == CurrentUserId);
        if (user == null)
            return NotFound();
        var areaNames = (await _areas.GetAreasForUserAsync(CurrentUserId)).Select(area => area.Name).ToList();
        return View(new PortalProfileViewModel(user.Name, user.Email, PortalViewModelMasking.MaskPhone(user.Phone), user.Role, areaNames));
    }
}
