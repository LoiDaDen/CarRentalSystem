using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.ViewModels;
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
            if (booking == null || !await CanViewBookingAsync(booking, userId))
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
                .ThenInclude(b => b!.User)
                .Include(h => h.Booking)
                .ThenInclude(b => b.Car)
                .ThenInclude(c => c!.CarBrand)
                .Include(h => h.InventoryItems)
                .FirstOrDefaultAsync(h => h.HandoverID == id && !h.IsDeleted);

            if (record?.Booking == null || !await CanViewBookingAsync(record.Booking, userId))
                return NotFound("Không tìm thấy biên bản.");

            return View(record);
        }

        [HttpGet]
        public async Task<IActionResult> Capture(int bookingId, string type)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var booking = await LoadBookingForHandoverAsync(bookingId);
            if (booking == null || !await CanCaptureBookingAsync(booking, userId))
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

            return View(await BuildCaptureViewModelAsync(booking, normalizedType));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(HandoverCaptureViewModel model)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var booking = await LoadBookingForHandoverAsync(model.BookingID);
            if (booking == null || !await CanCaptureBookingAsync(booking, userId))
                return NotFound();

            var normalizedType = NormalizeType(model.Type);
            if (!IsStatusAllowedForHandover(booking.BookingStatus?.Name, normalizedType))
            {
                TempData["ErrorMessage"] = "Trạng thái đơn thuê không hợp lệ để lập biên bản bàn giao.";
                return RedirectToAction("Details", "Booking", new { id = model.BookingID });
            }

            if (normalizedType == "CheckIn" && RequiresSignedDigitalContract(booking) && !HasSignedDigitalContract(booking))
            {
                TempData["ErrorMessage"] = "Vui lòng ký hợp đồng số trước khi lập biên bản nhận xe.";
                return RedirectToAction("Details", "Booking", new { id = model.BookingID });
            }

            var alreadyCaptured = await _context.HandoverRecords
                .AnyAsync(h => h.BookingID == model.BookingID && h.Type == normalizedType && !h.IsDeleted);
            if (alreadyCaptured)
            {
                TempData["ErrorMessage"] = $"Biên bản {normalizedType} đã tồn tại.";
                return RedirectToAction("Details", "Booking", new { id = model.BookingID });
            }

            if (!HasRequiredEvidenceImages(model) || string.IsNullOrWhiteSpace(model.SignatureBase64))
            {
                TempData["ErrorMessage"] = "Vui lòng chụp đủ 5 ảnh và ký xác nhận trước khi gửi biên bản.";
                return RedirectToAction("Capture", new { bookingId = model.BookingID, type = normalizedType });
            }

            var uploadPath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "Handovers");
            Directory.CreateDirectory(uploadPath);

            HandoverRecord record;
            try
            {
                record = new HandoverRecord
                {
                    BookingID = model.BookingID,
                    Type = normalizedType,
                    RecordDate = DateTime.UtcNow,
                    Odo = model.Odo,
                    FuelLevel = model.FuelLevel,
                    RecordedByUserId = userId,
                    FrontImgUrl = await SaveEvidenceFileAsync(uploadPath, model.ImgFront, booking.UserID, "Handovers"),
                    BackImgUrl = await SaveEvidenceFileAsync(uploadPath, model.ImgBack, booking.UserID, "Handovers"),
                    LeftImgUrl = await SaveEvidenceFileAsync(uploadPath, model.ImgLeft, booking.UserID, "Handovers"),
                    RightImgUrl = await SaveEvidenceFileAsync(uploadPath, model.ImgRight, booking.UserID, "Handovers"),
                    InsideImgUrl = await SaveEvidenceFileAsync(uploadPath, model.ImgInside, booking.UserID, "Handovers"),
                    GeoLatitude = model.GeoLatitude,
                    GeoLongitude = model.GeoLongitude
                };

                foreach (var item in BuildInventorySnapshots(model.InventoryItems, normalizedType))
                    record.InventoryItems.Add(item);

            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Capture", new { bookingId = model.BookingID, type = normalizedType });
            }

            if (record.InventoryItems.Count == 0)
            {
                TempData["ErrorMessage"] = "Checklist phụ kiện đang trống. Vui lòng thử lại sau khi hệ thống tải danh mục phụ kiện.";
                return RedirectToAction("Capture", new { bookingId = model.BookingID, type = normalizedType });
            }

            var signatureResult = await SaveSignatureAsync(uploadPath, model.SignatureBase64, booking.UserID);
            if (!signatureResult.IsValid)
            {
                TempData["ErrorMessage"] = "Chữ ký không hợp lệ. Vui lòng ký lại.";
                return RedirectToAction("Capture", new { bookingId = model.BookingID, type = normalizedType });
            }

            record.SignatureUrl = signatureResult.Url;
            if (!string.IsNullOrWhiteSpace(model.SignatureBase64))
                record.CustomerSignatureHash = _evidenceHash.ComputeTextHash(model.SignatureBase64);

            record.EvidenceHash = _evidenceHash.ComputeHandoverEvidenceHash(record);
            _context.HandoverRecords.Add(record);
            await _context.SaveChangesAsync();

            var compensationAmount = record.InventoryItems.Sum(i => i.MissingCharge);
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "CreateHandoverEvidence",
                EntityType = nameof(HandoverRecord),
                EntityId = record.HandoverID,
                UserId = userId,
                Reason = $"Handover {record.Type} evidence captured.",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    record.BookingID,
                    record.Type,
                    record.EvidenceHash,
                    InventoryItems = record.InventoryItems.Count,
                    CompensationAmount = compensationAmount
                })
            });

            TempData["SuccessMessage"] = $"Biên bản {record.Type} đã được lưu trữ an toàn.";
            return RedirectToAction("Details", "Booking", new { id = model.BookingID });
        }

        private async Task<Booking?> LoadBookingForHandoverAsync(int bookingId)
        {
            return await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Car)
                .ThenInclude(c => c!.CarBrand)
                .Include(b => b.Car)
                .ThenInclude(c => c!.InventoryItems)
                .Include(b => b.DigitalContract)
                .Include(b => b.BookingTransaction)
                .ThenInclude(t => t!.DigitalContract)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
        }

        private async Task<HandoverCaptureViewModel> BuildCaptureViewModelAsync(Booking booking, string normalizedType)
        {
            var model = new HandoverCaptureViewModel
            {
                BookingID = booking.BookingID,
                Type = normalizedType,
                Booking = booking,
                Odo = booking.Car?.CurrentOdometer ?? 0,
                FuelLevel = "F",
                InventoryItems = await BuildInventoryInputsAsync(booking, normalizedType)
            };

            return model;
        }

        private async Task<List<HandoverInventoryInput>> BuildInventoryInputsAsync(Booking booking, string normalizedType)
        {
            if (normalizedType == "CheckOut")
            {
                var checkIn = await _context.HandoverRecords
                    .Include(h => h.InventoryItems)
                    .Where(h => h.BookingID == booking.BookingID && h.Type == "CheckIn")
                    .OrderByDescending(h => h.RecordDate)
                    .FirstOrDefaultAsync();

                if (checkIn?.InventoryItems.Any() == true)
                {
                    return checkIn.InventoryItems
                        .OrderBy(i => i.ItemName)
                        .Select(i => new HandoverInventoryInput
                        {
                            CarInventoryItemID = i.CarInventoryItemID,
                            ItemName = i.ItemName,
                            ExpectedQuantity = i.ActualQuantity,
                            ActualQuantity = i.ActualQuantity,
                            ConditionStatus = i.ActualQuantity > 0
                                ? HandoverInventoryConditionStatusConstants.Intact
                                : HandoverInventoryConditionStatusConstants.Absent,
                            UnitReplacementCost = i.UnitReplacementCost,
                            Notes = i.Notes
                        })
                        .ToList();
                }
            }

            var carInventory = booking.Car?.InventoryItems?
                .Where(i => i.IsActive && !i.IsDeleted)
                .OrderBy(i => i.ItemName)
                .Select(i => new HandoverInventoryInput
                {
                    CarInventoryItemID = i.CarInventoryItemID,
                    ItemName = i.ItemName,
                    ExpectedQuantity = i.Quantity,
                    ActualQuantity = i.Quantity,
                    ConditionStatus = HandoverInventoryConditionStatusConstants.Present,
                    UnitReplacementCost = i.UnitReplacementCost
                })
                .ToList();

            return carInventory?.Count > 0 ? carInventory : BuildFallbackInventoryInputs();
        }

        private static List<HandoverInventoryInput> BuildFallbackInventoryInputs()
        {
            return new List<HandoverInventoryInput>
            {
                new() { ItemName = "Đăng kiểm/bảo hiểm bản sao", ExpectedQuantity = 1, ActualQuantity = 1, ConditionStatus = HandoverInventoryConditionStatusConstants.Present, UnitReplacementCost = 200000 },
                new() { ItemName = "Thảm sàn", ExpectedQuantity = 1, ActualQuantity = 1, ConditionStatus = HandoverInventoryConditionStatusConstants.Present, UnitReplacementCost = 500000 },
                new() { ItemName = "Bánh dự phòng", ExpectedQuantity = 1, ActualQuantity = 1, ConditionStatus = HandoverInventoryConditionStatusConstants.Present, UnitReplacementCost = 2500000 },
                new() { ItemName = "Bộ kích và cờ lê", ExpectedQuantity = 1, ActualQuantity = 1, ConditionStatus = HandoverInventoryConditionStatusConstants.Present, UnitReplacementCost = 1200000 },
                new() { ItemName = "Camera hành trình/thẻ nhớ", ExpectedQuantity = 1, ActualQuantity = 1, ConditionStatus = HandoverInventoryConditionStatusConstants.Present, UnitReplacementCost = 1800000 },
                new() { ItemName = "Cáp sạc điện thoại", ExpectedQuantity = 1, ActualQuantity = 1, ConditionStatus = HandoverInventoryConditionStatusConstants.Present, UnitReplacementCost = 250000 },
                new() { ItemName = "Bơm lốp mini", ExpectedQuantity = 1, ActualQuantity = 1, ConditionStatus = HandoverInventoryConditionStatusConstants.Present, UnitReplacementCost = 700000 }
            };
        }

        private static List<HandoverInventoryItem> BuildInventorySnapshots(IEnumerable<HandoverInventoryInput>? inputs, string normalizedType)
        {
            var snapshots = new List<HandoverInventoryItem>();
            foreach (var input in inputs ?? Enumerable.Empty<HandoverInventoryInput>())
            {
                var itemName = input.ItemName?.Trim();
                if (string.IsNullOrWhiteSpace(itemName))
                    continue;

                var unitCost = Math.Max(0, input.UnitReplacementCost);
                var originalExpectedQuantity = Math.Max(0, input.ExpectedQuantity);
                var status = NormalizeInventoryCondition(input.ConditionStatus, normalizedType);
                var expectedQuantity = normalizedType == "CheckIn"
                    ? (status == HandoverInventoryConditionStatusConstants.Present ? originalExpectedQuantity : 0)
                    : originalExpectedQuantity;
                var actualQuantity = normalizedType == "CheckIn"
                    ? expectedQuantity
                    : status switch
                    {
                        HandoverInventoryConditionStatusConstants.Missing => 0,
                        _ => expectedQuantity
                    };
                var missingQuantity = normalizedType == "CheckOut" && status == HandoverInventoryConditionStatusConstants.Missing
                    ? expectedQuantity
                    : 0;
                var compensationCharge = normalizedType == "CheckOut" && status is HandoverInventoryConditionStatusConstants.Missing or HandoverInventoryConditionStatusConstants.Damaged
                    ? Math.Max(0, expectedQuantity) * unitCost
                    : 0m;

                snapshots.Add(new HandoverInventoryItem
                {
                    CarInventoryItemID = input.CarInventoryItemID,
                    ItemName = itemName,
                    ExpectedQuantity = expectedQuantity,
                    ActualQuantity = actualQuantity,
                    MissingQuantity = missingQuantity,
                    ConditionStatus = status,
                    UnitReplacementCost = unitCost,
                    MissingCharge = compensationCharge,
                    Notes = input.Notes?.Trim()
                });
            }

            return snapshots;
        }

        private static string NormalizeInventoryCondition(string? conditionStatus, string normalizedType)
        {
            var value = conditionStatus?.Trim();
            if (normalizedType == "CheckOut")
            {
                return value switch
                {
                    HandoverInventoryConditionStatusConstants.Missing => HandoverInventoryConditionStatusConstants.Missing,
                    HandoverInventoryConditionStatusConstants.Damaged => HandoverInventoryConditionStatusConstants.Damaged,
                    HandoverInventoryConditionStatusConstants.Absent => HandoverInventoryConditionStatusConstants.Absent,
                    _ => HandoverInventoryConditionStatusConstants.Intact
                };
            }

            return value == HandoverInventoryConditionStatusConstants.Absent
                ? HandoverInventoryConditionStatusConstants.Absent
                : HandoverInventoryConditionStatusConstants.Present;
        }

        private static bool HasRequiredEvidenceImages(HandoverCaptureViewModel model)
        {
            return model.ImgFront?.Length > 0 &&
                   model.ImgBack?.Length > 0 &&
                   model.ImgLeft?.Length > 0 &&
                   model.ImgRight?.Length > 0 &&
                   model.ImgInside?.Length > 0;
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

        private async Task<string?> SaveEvidenceFileAsync(string uploadPath, IFormFile? file, int ownerUserId, string folder)
        {
            if (file == null || file.Length == 0)
                return null;

            if (!await _fileUploadValidation.IsAllowedImageAsync(file, MaxEvidenceFileSize))
                throw new InvalidOperationException($"File {file.FileName} không hợp lệ. Chỉ chấp nhận JPG, PNG, WEBP đúng định dạng và tối đa 5MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var ownerFolder = Path.Combine(uploadPath, ownerUserId.ToString());
            Directory.CreateDirectory(ownerFolder);
            var relativeFileName = $"{ownerUserId}/{fileName}";
            var filePath = Path.Combine(ownerFolder, fileName);
            await using var stream = new FileStream(filePath, FileMode.CreateNew);
            await file.CopyToAsync(stream);
            return $"/SecureFile/GetSecureImage?folder={folder}&fileName={relativeFileName}";
        }

        private static async Task<(bool IsValid, string? Url)> SaveSignatureAsync(string uploadPath, string? signatureBase64, int ownerUserId)
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
            var ownerFolder = Path.Combine(uploadPath, ownerUserId.ToString());
            Directory.CreateDirectory(ownerFolder);
            var relativeFileName = $"{ownerUserId}/{signFileName}";
            var signPath = Path.Combine(ownerFolder, signFileName);
            await System.IO.File.WriteAllBytesAsync(signPath, bytes);
            return (true, $"/SecureFile/GetSecureImage?folder=Handovers&fileName={relativeFileName}");
        }

        private async Task<bool> CanViewBookingAsync(Booking booking, int userId)
        {
            if (booking.UserID == userId) return true;
            if (booking.Car?.OwnerID == userId) return true;
            return await CanInternalAccessBookingAsync(booking, userId);
        }

        private async Task<bool> CanCaptureBookingAsync(Booking booking, int userId)
        {
            if (booking.UserID == userId) return true;
            return await CanInternalAccessBookingAsync(booking, userId);
        }

        private async Task<bool> CanInternalAccessBookingAsync(Booking booking, int userId)
        {
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
