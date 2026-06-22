using CarRentalSystem.Authorization;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = PortalPolicyNames.RequireManager)]
public sealed class CarController : InternalPortalControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInternalPortalScopeService _scope;

    public CarController(ApplicationDbContext context, IInternalPortalScopeService scope)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<IActionResult> Index(string? status = null)
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(CurrentUserId, CurrentRole);
        var query = _context.Cars.AsNoTracking()
            .Include(car => car.CarBrand)
            .Include(car => car.CarStatus)
            .Include(car => car.Location)
            .Where(car => !car.IsDeleted && locations.Contains(car.LocationID));
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(car => car.CarStatus != null && car.CarStatus.Name == status);
        return View(await query.OrderBy(car => car.LicensePlate).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        if (!await _scope.CanAccessCarAsync(CurrentUserId, CurrentRole, id))
            return Forbid();
        if (status is not CarStatusConstants.Available and not CarStatusConstants.Maintenance)
            return Forbid();

        var car = await _context.Cars.FirstOrDefaultAsync(item => item.CarID == id);
        var carStatus = await _context.CarStatuses.FirstOrDefaultAsync(item => item.Name == status);
        if (car == null || carStatus == null)
            return NotFound();

        car.CarStatusID = carStatus.CarStatusID;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật trạng thái xe.";
        return RedirectToAction(nameof(Index));
    }
}
