// CarRentalSystem/Services/AreaService.cs
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CarRentalSystem.Services;

public interface IAreaService
{
    Task<int?> GetAreaIdForUserAsync(int userId);
    Task<List<int>> GetAreaIdsForUserAsync(int userId);
    Task<List<int>> GetLocationIdsForAreaAsync(int areaId);
    Task<List<int>> GetLocationIdsForUserAsync(int userId);
    Task<List<int>> GetLocationIdsForUserAsync(int userId, string role);
    Task<List<Area>> GetAreasForUserAsync(int userId);
    Task<Area?> GetAreaByIdAsync(int areaId);
    Task<bool> IsUserAreaManagerAsync(int userId, int areaId);
    Task<bool> IsUserInAreaAsync(int userId, int areaId);
    Task<bool> CanManageAreaAsync(int userId, string userRole, int areaId);
    Task<bool> CanViewAreaAsync(int userId, string userRole, int areaId);
    Task<int?> GetAreaIdForLocationAsync(int locationId);
    Task<bool> IsAreaAllowedForUserAsync(int userId, int areaId);
}

public class AreaService : IAreaService
{
    private readonly ApplicationDbContext _context;

    public AreaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int?> GetAreaIdForUserAsync(int userId)
    {
        return await _context.AreaStaffs
            .Where(asx => asx.UserID == userId && !asx.IsDeleted)
            .Select(asx => asx.AreaID)
            .FirstOrDefaultAsync();
    }

    public async Task<List<int>> GetAreaIdsForUserAsync(int userId)
    {
        return await _context.AreaStaffs
            .Where(asx => asx.UserID == userId && !asx.IsDeleted)
            .Select(asx => asx.AreaID)
            .ToListAsync();
    }

    public async Task<List<int>> GetLocationIdsForAreaAsync(int areaId)
    {
        return await _context.AreaLocations
            .Where(al => al.AreaID == areaId && !al.IsDeleted)
            .Select(al => al.LocationID)
            .ToListAsync();
    }

    public async Task<List<int>> GetLocationIdsForUserAsync(int userId)
    {
        var areaIds = await GetAreaIdsForUserAsync(userId);
        if (!areaIds.Any())
            return new List<int>();

        return await _context.AreaLocations
            .Where(al => areaIds.Contains(al.AreaID) && !al.IsDeleted)
            .Select(al => al.LocationID)
            .ToListAsync();
    }

    public async Task<List<int>> GetLocationIdsForUserAsync(int userId, string role)
    {
        if (role == "Admin")
        {
            return await _context.Locations.Select(l => l.LocationID).ToListAsync();
        }
        return await GetLocationIdsForUserAsync(userId);
    }

    public async Task<List<Area>> GetAreasForUserAsync(int userId)
    {
        var areaIds = await GetAreaIdsForUserAsync(userId);
        if (!areaIds.Any())
            return new List<Area>();

        return await _context.Areas
            .Where(a => areaIds.Contains(a.AreaID) && !a.IsDeleted)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<Area?> GetAreaByIdAsync(int areaId)
    {
        return await _context.Areas
            .FirstOrDefaultAsync(a => a.AreaID == areaId && !a.IsDeleted);
    }

    public async Task<bool> IsAreaAllowedForUserAsync(int userId, int areaId)
    {
        return await _context.AreaStaffs
            .AnyAsync(asx =>
                asx.UserID == userId &&
                asx.AreaID == areaId &&
                !asx.IsDeleted);
    }

    public async Task<bool> IsUserAreaManagerAsync(int userId, int areaId)
    {
        return await _context.AreaStaffs
            .AnyAsync(asx =>
                asx.UserID == userId &&
                asx.AreaID == areaId &&
                asx.RoleInArea.ToLower() == AreaStaffRoleConstants.Manager &&
                !asx.IsDeleted);
    }

    /// <summary>
    /// Kiem tra user co quyen quan ly khu vuc (edit/staff/location).
    /// Admin = full access.
    /// Manager/Staff = chi khu vuc duoc gan va phai co RoleInArea == "manager".
    /// </summary>
    public async Task<bool> CanManageAreaAsync(int userId, string userRole, int areaId)
    {
        if (userRole == "Admin")
            return true;

        return await IsUserAreaManagerAsync(userId, areaId);
    }

    /// <summary>
    /// Kiem tra user co quyen xem khu vuc (chi can thuoc khu vuc do).
    /// Admin = full access.
    /// Manager/Staff = chi khu vuc duoc gan.
    /// </summary>
    public async Task<bool> CanViewAreaAsync(int userId, string userRole, int areaId)
    {
        if (userRole == "Admin")
            return true;

        return await IsUserInAreaAsync(userId, areaId);
    }

    public async Task<bool> IsUserInAreaAsync(int userId, int areaId)
    {
        return await _context.AreaStaffs
            .AnyAsync(asx =>
                asx.UserID == userId &&
                asx.AreaID == areaId &&
                !asx.IsDeleted);
    }

    public async Task<int?> GetAreaIdForLocationAsync(int locationId)
    {
        return await _context.AreaLocations
            .Where(al => al.LocationID == locationId && !al.IsDeleted)
            .Select(al => al.AreaID)
            .FirstOrDefaultAsync();
    }
}
