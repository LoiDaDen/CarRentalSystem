using Microsoft.AspNetCore.Authorization;
using CarRentalSystem.Authorization;
using System.Security.Claims;
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class PhysicalContractsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAreaService _areaService;

        public PhysicalContractsController(
            ApplicationDbContext context,
            IAreaService areaService)
        {
            _context = context;
            _areaService = areaService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            int? areaId,
            string? q,
            string? typeCode,
            string? status,
            string? fileKind,
            int? carId,
            int? bookingId,
            DateTime? uploadedFrom,
            DateTime? uploadedTo)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            var scopedLocationIds = await ResolveScopedLocationIdsAsync(userId, role, areaId);
            if (scopedLocationIds == null) return Forbid();

            var query = BuildBaseQuery(scopedLocationIds);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(c =>
                    c.PhysicalContractID.ToString().Contains(keyword) ||
                    (c.Title != null && c.Title.Contains(keyword)) ||
                    (c.Note != null && c.Note.Contains(keyword)) ||
                    (c.Car != null && c.Car.CarName.Contains(keyword)) ||
                    (c.Car != null && c.Car.LicensePlate.Contains(keyword)) ||
                    (c.BookingID.HasValue && c.BookingID.Value.ToString().Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(typeCode))
                query = query.Where(c => c.PhysicalContractType != null && c.PhysicalContractType.Code == typeCode);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(c => c.Status == status);

            if (!string.IsNullOrWhiteSpace(fileKind))
                query = query.Where(c => c.FileKind == fileKind);

            if (carId.HasValue)
                query = query.Where(c => c.CarID == carId.Value);

            if (bookingId.HasValue)
                query = query.Where(c => c.BookingID == bookingId.Value);

            if (uploadedFrom.HasValue)
                query = query.Where(c => c.UploadedAt >= uploadedFrom.Value.Date);

            if (uploadedTo.HasValue)
            {
                var uploadedToExclusive = uploadedTo.Value.Date.AddDays(1);
                query = query.Where(c => c.UploadedAt < uploadedToExclusive);
            }

            await PopulateLookupsAsync(role, areaId);

            ViewBag.AreaId = areaId;
            ViewBag.Query = q;
            ViewBag.TypeCode = typeCode;
            ViewBag.Status = status;
            ViewBag.FileKind = fileKind;
            ViewBag.CarId = carId;
            ViewBag.BookingId = bookingId;
            ViewBag.UploadedFrom = uploadedFrom?.ToString("yyyy-MM-dd");
            ViewBag.UploadedTo = uploadedTo?.ToString("yyyy-MM-dd");
            ViewBag.CurrentRole = role;

            var contracts = await query
                .AsNoTracking()
                .OrderByDescending(c => c.UploadedAt)
                .Take(300)
                .ToListAsync();

            return View(contracts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upload(int? bookingId, int? carId, IFormFile? file, string? title, string? note, int? areaId)
        {
            TempData["Error"] = "Hợp đồng vật lý do chủ xe tự chuẩn bị và tự lưu ngoài hệ thống. App không còn nhận upload hợp đồng giấy.";
            return RedirectToAction(nameof(Index), new { areaId, bookingId, carId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Classify(int id, int? areaId)
        {
            TempData["Error"] = "Module hợp đồng vật lý chỉ còn đọc dữ liệu cũ; không phân loại tài liệu mới.";
            return RedirectToAction(nameof(Index), new { areaId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmType(int id, int contractTypeId, int? areaId)
        {
            TempData["Error"] = "Module hợp đồng vật lý chỉ còn đọc dữ liệu cũ; không xác nhận tài liệu mới.";
            return RedirectToAction(nameof(Index), new { areaId });
        }

        private IQueryable<PhysicalContract> BuildBaseQuery(List<int>? scopedLocationIds)
        {
            var query = _context.PhysicalContracts
                .Include(c => c.PhysicalContractType)
                .Include(c => c.Car).ThenInclude(c => c!.CarBrand)
                .Include(c => c.Booking).ThenInclude(b => b!.User)
                .Include(c => c.Booking).ThenInclude(b => b!.Car)
                .AsQueryable();

            if (scopedLocationIds != null)
            {
                query = query.Where(c =>
                    (c.Car != null && scopedLocationIds.Contains(c.Car.LocationID)) ||
                    (c.Booking != null && (
                        scopedLocationIds.Contains(c.Booking.PickupLocationID) ||
                        scopedLocationIds.Contains(c.Booking.ReturnLocationID))));
            }

            return query;
        }

        private async Task<List<int>?> ResolveScopedLocationIdsAsync(int userId, string role, int? areaId)
        {
            if (role == UserRoleConstants.Admin)
            {
                if (!areaId.HasValue)
                    return null;

                var areaExists = await _context.Areas.AnyAsync(a => a.AreaID == areaId.Value && !a.IsDeleted);
                if (!areaExists) return new List<int>();
                return await _areaService.GetLocationIdsForAreaAsync(areaId.Value);
            }

            if (areaId.HasValue)
            {
                var allowed = await _areaService.IsAreaAllowedForUserAsync(userId, areaId.Value);
                if (!allowed) return null;
                return await _areaService.GetLocationIdsForAreaAsync(areaId.Value);
            }

            return await _areaService.GetLocationIdsForUserAsync(userId);
        }

        private async Task PopulateLookupsAsync(string role, int? selectedAreaId)
        {
            ViewBag.Types = await _context.PhysicalContractTypes
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .ToListAsync();

            ViewBag.Statuses = await _context.PhysicalContracts
                .AsNoTracking()
                .Where(c => c.Status != "")
                .Select(c => c.Status)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            ViewBag.Areas = role == UserRoleConstants.Admin
                ? await _context.Areas.AsNoTracking().Where(a => !a.IsDeleted).OrderBy(a => a.Name).ToListAsync()
                : new List<Area>();
            ViewBag.SelectedAreaId = selectedAreaId;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }

        private string GetCurrentRole() =>
            User.FindFirst(ClaimTypes.Role)?.Value
         ?? User.FindFirst("role")?.Value ?? "";
    }
}
