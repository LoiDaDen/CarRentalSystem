using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;

namespace CarRentalSystem.Areas.Driver.Controllers
{
    [Area("Driver")]
    [Authorize]
    public class DriverDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;

        public DriverDashboardController(ApplicationDbContext db)
        {
            _db = db;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;
            if (int.TryParse(claim.Value, out int id)) return id;
            return null;
        }

        private async Task<CarRentalSystem.Models.Driver?> GetCurrentDriverAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return null;

            return await _db.Drivers
                .Include(d => d.Location)
                .Include(d => d.DriverBookings!)
                    .ThenInclude(db => db.Booking)
                        .ThenInclude(b => b!.User)
                .Include(d => d.DriverBookings!)
                    .ThenInclude(db => db.Booking)
                        .ThenInclude(b => b!.Car)
                            .ThenInclude(c => c!.CarBrand)
                .Include(d => d.DriverBookings!)
                    .ThenInclude(db => db.Booking)
                        .ThenInclude(b => b!.PickupLocation)
                .Include(d => d.DriverBookings!)
                    .ThenInclude(db => db.Booking)
                        .ThenInclude(b => b!.ReturnLocation)
                .FirstOrDefaultAsync(d => d.UserID == userId);
        }

        // GET /Driver/DriverDashboard
        public async Task<IActionResult> Index(string? filter)
        {
            var driver = await GetCurrentDriverAsync();
            if (driver == null)
            {
                TempData["Error"] = "Bạn chưa đăng ký làm tài xế hoặc chưa được duyệt.";
                return RedirectToAction("Register", "Driver", new { area = "" });
            }

            if (!driver.IsApproved)
            {
                TempData["Info"] = "Tài khoản tài xế của bạn đang chờ Admin duyệt.";
                return RedirectToAction("MyProfile", "Driver", new { area = "" });
            }

            // ═══ Kiểm tra tài xế bị khóa ═══
            if (driver.IsLocked)
            {
                TempData["Error"] = "Tài khoản tài xế của bạn đã bị khóa. Vui lòng liên hệ Admin để được hỗ trợ.";
                return RedirectToAction("Login", "Auth", new { area = "" });
            }

            // Pass to ViewBag for Layout
            ViewBag.IsDriverAvailable = driver.IsAvailable;
            ViewData["IsAvailable"] = driver.IsAvailable;

            var driverBookings = _db.DriverBookings
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.User)
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.Car)
                        .ThenInclude(c => c!.CarBrand)
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.PickupLocation)
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.ReturnLocation)
                .Where(db => db.DriverID == driver.DriverID)
                .AsQueryable();

            if (filter == "today")
            {
                var today = DateTime.UtcNow.Date;
                driverBookings = driverBookings.Where(db =>
                    db.Booking!.StartDate.Date == today ||
                    (db.Booking.StartDate.Date <= today && db.Booking.EndDate.Date >= today));
            }
            else if (filter == "upcoming")
            {
                driverBookings = driverBookings.Where(db =>
                    db.Booking!.StartDate.Date > DateTime.UtcNow.Date &&
                    (db.Status == "Confirmed" || db.Status == "Pending"));
            }
            else if (filter == "active")
            {
                driverBookings = driverBookings.Where(db =>
                    db.Status == "Active" || db.Status == "Confirmed");
            }
            else if (filter == "completed")
            {
                driverBookings = driverBookings.Where(db => db.Status == "Completed");
            }

            var bookings = await driverBookings
                .OrderByDescending(db => db.Booking!.StartDate)
                .ToListAsync();

            // Stats
            var totalTrips = await _db.DriverBookings
                .Where(db => db.DriverID == driver.DriverID && db.Status == "Completed")
                .CountAsync();

            var totalEarnings = await _db.DriverBookings
                .Where(db => db.DriverID == driver.DriverID && db.Status == "Completed")
                .SumAsync(db => db.DriverFee);

            var upcomingCount = await _db.DriverBookings
                .Where(db => db.DriverID == driver.DriverID &&
                    (db.Status == "Pending" || db.Status == "Confirmed") &&
                    db.Booking!.StartDate.Date >= DateTime.UtcNow.Date)
                .CountAsync();

            ViewBag.Driver = driver;
            ViewBag.Filter = filter;
            ViewBag.TotalTrips = totalTrips;
            ViewBag.TotalEarnings = totalEarnings;
            ViewBag.UpcomingCount = upcomingCount;
            ViewBag.TodayBookings = bookings;

            return View();
        }

        // GET /Driver/DriverDashboard/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var driver = await GetCurrentDriverAsync();
            if (driver == null) return RedirectToAction("Register", "Driver", new { area = "" });

            var driverBooking = await _db.DriverBookings
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.User)
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.Car)
                        .ThenInclude(c => c!.CarBrand)
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.PickupLocation)
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.ReturnLocation)
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.Payment)
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.BookingStatus)
                .Include(db => db.Driver)
                .FirstOrDefaultAsync(db => db.DriverBookingID == id && db.DriverID == driver.DriverID);

            if (driverBooking == null) return NotFound();

            return View(driverBooking);
        }

        // POST: Confirm pickup / Start trip
        [HttpPost]
        public async Task<IActionResult> StartTrip(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (driver == null) return NotFound();

            var driverBooking = await _db.DriverBookings
                .Include(db => db.Booking)
                .FirstOrDefaultAsync(db => db.DriverBookingID == id && db.DriverID == driver.DriverID);

            if (driverBooking == null) return NotFound();

            if (driverBooking.Status != "Confirmed" && driverBooking.Status != "Pending")
            {
                TempData["Error"] = "Không thể bắt đầu chuyến đi ở trạng thái hiện tại.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Update DriverBooking status
            driverBooking.Status = "Active";
            driverBooking.Booking!.ActualPickupDate = DateTime.UtcNow;

            // Update Booking status to Active if needed
            var activeStatus = await _db.BookingStatuses.FirstOrDefaultAsync(s => s.Name == "Active");
            if (activeStatus != null)
            {
                driverBooking.Booking!.BookingStatusID = activeStatus.BookingStatusID;
            }

            // Update Car status to Rented
            var car = await _db.Cars.FindAsync(driverBooking.Booking!.CarID);
            if (car != null)
            {
                var rentedStatus = await _db.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Rented");
                if (rentedStatus != null) car.CarStatusID = rentedStatus.CarStatusID;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã xác nhận bắt đầu chuyến đi! Chúc bạn lái xe an toàn.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Complete trip
        [HttpPost]
        public async Task<IActionResult> CompleteTrip(int id, string? notes)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (driver == null) return NotFound();

            var driverBooking = await _db.DriverBookings
                .Include(db => db.Booking)
                .FirstOrDefaultAsync(db => db.DriverBookingID == id && db.DriverID == driver.DriverID);

            if (driverBooking == null) return NotFound();

            if (driverBooking.Status != "Active")
            {
                TempData["Error"] = "Chỉ có thể hoàn thành chuyến đi đang hoạt động.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Update DriverBooking status
            driverBooking.Status = "Completed";
            driverBooking.Booking!.ActualReturnDate = DateTime.UtcNow;

            // Update Booking status to Completed
            var completedStatus = await _db.BookingStatuses.FirstOrDefaultAsync(s => s.Name == "Completed");
            if (completedStatus != null)
            {
                driverBooking.Booking!.BookingStatusID = completedStatus.BookingStatusID;
            }

            // Update Driver stats
            driver.TotalTrips += 1;

            // Update Car status back to Available
            var car = await _db.Cars.FindAsync(driverBooking.Booking!.CarID);
            if (car != null)
            {
                var availableStatus = await _db.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Available");
                if (availableStatus != null) car.CarStatusID = availableStatus.CarStatusID;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Chuyến đi đã hoàn thành! Cảm ơn bạn đã sử dụng dịch vụ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Cancel booking assignment
        [HttpPost]
        public async Task<IActionResult> CancelAssignment(int id, string? reason)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (driver == null) return NotFound();

            var driverBooking = await _db.DriverBookings
                .Include(db => db.Booking)
                .FirstOrDefaultAsync(db => db.DriverBookingID == id && db.DriverID == driver.DriverID);

            if (driverBooking == null) return NotFound();

            if (driverBooking.Status != "Pending" && driverBooking.Status != "Confirmed")
            {
                TempData["Error"] = "Không thể hủy phân công ở trạng thái hiện tại.";
                return RedirectToAction(nameof(Details), new { id });
            }

            driverBooking.Status = "Cancelled";
            driverBooking.Booking!.DriverBookingID = null;
            await _db.SaveChangesAsync();

            TempData["Info"] = "Đã hủy nhận chuyến xe.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Toggle availability
        [HttpPost]
        public async Task<IActionResult> ToggleAvailability(bool isAvailable)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserID == userId);
            if (driver == null) return NotFound();

            driver.IsAvailable = isAvailable;
            await _db.SaveChangesAsync();

            TempData["Success"] = isAvailable
                ? "Bạn đã sẵn sàng nhận chuyến!"
                : "Bạn đã tắt chế độ sẵn sàng.";

            return RedirectToAction(nameof(Index));
        }
    }
}
