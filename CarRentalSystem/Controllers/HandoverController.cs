using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class HandoverController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEvidenceHashService _evidenceHash;
        private readonly IAuditTrailService _auditTrail;

        public HandoverController(ApplicationDbContext context, IWebHostEnvironment env, IEvidenceHashService evidenceHash, IAuditTrailService auditTrail)
        {
            _context = context;
            _env = env;
            _evidenceHash = evidenceHash;
            _auditTrail = auditTrail;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int bookingId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Car)
                .ThenInclude(c => c.CarBrand)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.UserID == userId);

            if (booking == null) return NotFound("Không tìm thấy chuyến đi.");

            var handoverRecords = await _context.HandoverRecords
                .Where(h => h.BookingID == bookingId && !h.IsDeleted)
                .OrderBy(h => h.RecordDate)
                .ToListAsync();

            ViewBag.Booking = booking;
            ViewBag.HandoverRecords = handoverRecords;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ViewRecord(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var record = await _context.HandoverRecords
                .Include(h => h.Booking)
                .ThenInclude(b => b.Car)
                .ThenInclude(c => c.CarBrand)
                .FirstOrDefaultAsync(h => h.HandoverID == id && h.Booking.UserID == userId);

            if (record == null) return NotFound("Không tìm thấy biên bản.");

            return View(record);
        }

        [HttpGet]
        public async Task<IActionResult> Capture(int bookingId, string type)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Car)
                .ThenInclude(c => c.CarBrand)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.UserID == userId);

            if (booking == null) return NotFound("Không tìm thấy chuyến đi.");

            ViewBag.Type = type == "CheckOut" ? "CheckOut" : "CheckIn"; // Default CheckIn (Nhận xe)
            ViewBag.Booking = booking;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int BookingID, string Type, int Odo, string FuelLevel, 
            IFormFile? imgFront, IFormFile? imgBack, IFormFile? imgLeft, IFormFile? imgRight, IFormFile? imgInside, string? SignatureBase64)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingID == BookingID && b.UserID == userId);
            if (booking == null) return NotFound();

            var record = new HandoverRecord
            {
                BookingID = BookingID,
                Type = Type == "CheckOut" ? "CheckOut" : "CheckIn",
                RecordDate = DateTime.UtcNow,
                Odo = Odo,
                FuelLevel = FuelLevel,
                RecordedByUserId = userId
            };

            string uploadPath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "Handovers");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            // 🔒 Upload security constants
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            const long maxFileSize = 5 * 1024 * 1024; // 5MB per file

            async Task<string?> SaveFile(IFormFile? file)
            {
                if (file == null || file.Length == 0) return null;

                // Validate file size
                if (file.Length > maxFileSize)
                    throw new InvalidOperationException($"File {file.FileName} vượt quá giới hạn 5MB.");

                // Validate extension
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                    throw new InvalidOperationException($"File {file.FileName} có định dạng không hợp lệ. Chỉ chấp nhận JPG, PNG, WEBP.");

                string fileName = Guid.NewGuid().ToString() + ext;
                string filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
                return $"/SecureFile/GetSecureImage?folder=Handovers&fileName={fileName}";
            }

            record.FrontImgUrl = await SaveFile(imgFront);
            record.BackImgUrl = await SaveFile(imgBack);
            record.LeftImgUrl = await SaveFile(imgLeft);
            record.RightImgUrl = await SaveFile(imgRight);
            record.InsideImgUrl = await SaveFile(imgInside);

            if (!string.IsNullOrEmpty(SignatureBase64))
            {
                var base64Data = SignatureBase64.Substring(SignatureBase64.IndexOf(",") + 1);
                byte[] bytes = Convert.FromBase64String(base64Data);
                string signFileName = Guid.NewGuid().ToString() + "_sig.png";
                string signPath = Path.Combine(uploadPath, signFileName);
                await System.IO.File.WriteAllBytesAsync(signPath, bytes);
                record.SignatureUrl = $"/SecureFile/GetSecureImage?folder=Handovers&fileName={signFileName}";
                record.CustomerSignatureHash = _evidenceHash.ComputeTextHash(SignatureBase64);
            }

            record.EvidenceHash = _evidenceHash.ComputeHandoverEvidenceHash(record);
            _context.HandoverRecords.Add(record);
            
            if(record.Type == "CheckOut")
            {
                booking.ActualReturnDate = DateTime.UtcNow;
                // Keep DepositStatus as "Holding" for 15 days, Admin will refund later.
            }

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
            return RedirectToAction("Details", "Booking", new { id = BookingID }); // Fallback or MyBookings
        }
    }
}
