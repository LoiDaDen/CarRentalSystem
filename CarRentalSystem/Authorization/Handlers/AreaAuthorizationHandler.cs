// CarRentalSystem/Authorization/Handlers/AreaAuthorizationHandler.cs
using System.Security.Claims;
using CarRentalSystem.Authorization.Requirements;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Authorization.Handlers;

/// <summary>
/// Authorization handler xử lý tất cả Area-related requirements.
///
/// Authorization matrix:
///
/// | Requirement              | Manager (hệ thống) | AreaManager | Staff/Viewer        |
/// |-------------------------|--------------------|-------------|--------------------|
/// | AreaView (list all)     | ✅                  | ✅           | ✅ (filtered)       |
/// | AreaCreate              | ✅                  | ❌           | ❌                  |
/// | AreaUpdate              | ✅                  | ✅           | ❌                  |
/// | AreaDelete              | ✅ + no active BK  | ❌           | ❌                  |
/// | AreaManageStaff         | ✅                  | ✅           | ❌                  |
/// | AreaViewStats           | ✅                  | ✅ (trong area) | ❌             |
/// </summary>
public class AreaAuthorizationHandler : AuthorizationHandler<IAuthorizationRequirement, Area>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AreaAuthorizationHandler> _logger;

    private static readonly string[] ViewableRoles = { "Manager", "Staff", "Viewer" };

    public AreaAuthorizationHandler(
        IServiceProvider serviceProvider,
        ILogger<AreaAuthorizationHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement,
        Area? area)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("sub")?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning(
                "AreaAuthorizationHandler: cannot parse user ID from claims for user {User}",
                user.Identity?.Name);
            return;
        }

        var role = user.FindFirst(ClaimTypes.Role)?.Value
                ?? user.FindFirst("role")?.Value
                ?? "";

        bool succeeded = requirement switch
        {
            AreaViewRequirement => await HandleViewAsync(userId, role, area),
            AreaCreateRequirement => HandleCreate(role),
            AreaUpdateRequirement => await HandleUpdateAsync(userId, role, area),
            AreaDeleteRequirement => await HandleDeleteAsync(userId, role, area),
            AreaManageStaffRequirement => await HandleManageStaffAsync(userId, role, area),
            AreaViewStatsRequirement => await HandleViewStatsAsync(userId, role, area),
            _ => false
        };

        if (succeeded)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> HandleViewAsync(int userId, string role, Area? area)
    {
        if (!ViewableRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            return false;

        if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            return true;

        return area == null
            ? await HasAnyAreaAssignmentAsync(userId)
            : await BelongsToAreaAsync(userId, area.AreaID);
    }

    private static bool IsSystemManager(string role) =>
        string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

    private bool HandleCreate(string role) =>
        IsSystemManager(role);

    private async Task<bool> HandleUpdateAsync(int userId, string role, Area? area)
    {
        if (IsSystemManager(role))
            return true;

        if (area == null)
            return false;

        return await IsAreaManagerAsync(userId, area.AreaID);
    }

    private async Task<bool> HandleDeleteAsync(int userId, string role, Area? area)
    {
        if (!IsSystemManager(role))
            return false;

        if (area == null)
            return false;

        // Không còn booking active nào trong Area
        return !await HasActiveBookingInAreaAsync(area.AreaID);
    }

    private async Task<bool> HandleManageStaffAsync(int userId, string role, Area? area)
    {
        if (IsSystemManager(role))
            return true;

        if (area == null)
            return false;

        return await IsAreaManagerAsync(userId, area.AreaID);
    }

    private async Task<bool> HandleViewStatsAsync(int userId, string role, Area? area)
    {
        if (IsSystemManager(role))
            return true;

        if (area == null)
            return false;

        return await BelongsToAreaAsync(userId, area.AreaID);
    }

    private async Task<bool> IsAreaManagerAsync(int userId, int areaId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.AreaStaffs
            .IgnoreQueryFilters()
            .AnyAsync(asx => asx.UserID == userId
                           && asx.AreaID == areaId
                           && asx.RoleInArea.ToLower() == AreaStaffRoleConstants.Manager);
    }

    private async Task<bool> BelongsToAreaAsync(int userId, int areaId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.AreaStaffs
            .IgnoreQueryFilters()
            .AnyAsync(asx => asx.UserID == userId && asx.AreaID == areaId);
    }

    private async Task<bool> HasAnyAreaAssignmentAsync(int userId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.AreaStaffs
            .IgnoreQueryFilters()
            .AnyAsync(asx => asx.UserID == userId && !asx.IsDeleted);
    }

    private async Task<bool> HasActiveBookingInAreaAsync(int areaId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var locationIds = await db.AreaLocations
            .IgnoreQueryFilters()
            .Where(al => al.AreaID == areaId && !al.IsDeleted)
            .Select(al => al.LocationID)
            .ToListAsync();

        if (!locationIds.Any())
            return false;

        var activeStatuses = await db.BookingStatuses
            .Where(bs => !new[] { "Completed", "Cancelled", "Rejected" }.Contains(bs.Name))
            .Select(bs => bs.BookingStatusID)
            .ToListAsync();

        return await db.Bookings
            .IgnoreQueryFilters()
            .AnyAsync(b =>
                b.Car != null &&
                locationIds.Contains(b.Car.LocationID) &&
                activeStatuses.Contains(b.BookingStatusID) &&
                !b.IsDeleted);
    }
}
