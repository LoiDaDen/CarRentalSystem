using System.Security.Claims;
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels.Cameras;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class CameraController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAreaService _areaService;
        private readonly ICameraAnprService _cameraAnprService;

        public CameraController(ApplicationDbContext context, IAreaService areaService, ICameraAnprService cameraAnprService)
        {
            _context = context;
            _areaService = areaService;
            _cameraAnprService = cameraAnprService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? areaId = null, int? locationId = null, string? status = null)
        {
            locationId = await NormalizeLocationForAreaAsync(areaId, locationId);
            await PopulateSelectorsAsync(areaId, locationId);
            var cameras = await ApplyCameraAccessFilter(_context.CameraDevices
                    .Include(c => c.Area)
                    .Include(c => c.Location)
                    .AsNoTracking())
                .Where(c => !areaId.HasValue || c.AreaID == areaId)
                .Where(c => !locationId.HasValue || c.LocationID == locationId)
                .Where(c => string.IsNullOrWhiteSpace(status) || c.Status == status)
                .OrderBy(c => c.Area!.Name)
                .ThenBy(c => c.Location!.Name)
                .ThenBy(c => c.Code)
                .Select(c => new CameraDeviceListItem
                {
                    CameraDeviceID = c.CameraDeviceID,
                    Code = c.Code,
                    Name = c.Name,
                    Provider = c.Provider,
                    AreaID = c.AreaID,
                    AreaName = c.Area != null ? c.Area.Name : null,
                    LocationID = c.LocationID,
                    LocationName = c.Location != null ? c.Location.Name : null,
                    DefaultDirection = c.DefaultDirection,
                    Status = c.Status,
                    LiveViewUrl = c.LiveViewUrl,
                    EventCountToday = c.GateEvents.Count(e => e.CapturedAt >= DateTime.UtcNow.Date)
                })
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            return View(new CameraIndexViewModel { Cameras = cameras });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CameraDeviceInputModel input)
        {
            if (!ModelState.IsValid || !await ValidateScopeAsync(input))
            {
                await PopulateSelectorsAsync(input.AreaID, input.LocationID);
                var model = new CameraIndexViewModel();
                return View(nameof(Index), model);
            }

            if (!await CanManageScopeAsync(input.AreaID, input.LocationID))
                return Forbid();

            var code = input.Code.Trim().ToUpperInvariant();
            if (await _context.CameraDevices.AnyAsync(c => c.Code == code))
            {
                ModelState.AddModelError(nameof(input.Code), "Mã camera đã tồn tại.");
                await PopulateSelectorsAsync(input.AreaID, input.LocationID);
                return View(nameof(Index), new CameraIndexViewModel());
            }

            var secret = _cameraAnprService.GenerateWebhookSecret();
            var camera = new CameraDevice
            {
                Code = code,
                Name = input.Name.Trim(),
                Provider = input.Provider?.Trim(),
                AreaID = input.AreaID,
                LocationID = input.LocationID,
                DefaultDirection = input.DefaultDirection,
                LiveViewUrl = input.LiveViewUrl?.Trim(),
                Status = input.Status,
                EncryptedWebhookSecret = _cameraAnprService.ProtectWebhookSecret(secret)
            };

            _context.CameraDevices.Add(camera);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã tạo camera {camera.Code}. Secret chỉ hiển thị một lần.";
            TempData["CameraSecret"] = secret;
            return RedirectToAction(nameof(Index), new { areaId = input.AreaID, locationId = input.LocationID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(CameraDeviceInputModel input)
        {
            var camera = await _context.CameraDevices.FirstOrDefaultAsync(c => c.CameraDeviceID == input.CameraDeviceID);
            if (camera == null) return NotFound();
            if (!await CanManageCameraAsync(camera)) return Forbid();

            if (!ModelState.IsValid || !await ValidateScopeAsync(input))
                return RedirectToAction(nameof(Index), new { areaId = input.AreaID, locationId = input.LocationID });

            var code = input.Code.Trim().ToUpperInvariant();
            if (await _context.CameraDevices.AnyAsync(c => c.CameraDeviceID != camera.CameraDeviceID && c.Code == code))
            {
                TempData["Error"] = "Mã camera đã tồn tại.";
                return RedirectToAction(nameof(Index), new { areaId = input.AreaID, locationId = input.LocationID });
            }

            camera.Code = code;
            camera.Name = input.Name.Trim();
            camera.Provider = input.Provider?.Trim();
            camera.AreaID = input.AreaID;
            camera.LocationID = input.LocationID;
            camera.DefaultDirection = input.DefaultDirection;
            camera.LiveViewUrl = input.LiveViewUrl?.Trim();
            camera.Status = input.Status;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật camera {camera.Code}.";
            return RedirectToAction(nameof(Index), new { areaId = input.AreaID, locationId = input.LocationID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateSecret(int id)
        {
            var camera = await _context.CameraDevices.FirstOrDefaultAsync(c => c.CameraDeviceID == id);
            if (camera == null) return NotFound();
            if (!await CanManageCameraAsync(camera)) return Forbid();

            var secret = _cameraAnprService.GenerateWebhookSecret();
            camera.EncryptedWebhookSecret = _cameraAnprService.ProtectWebhookSecret(secret);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cấp lại secret cho camera {camera.Code}.";
            TempData["CameraSecret"] = secret;
            return RedirectToAction(nameof(Index), new { areaId = camera.AreaID, locationId = camera.LocationID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var camera = await _context.CameraDevices.FirstOrDefaultAsync(c => c.CameraDeviceID == id);
            if (camera == null) return NotFound();
            if (!await CanManageCameraAsync(camera)) return Forbid();

            _context.CameraDevices.Remove(camera);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa camera {camera.Code}.";
            return RedirectToAction(nameof(Index), new { areaId = camera.AreaID, locationId = camera.LocationID });
        }

        [HttpGet]
        public async Task<IActionResult> Monitor(int? areaId = null, int? locationId = null, int? cameraId = null, string? plate = null, string? direction = null, string? matchStatus = null)
        {
            locationId = await NormalizeLocationForAreaAsync(areaId, locationId);
            await PopulateSelectorsAsync(areaId, locationId, includeCameras: true);
            var today = DateTime.UtcNow.Date;
            var normalizedPlate = CarRentalSystem.Helpers.LicensePlateNormalizer.Normalize(plate);

            var query = ApplyEventAccessFilter(_context.CameraGateEvents
                    .Include(e => e.CameraDevice)
                    .Include(e => e.Car)
                    .Include(e => e.Area)
                    .Include(e => e.Location)
                    .AsNoTracking())
                .Where(e => !areaId.HasValue || e.AreaID == areaId)
                .Where(e => !locationId.HasValue || e.LocationID == locationId)
                .Where(e => !cameraId.HasValue || e.CameraDeviceID == cameraId)
                .Where(e => string.IsNullOrWhiteSpace(normalizedPlate) || e.LicensePlateNormalized.Contains(normalizedPlate))
                .Where(e => string.IsNullOrWhiteSpace(direction) || e.Direction == direction)
                .Where(e => string.IsNullOrWhiteSpace(matchStatus) || e.MatchStatus == matchStatus);

            var events = await query
                .OrderByDescending(e => e.CapturedAt)
                .Take(200)
                .Select(e => new CameraGateEventListItem
                {
                    CameraGateEventID = e.CameraGateEventID,
                    CameraCode = e.CameraDevice != null ? e.CameraDevice.Code : "",
                    CameraName = e.CameraDevice != null ? e.CameraDevice.Name : "",
                    LicensePlateRaw = e.LicensePlateRaw,
                    LicensePlateNormalized = e.LicensePlateNormalized,
                    Direction = e.Direction,
                    Confidence = e.Confidence,
                    CapturedAt = e.CapturedAt,
                    SnapshotUrl = e.SnapshotUrl,
                    CarID = e.CarID,
                    CarName = e.Car != null ? e.Car.CarName : null,
                    CarLicensePlate = e.Car != null ? e.Car.LicensePlate : null,
                    AreaName = e.Area != null ? e.Area.Name : null,
                    LocationName = e.Location != null ? e.Location.Name : null,
                    MatchStatus = e.MatchStatus,
                    FailureReason = e.FailureReason,
                    LiveViewUrl = e.CameraDevice != null ? e.CameraDevice.LiveViewUrl : null
                })
                .ToListAsync();

            var accessibleEvents = ApplyEventAccessFilter(_context.CameraGateEvents.AsNoTracking());
            var model = new CameraMonitorViewModel
            {
                Events = events,
                TodayCount = await accessibleEvents.CountAsync(e => e.CapturedAt >= today),
                UnknownPlateCount = await accessibleEvents.CountAsync(e => e.CapturedAt >= today && e.MatchStatus == CameraGateMatchStatusConstants.UnknownPlate),
                LowConfidenceCount = await accessibleEvents.CountAsync(e => e.CapturedAt >= today && e.MatchStatus == CameraGateMatchStatusConstants.LowConfidence),
                AreaMismatchCount = await accessibleEvents.CountAsync(e => e.CapturedAt >= today && e.MatchStatus == CameraGateMatchStatusConstants.AreaMismatch),
                InsideCount = await ApplyPresenceAccessFilter(_context.VehicleAreaPresences.AsNoTracking()).CountAsync(p => p.IsInside)
            };

            ViewBag.CurrentAreaId = areaId;
            ViewBag.CurrentLocationId = locationId;
            ViewBag.CurrentCameraId = cameraId;
            ViewBag.CurrentPlate = plate;
            ViewBag.CurrentDirection = direction;
            ViewBag.CurrentMatchStatus = matchStatus;
            return View(model);
        }

        private IQueryable<CameraDevice> ApplyCameraAccessFilter(IQueryable<CameraDevice> query)
        {
            if (GetCurrentRole() == UserRoleConstants.Admin) return query;
            var userId = GetCurrentUserId();
            var areaIds = _context.AreaStaffs.Where(a => a.UserID == userId).Select(a => a.AreaID);
            var locationIds = _context.AreaLocations.Where(al => areaIds.Contains(al.AreaID)).Select(al => al.LocationID);
            return query.Where(c => (c.AreaID.HasValue && areaIds.Contains(c.AreaID.Value)) || (c.LocationID.HasValue && locationIds.Contains(c.LocationID.Value)));
        }

        private IQueryable<CameraGateEvent> ApplyEventAccessFilter(IQueryable<CameraGateEvent> query)
        {
            if (GetCurrentRole() == UserRoleConstants.Admin) return query;
            var userId = GetCurrentUserId();
            var areaIds = _context.AreaStaffs.Where(a => a.UserID == userId).Select(a => a.AreaID);
            var locationIds = _context.AreaLocations.Where(al => areaIds.Contains(al.AreaID)).Select(al => al.LocationID);
            return query.Where(e => (e.AreaID.HasValue && areaIds.Contains(e.AreaID.Value)) || (e.LocationID.HasValue && locationIds.Contains(e.LocationID.Value)));
        }

        private IQueryable<VehicleAreaPresence> ApplyPresenceAccessFilter(IQueryable<VehicleAreaPresence> query)
        {
            if (GetCurrentRole() == UserRoleConstants.Admin) return query;
            var userId = GetCurrentUserId();
            var areaIds = _context.AreaStaffs.Where(a => a.UserID == userId).Select(a => a.AreaID);
            var locationIds = _context.AreaLocations.Where(al => areaIds.Contains(al.AreaID)).Select(al => al.LocationID);
            return query.Where(p => (p.AreaID.HasValue && areaIds.Contains(p.AreaID.Value)) || (p.LocationID.HasValue && locationIds.Contains(p.LocationID.Value)));
        }

        private async Task<bool> CanManageCameraAsync(CameraDevice camera)
        {
            return await CanManageScopeAsync(camera.AreaID, camera.LocationID);
        }

        private async Task<bool> CanManageScopeAsync(int? areaId, int? locationId)
        {
            var role = GetCurrentRole();
            if (role == UserRoleConstants.Admin) return true;
            if (role != UserRoleConstants.Manager) return false;

            var userId = GetCurrentUserId();
            if (areaId.HasValue)
                return await _areaService.CanManageAreaAsync(userId, role, areaId.Value);

            if (locationId.HasValue)
            {
                var resolvedAreaId = await _areaService.GetAreaIdForLocationAsync(locationId.Value);
                return resolvedAreaId.HasValue && await _areaService.CanManageAreaAsync(userId, role, resolvedAreaId.Value);
            }

            return false;
        }

        private async Task<bool> ValidateScopeAsync(CameraDeviceInputModel input)
        {
            if (!input.AreaID.HasValue)
            {
                ModelState.AddModelError(nameof(input.AreaID), "Phải chọn khu vực trước khi chọn địa điểm.");
                return false;
            }

            if (input.AreaID.HasValue && input.LocationID.HasValue)
            {
                var mapped = await _context.AreaLocations.AnyAsync(al => al.AreaID == input.AreaID.Value && al.LocationID == input.LocationID.Value);
                if (!mapped)
                {
                    ModelState.AddModelError(nameof(input.LocationID), "Địa điểm không thuộc khu vực đã chọn.");
                    return false;
                }
            }

            return true;
        }

        private async Task<int?> NormalizeLocationForAreaAsync(int? areaId, int? locationId)
        {
            if (!areaId.HasValue || !locationId.HasValue)
                return null;

            var mapped = await _context.AreaLocations
                .AsNoTracking()
                .AnyAsync(al => al.AreaID == areaId.Value && al.LocationID == locationId.Value && !al.IsDeleted);

            return mapped ? locationId : null;
        }

        private async Task PopulateSelectorsAsync(int? areaId, int? locationId, bool includeCameras = false)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            var areasQuery = _context.Areas.AsNoTracking().OrderBy(a => a.Name).AsQueryable();
            var locationsQuery = _context.Locations.AsNoTracking().OrderBy(l => l.Name).AsQueryable();
            var areaLocationsQuery = _context.AreaLocations.AsNoTracking().Where(al => !al.IsDeleted).AsQueryable();
            if (role != UserRoleConstants.Admin)
            {
                var areaIds = await _areaService.GetAreaIdsForUserAsync(userId);
                var locationIds = await _areaService.GetLocationIdsForUserAsync(userId);
                areasQuery = areasQuery.Where(a => areaIds.Contains(a.AreaID));
                locationsQuery = locationsQuery.Where(l => locationIds.Contains(l.LocationID));
                areaLocationsQuery = areaLocationsQuery.Where(al => areaIds.Contains(al.AreaID) && locationIds.Contains(al.LocationID));
            }

            var allLocationsQuery = locationsQuery;
            if (areaId.HasValue)
            {
                var scopedLocationIds = areaLocationsQuery
                    .Where(al => al.AreaID == areaId.Value)
                    .Select(al => al.LocationID);
                locationsQuery = locationsQuery.Where(l => scopedLocationIds.Contains(l.LocationID));
            }

            ViewBag.Areas = await areasQuery.ToListAsync();
            ViewBag.Locations = await locationsQuery.ToListAsync();
            ViewBag.AreaLocations = await areaLocationsQuery
                .Select(al => new { al.AreaID, al.LocationID })
                .ToListAsync();
            ViewBag.LocationOptions = await allLocationsQuery
                .Select(l => new { l.LocationID, l.Name })
                .ToListAsync();
            ViewBag.CurrentAreaId = areaId;
            ViewBag.CurrentLocationId = locationId;
            ViewBag.Directions = new[] { CameraGateDirectionConstants.In, CameraGateDirectionConstants.Out, CameraGateDirectionConstants.Unknown };
            ViewBag.MatchStatuses = new[]
            {
                CameraGateMatchStatusConstants.Matched,
                CameraGateMatchStatusConstants.UnknownPlate,
                CameraGateMatchStatusConstants.LowConfidence,
                CameraGateMatchStatusConstants.AreaMismatch,
                CameraGateMatchStatusConstants.InvalidScope
            };

            if (includeCameras)
                ViewBag.Cameras = await ApplyCameraAccessFilter(_context.CameraDevices.AsNoTracking()).OrderBy(c => c.Code).ToListAsync();
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        }

        private string GetCurrentRole()
        {
            return User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "";
        }
    }
}
