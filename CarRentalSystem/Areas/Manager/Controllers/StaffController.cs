using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = PortalPolicyNames.RequireManager)]
public sealed class StaffController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAreaService _areas;

    public StaffController(ApplicationDbContext context, IAreaService areas)
    {
        _context = context;
        _areas = areas;
    }

    public async Task<IActionResult> Index(int? areaId = null)
    {
        var allowedAreas = await _areas.GetAreaIdsForUserAsync(CurrentUserId);
        var selectedAreaId = areaId.HasValue && allowedAreas.Contains(areaId.Value) ? areaId.Value : allowedAreas.FirstOrDefault();
        ViewBag.Areas = await _areas.GetAreasForUserAsync(CurrentUserId);
        ViewBag.AvailableStaff = await _context.Users.AsNoTracking()
            .Where(user => user.Role == UserRoleConstants.Staff)
            .OrderBy(user => user.Name)
            .ToListAsync();

        if (selectedAreaId <= 0 || !await _areas.IsUserAreaManagerAsync(CurrentUserId, selectedAreaId))
            return View(Array.Empty<ManagerStaffRowViewModel>());

        return View(await _context.AreaStaffs.AsNoTracking()
            .Where(item => item.AreaID == selectedAreaId && !item.IsDeleted && item.User != null && item.User.Role == UserRoleConstants.Staff)
            .OrderBy(item => item.User!.Name)
            .Select(item => new ManagerStaffRowViewModel(
                item.UserID,
                item.User!.Name,
                item.User.Email,
                item.AreaID,
                item.Area!.Name,
                item.AssignedAt,
                _context.Bookings.Count(booking => booking.AssignedStaffUserID == item.UserID)))
            .ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int areaId, int userId)
    {
        if (!await _areas.IsUserAreaManagerAsync(CurrentUserId, areaId))
            return Forbid();
        if (!await _context.Users.AnyAsync(user => user.UserID == userId && user.Role == UserRoleConstants.Staff))
            return Forbid();
        if (!await _context.AreaStaffs.AnyAsync(item => item.AreaID == areaId && item.UserID == userId && !item.IsDeleted))
        {
            _context.AreaStaffs.Add(new AreaStaff
            {
                AreaID = areaId,
                UserID = userId,
                RoleInArea = AreaStaffRoleConstants.Staff,
                AssignedBy = CurrentUserId,
                AssignedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { areaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int areaId, int userId)
    {
        if (!await _areas.IsUserAreaManagerAsync(CurrentUserId, areaId))
            return Forbid();
        var item = await _context.AreaStaffs.FirstOrDefaultAsync(row => row.AreaID == areaId && row.UserID == userId && !row.IsDeleted && row.User != null && row.User.Role == UserRoleConstants.Staff);
        if (item != null)
        {
            item.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { areaId });
    }
}
