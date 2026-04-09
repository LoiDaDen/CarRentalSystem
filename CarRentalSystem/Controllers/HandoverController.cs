using CarRentalSystem.Data;
using CarRentalSystem.Models;
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

        public HandoverController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
                RecordDate = DateTime.Now,
                Odo = Odo,
                FuelLevel = FuelLevel
            };

            string uploadPath = Path.Combine(_env.WebRootPath, "uploads", "handovers");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            async Task<string?> SaveFile(IFormFile? file)
            {
                if (file == null || file.Length == 0) return null;
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                string filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
                return "/uploads/handovers/" + fileName;
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
                record.SignatureUrl = "/uploads/handovers/" + signFileName;
            }

            _context.HandoverRecords.Add(record);
            
            if(record.Type == "CheckOut")
            {
                booking.ActualReturnDate = DateTime.Now;
                // Keep DepositStatus as "Holding" for 15 days, Admin will refund later.
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Biên bản {record.Type} đã được lưu trữ an toàn.";
            return RedirectToAction("Details", "Booking", new { id = BookingID }); // Fallback or MyBookings
        }
    }
}
