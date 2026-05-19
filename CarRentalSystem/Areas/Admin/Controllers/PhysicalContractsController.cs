using System.Security.Claims;
using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class PhysicalContractsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAreaService _areaService;
        private readonly IFileUploadValidationService _fileValidation;
        private readonly IContractClassificationService _classifier;
        private readonly IAuditTrailService _auditTrail;

        public PhysicalContractsController(
            ApplicationDbContext context,
            IAreaService areaService,
            IFileUploadValidationService fileValidation,
            IContractClassificationService classifier,
            IAuditTrailService auditTrail)
        {
            _context = context;
            _areaService = areaService;
            _fileValidation = fileValidation;
            _classifier = classifier;
            _auditTrail = auditTrail;
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
        public async Task<IActionResult> Upload(int? bookingId, int? carId, IFormFile? file, string? title, string? note, int? areaId)
        {
            if (file == null || !await _fileValidation.IsAllowedImageOrPdfAsync(file, 12 * 1024 * 1024))
            {
                TempData["Error"] = "Chi chap nhan anh/PDF hop dong toi da 12MB.";
                return RedirectToAction(nameof(Index), new { areaId, bookingId, carId });
            }

            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            var scopedLocationIds = await ResolveScopedLocationIdsAsync(userId, role, areaId);
            if (scopedLocationIds == null) return Forbid();

            if (role != UserRoleConstants.Admin && !bookingId.HasValue && !carId.HasValue)
                return Forbid();

            if (carId.HasValue && !await CanAccessCarAsync(carId.Value, scopedLocationIds))
                return Forbid();

            if (bookingId.HasValue && !await CanAccessBookingAsync(bookingId.Value, scopedLocationIds))
                return Forbid();

            var fileHash = await ComputeFileHashAsync(file);
            var fileUrl = await SaveFileAsync(file, "uploads/contracts");
            var classification = await _classifier.ClassifyAsync(file.FileName, note);

            var contract = new PhysicalContract
            {
                BookingID = bookingId,
                CarID = carId,
                OwnerID = userId,
                Title = title,
                Note = note,
                FileUrl = fileUrl,
                FileKind = Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "Pdf" : "Image",
                SuggestedContractTypeCode = classification.SuggestedContractTypeCode,
                AiConfidence = classification.Confidence,
                AiReason = classification.Reason,
                Status = "PendingConfirmation",
                FileContentHash = fileHash,
                UploadedIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UploadedUserAgent = Request.Headers.UserAgent.ToString()
            };

            _context.PhysicalContracts.Add(contract);
            await _context.SaveChangesAsync();

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "UploadPhysicalContract",
                EntityType = nameof(PhysicalContract),
                EntityId = contract.PhysicalContractID,
                UserId = userId,
                Reason = "Physical contract uploaded with content hash.",
                NewValues = JsonSerializer.Serialize(new { contract.BookingID, contract.CarID, contract.FileKind, contract.FileContentHash })
            });

            TempData["Success"] = "Da upload hop dong. AI da goi y loai hop dong, vui long xac nhan.";
            return RedirectToAction(nameof(Index), new { areaId, bookingId, carId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Classify(int id, int? areaId)
        {
            var contract = await GetScopedContractAsync(id, areaId);
            if (contract == null) return NotFound();

            var result = await _classifier.ClassifyAsync(Path.GetFileName(contract.FileUrl), contract.Note);
            contract.SuggestedContractTypeCode = result.SuggestedContractTypeCode;
            contract.AiConfidence = result.Confidence;
            contract.AiReason = result.Reason;
            contract.Status = "PendingConfirmation";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Da phan loai lai tai lieu.";
            return RedirectToAction(nameof(Index), new { areaId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmType(int id, int contractTypeId, int? areaId)
        {
            var contract = await GetScopedContractAsync(id, areaId);
            if (contract == null) return NotFound();

            var exists = await _context.PhysicalContractTypes
                .AnyAsync(t => t.PhysicalContractTypeID == contractTypeId && t.IsActive);
            if (!exists) return BadRequest();

            contract.PhysicalContractTypeID = contractTypeId;
            contract.Status = "Confirmed";
            contract.ConfirmedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Da xac nhan loai hop dong.";
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

        private async Task<PhysicalContract?> GetScopedContractAsync(int id, int? areaId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            var scopedLocationIds = await ResolveScopedLocationIdsAsync(userId, role, areaId);
            if (scopedLocationIds == null) return null;

            return await BuildBaseQuery(scopedLocationIds)
                .FirstOrDefaultAsync(c => c.PhysicalContractID == id);
        }

        private async Task<bool> CanAccessCarAsync(int carId, List<int>? scopedLocationIds)
        {
            var query = _context.Cars.Where(c => c.CarID == carId);
            if (scopedLocationIds != null)
                query = query.Where(c => scopedLocationIds.Contains(c.LocationID));
            return await query.AnyAsync();
        }

        private async Task<bool> CanAccessBookingAsync(int bookingId, List<int>? scopedLocationIds)
        {
            var query = _context.Bookings.Where(b => b.BookingID == bookingId);
            if (scopedLocationIds != null)
            {
                query = query.Where(b =>
                    scopedLocationIds.Contains(b.PickupLocationID) ||
                    scopedLocationIds.Contains(b.ReturnLocationID));
            }

            return await query.AnyAsync();
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

        private static async Task<string> SaveFileAsync(IFormFile file, string relativeFolder)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(path);
            await file.CopyToAsync(stream);
            return $"/{relativeFolder}/{fileName}".Replace("\\", "/");
        }

        private static async Task<string> ComputeFileHashAsync(IFormFile file)
        {
            await using var stream = file.OpenReadStream();
            var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
