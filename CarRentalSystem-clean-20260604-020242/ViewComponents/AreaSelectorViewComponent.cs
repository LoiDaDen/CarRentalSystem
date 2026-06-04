// CarRentalSystem/ViewComponents/AreaSelectorViewComponent.cs
// ViewComponent hien thi dropdown chon khu vuc trong sidebar
// Doc CurrentAreaId tu cookie, tra ve danh sach area cua user hien tai
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CarRentalSystem.ViewComponents;

[ViewComponent(Name = "AreaSelector")]
public class AreaSelectorViewComponent : ViewComponent
{
    private readonly IAreaService _areaService;
    private const string CookieName = "CurrentAreaId";

    public AreaSelectorViewComponent(IAreaService areaService)
    {
        _areaService = areaService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = GetUserId();
        var role = GetUserRole();

        if (userId <= 0 || role == "")
            return View(new AreaSelectorVm { Areas = new List<AreaItem>(), CurrentAreaId = null, IsManager = false });

        var areas = await _areaService.GetAreasForUserAsync(userId);

        // Doc areaId tu cookie
        int? currentAreaId = null;
        if (Request.Cookies.TryGetValue(CookieName, out var cookieVal)
            && int.TryParse(cookieVal, out var parsed)
            && parsed > 0)
        {
            currentAreaId = parsed;
        }

        // Neu cookie khong hop le (area khong thuoc user nua) -> reset
        if (currentAreaId.HasValue && !areas.Any(a => a.AreaID == currentAreaId.Value))
        {
            currentAreaId = areas.FirstOrDefault()?.AreaID;
        }

        var vm = new AreaSelectorVm
        {
            Areas = areas.Select(a => new AreaItem
            {
                AreaID = a.AreaID,
                Name = a.Name,
                Province = a.Province,
                Status = a.Status
            }).ToList(),
            CurrentAreaId = currentAreaId,
            IsManager = role == "Manager"
        };

        return View(vm);
    }

    private int GetUserId()
    {
        var user = User as ClaimsPrincipal;
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier)
                 ?? user?.FindFirst("sub")
                 ?? user?.FindFirst("UserID");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }

    private string GetUserRole()
    {
        var user = User as ClaimsPrincipal;
        return user?.FindFirst(ClaimTypes.Role)?.Value
         ?? user?.FindFirst("role")?.Value ?? "";
    }

    public class AreaSelectorVm
    {
        public List<AreaItem> Areas { get; set; } = new();
        public int? CurrentAreaId { get; set; }
        public bool IsManager { get; set; }
    }

    public class AreaItem
    {
        public int AreaID { get; set; }
        public string Name { get; set; } = "";
        public string Province { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
