using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class HandoverController : Controller
    {
        private const long MaxEvidenceFileSize = 5 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEvidenceHashService _evidenceHash;
        private readonly IAuditTrailService _auditTrail;
        private readonly IFileUploadValidationService _fileUploadValidation;
        private readonly IAreaService _areaService;

        public HandoverController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IEvidenceHashService evidenceHash,
            IAuditTrailService auditTrail,
            IFileUploadValidationService fileUploadValidation,
            IAreaService areaService)
        {
            _context = context;
            _env = env;
            _evidenceHash = evidenceHash;
            _auditTrail = auditTrail;
            _fileUploadValidation = fileUploadValidation;
            _areaService = areaService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int bookingId)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var booking = await LoadBookingForHandoverAsync(bookingId);
            if (booking == null || !await CanAccessBookingAsync(booking, userId))
                return NotFound("Không tìm thấy chuyến đi.");

            ViewBag.Booking = booking;
            ViewBag.HandoverRecords = await _context.HandoverRecords
                .Where(h => h.BookingID == bookingId && !h.IsDeleted)
                .OrderBy(h => h.RecordDate)
                .ToListAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ViewRecord(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var record = await _context.HandoverRecords
                .Include(h => h.Booking)
                .ThenInclude(b => b.Car)
                .ThenInclude(c => c!.CarBrand)
                .FirstOrDefaultAsync(h => h.HandoverID == id && !h.IsDeleted);

            if (record?.Booking == null || !await CanAccessBookingAsync(record.Booking, userId))
                return NotFound("Không tìm thấy biên bản.");

            return View(record);
        }

        [HttpGet]
        public async Task<IActionResult> Capture(int bookingId, string type)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var booking = await LoadBookingForHandoverAsync(bookingId);
            if (booking == null || !await CanAccessBookingAsync(booking, userId))
                return NotFound("Không tìm thấy chuyến đi.");

            var normalizedType = NormalizeType(type);
            if (!IsStatusAllowedForHandover(booking.BookingStatus?.Name, normalizedType))
            {
                TempData["ErrorMessage"] = "Trạng thái đơn thuê không hợp lệ để lập biên bản bàn giao.";
                return RedirectToAction("Details", "Booking", new { id = bookingId });
            }

            if (normalizedType == "CheckIn" && RequiresSignedDigitalContract(booking) && !HasSignedDigitalContract(booking))
            {
                TempData["ErrorMessage"] = "Vui lòng ký hợp đồng số trước khi lập biên bản nhận xe.";
                return RedirectToAction("Details", "Booking", new { id = bookingId });
            }

            var alreadyCaptured = await _context.HandoverRecords
                .AnyAsync(h => h.BookingID == bookingId && h.Type == normalizedType && !h.IsDeleted);
            if (alreadyCaptured)
            {
                TempData["ErrorMessage"] = $"Biên bản {normalizedType} đã tồn tại.";
                return RedirectToAction("Details", "Booking", new { id = bookingId });
            }

            ViewBag.Type = normalizedType;
            ViewBag.Booking = booking;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            int BookingID,
            string Type,
            int Odo,
            string FuelLevel,
            IFormFile? imgFront,
            IFormFile? imgBack,
            IFormFile? imgLeft,
            IFormFile? imgRight,
            IFormFile? imgInside,
            double? GeoLatitude,
            double? GeoLongitude,
            string? SignatureBase64)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var booking = await LoadBookingForHandoverAsync(BookingID);
            if (booking == null || !await CanAccessBookingAsync(booking, userId))
                return NotFound();

            var normalizedType = NormalizeType(Type);
            if (!IsStatusAllowedForHandover(booking.BookingStatus?.Name, normalizedType))
            {
                TempData["ErrorMessage"] = "Trạng thái đơn thuê không hợp lệ để lập biên bản bàn giao.";
                return RedirectToAction("Details", "Booking", new { id = BookingID });
            }

            if (normalizedType == "CheckIn" && RequiresSignedDigitalContract(booking) && !HasSignedDigitalContract(booking))
            {
                TempData["ErrorMessage"] = "Vui lòng ký hợp đồng số trước khi lập biên bản nhận xe.";
                return RedirectToAction("Details", "Booking", new { id = BookingID });
            }

            var alreadyCaptured = await _context.HandoverRecords
                .AnyAsync(h => h.BookingID == BookingID && h.Type == normalizedType && !h.IsDeleted);
            if (alreadyCaptured)
            {
                TempData["ErrorMessage"] = $"Biên bản {normalizedType} đã tồn tại.";
                return RedirectToAction("Details", "Booking", new { id = BookingID });
            }

            var uploadPath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "Handovers");
            Directory.CreateDirectory(uploadPath);

            HandoverRecord record;
            try
            {
                record = new HandoverRecord
                {
                    BookingID = BookingID,
                    Type = normalizedType,
                    RecordDate = DateTime.UtcNow,
                    Odo = Odo,
                    FuelLevel = FuelLevel,
                    RecordedByUserId = userId,
                    FrontImgUrl = await SaveEvidenceFileAsync(uploadPath, imgFront),
                    BackImgUrl = await SaveEvidenceFileAsync(uploadPath, imgBack),
                    LeftImgUrl = await SaveEvidenceFileAsync(uploadPath, imgLeft),
                    RightImgUrl = await SaveEvidenceFileAsync(uploadPath, imgRight),
                    InsideImgUrl = await SaveEvidenceFileAsync(uploadPath, imgInside),
                    GeoLatitude = GeoLatitude,
                    GeoLongitude = GeoLongitude
                };
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Capture", new { bookingId = BookingID, type = normalizedType });
            }

            var signatureResult = await SaveSignatureAsync(uploadPath, SignatureBase64);
            if (!signatureResult.IsValid)
            {
                TempData["ErrorMessage"] = "Chữ ký không hợp lệ. Vui lòng ký lại.";
                return RedirectToAction("Capture", new { bookingId = BookingID, type = normalizedType });
            }

            record.SignatureUrl = signatureResult.Url;
            if (!string.IsNullOrWhiteSpace(SignatureBase64))
                record.CustomerSignatureHash = _evidenceHash.ComputeTextHash(SignatureBase64);

            record.EvidenceHash = _evidenceHash.ComputeHandoverEvidenceHash(record);
            _context.HandoverRecords.Add(record);
            await _context.SaveChangesAsync();

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "CreateHandoverEvidence",
                EntityType = nameof(HandoverRecord),
                EntityId = record.HandoverID,
                UserId = userId,
                Reason = $"Handover {record.Type} evidence captured.",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { record.BookingID, record.Type, record.EvidenceHash })
            });

            TempData["SuccessMessage"] = $"Biên bản {record.Type} đã được lưu trữ an toàn.";
            return RedirectToAction("Details", "Booking", new { id = BookingID });
        }

        private async Task<Booking?> LoadBookingForHandoverAsync(int bookingId)
        {
            return await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Car)
                .ThenInclude(c => c!.CarBrand)
                .Include(b => b.DigitalContract)
                .Include(b => b.BookingTransaction)
                .ThenInclude(t => t!.DigitalContract)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
        }

        private static bool HasSignedDigitalContract(Booking booking)
        {
            var contract = booking.DigitalContract ?? booking.BookingTransaction?.DigitalContract;
            return contract?.IsSignedByCustomer == true;
        }

        private static bool RequiresSignedDigitalContract(Booking booking)
        {
            return CarContractSigningModes.Normalize(booking.Car?.ContractSigningMode) == CarContractSigningModes.Digital;
        }

        private async Task<string?> SaveEvidenceFileAsync(string uploadPath, IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            if (!await _fileUploadValidation.IsAllowedImageAsync(file, MaxEvidenceFileSize))
                throw new InvalidOperationException($"File {file.FileName} không hợp lệ. Chỉ chấp nhận JPG, PNG, WEBP đúng định dạng và tối đa 5MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadPath, fileName);
            await using var stream = new FileStream(filePath, FileMode.CreateNew);
            await file.CopyToAsync(stream);
            return $"/SecureFile/GetSecureImage?folder=Handovers&fileName={fileName}";
        }

        private static async Task<(bool IsValid, string? Url)> SaveSignatureAsync(string uploadPath, string? signatureBase64)
        {
            if (string.IsNullOrWhiteSpace(signatureBase64))
                return (true, null);

            var commaIndex = signatureBase64.IndexOf(",", StringComparison.Ordinal);
            var base64Data = commaIndex >= 0 ? signatureBase64[(commaIndex + 1)..] : signatureBase64;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64Data);
            }
            catch (FormatException)
            {
                return (false, null);
            }

            var signFileName = $"{Guid.NewGuid()}_sig.png";
            var signPath = Path.Combine(uploadPath, signFileName);
            await System.IO.File.WriteAllBytesAsync(signPath, bytes);
            return (true, $"/SecureFile/GetSecureImage?folder=Handovers&fileName={signFileName}");
        }

        private async Task<bool> CanAccessBookingAsync(Booking booking, int userId)
        {
            if (booking.UserID == userId) return true;
            if (User.IsInRole(UserRoleConstants.Admin)) return true;

            if (!User.IsInRole(UserRoleConstants.Manager) && !User.IsInRole(UserRoleConstants.Staff))
                return false;

            var locationId = booking.PickupLocationID > 0 ? booking.PickupLocationID : booking.Car?.LocationID;
            if (!locationId.HasValue || locationId.Value <= 0)
                return false;

            var areaId = await _areaService.GetAreaIdForLocationAsync(locationId.Value);
            return areaId.HasValue && await _areaService.IsAreaAllowedForUserAsync(userId, areaId.Value);
        }

        private static string NormalizeType(string? type) =>
            string.Equals(type, "CheckOut", StringComparison.OrdinalIgnoreCase) ? "CheckOut" : "CheckIn";

        private static bool IsStatusAllowedForHandover(string? status, string type)
        {
            return type == "CheckOut"
                ? string.Equals(status, BookingStatusConstants.Active, StringComparison.OrdinalIgnoreCase)
                : string.Equals(status, BookingStatusConstants.Confirmed, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdString, out userId);
        }
    }
}
