using System.Security.Claims;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class PhysicalContractsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadValidationService _fileValidation;
        private readonly IContractClassificationService _classifier;
        private readonly IAuditTrailService _auditTrail;

        public PhysicalContractsController(
            ApplicationDbContext context,
            IFileUploadValidationService fileValidation,
            IContractClassificationService classifier,
            IAuditTrailService auditTrail)
        {
            _context = context;
            _fileValidation = fileValidation;
            _classifier = classifier;
            _auditTrail = auditTrail;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? typeCode, int? carId, int? bookingId)
        {
            var userId = GetCurrentUserId();
            var query = _context.PhysicalContracts
                .Include(c => c.PhysicalContractType)
                .Include(c => c.Car).ThenInclude(c => c!.CarBrand)
                .Include(c => c.Booking)
                .Where(c => c.OwnerID == userId || User.IsInRole(UserRoleConstants.Admin))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(typeCode))
                query = query.Where(c => c.PhysicalContractType != null && c.PhysicalContractType.Code == typeCode);
            if (carId.HasValue)
                query = query.Where(c => c.CarID == carId.Value);
            if (bookingId.HasValue)
                query = query.Where(c => c.BookingID == bookingId.Value);

            ViewBag.Types = await _context.PhysicalContractTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
            return View(await query.OrderByDescending(c => c.UploadedAt).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int? bookingId, int? carId, IFormFile file, string? title, string? note)
        {
            if (file == null || !await _fileValidation.IsAllowedImageOrPdfAsync(file, 12 * 1024 * 1024))
            {
                TempData["Error"] = "Chỉ chấp nhận ảnh/PDF hợp đồng tối đa 12MB.";
                return RedirectToAction(nameof(Index));
            }

            var userId = GetCurrentUserId();
            if (carId.HasValue)
            {
                var ownsCar = await _context.Cars.AnyAsync(c => c.CarID == carId && c.OwnerID == userId);
                if (!ownsCar && !User.IsInRole(UserRoleConstants.Admin)) return Forbid();
            }

            if (bookingId.HasValue)
            {
                var ownsBooking = await _context.Bookings.AnyAsync(b => b.BookingID == bookingId && (b.UserID == userId || b.Car!.OwnerID == userId));
                if (!ownsBooking && !User.IsInRole(UserRoleConstants.Admin)) return Forbid();
            }

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
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { contract.BookingID, contract.CarID, contract.FileKind, contract.FileContentHash })
            });
            TempData["Success"] = "Đã upload hợp đồng. AI đã gợi ý loại hợp đồng, vui lòng xác nhận.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Classify(int id)
        {
            var contract = await GetOwnedContractAsync(id);
            if (contract == null) return NotFound();

            var result = await _classifier.ClassifyAsync(Path.GetFileName(contract.FileUrl), contract.Note);
            contract.SuggestedContractTypeCode = result.SuggestedContractTypeCode;
            contract.AiConfidence = result.Confidence;
            contract.AiReason = result.Reason;
            contract.Status = "PendingConfirmation";
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmType(int id, int contractTypeId)
        {
            var contract = await GetOwnedContractAsync(id);
            if (contract == null) return NotFound();

            var exists = await _context.PhysicalContractTypes.AnyAsync(t => t.PhysicalContractTypeID == contractTypeId && t.IsActive);
            if (!exists) return BadRequest();

            contract.PhysicalContractTypeID = contractTypeId;
            contract.Status = "Confirmed";
            contract.ConfirmedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xác nhận loại hợp đồng.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<PhysicalContract?> GetOwnedContractAsync(int id)
        {
            var userId = GetCurrentUserId();
            return await _context.PhysicalContracts
                .FirstOrDefaultAsync(c => c.PhysicalContractID == id && (c.OwnerID == userId || User.IsInRole(UserRoleConstants.Admin)));
        }

        private int GetCurrentUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(id, out var userId)) throw new InvalidOperationException("User id is missing.");
            return userId;
        }

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
