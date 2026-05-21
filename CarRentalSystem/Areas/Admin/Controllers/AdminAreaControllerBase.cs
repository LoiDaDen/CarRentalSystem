// CarRentalSystem/Areas/Admin/Controllers/AdminAreaControllerBase.cs
// Base controller cho cac Area Admin controllers — cung cap helper ve Area
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Areas.Admin.Controllers;

/// <summary>
/// Base controller cho cac Area Admin (Manager/Staff).
/// Cung cap helper doc khu vuc hien tai tu cookie va tra ve area hop le.
/// </summary>
public abstract class AdminAreaControllerBase : Controller
{
    protected readonly IAreaService _areaService;
    protected const string CurrentAreaCookie = "CurrentAreaId";

    protected AdminAreaControllerBase(IAreaService areaService)
    {
        _areaService = areaService;
    }

    /// <summary>
    /// Tra ve AreaID hien tai tu cookie.
    /// Nếu cookie không có hoặc không hợp lệ -> trả về area đầu tiên của user.
    /// Nếu user không có area nào -> trả về null.
    /// </summary>
    protected async Task<int?> GetCurrentAreaIdAsync(int userId)
    {
        if (Request.Cookies.TryGetValue(CurrentAreaCookie, out var cookieValue)
            && int.TryParse(cookieValue, out var cookieAreaId)
            && cookieAreaId > 0)
        {
            return cookieAreaId;
        }

        // Fallback: lay area dau tien cua user
        var areaIds = await _areaService.GetAreaIdsForUserAsync(userId);
        return areaIds.Count > 0 ? areaIds[0] : null;
    }

    /// <summary>
    /// Trả về area ID từ request query string, nếu không có thì từ cookie/fallback.
    /// Dung cho cac action co tham so areaId.
    /// </summary>
    protected async Task<int?> ResolveAreaIdAsync(int userId, int? queryAreaId)
    {
        if (queryAreaId.HasValue && queryAreaId.Value > 0)
            return queryAreaId;

        return await GetCurrentAreaIdAsync(userId);
    }

    /// <summary>
    /// Tra ve true neu areaId hop le va thuoc ve user.
    /// </summary>
    protected async Task<bool> IsValidAreaForUserAsync(int userId, int areaId)
    {
        if (areaId <= 0) return false;
        return await _areaService.IsAreaAllowedForUserAsync(userId, areaId);
    }

    /// <summary>
    /// Tra ve danh sach area cua user hien tai (cho dropdown chon khu vuc).
    /// </summary>
    protected async Task<List<CarRentalSystem.Models.Area>> GetUserAreasAsync(int userId)
    {
        return await _areaService.GetAreasForUserAsync(userId);
    }

    /// <summary>
    /// Lay userId tu ClaimsPrincipal.
    /// </summary>
    protected int GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? User.FindFirst("UserID");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }

    /// <summary>
    /// Lay role tu ClaimsPrincipal.
    /// </summary>
    protected string GetCurrentRole() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
     ?? User.FindFirst("role")?.Value ?? "";
}
