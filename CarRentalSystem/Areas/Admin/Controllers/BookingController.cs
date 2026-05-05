using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin", "Manager", "Staff")]
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly ApplicationDbContext _context;
        private readonly IAreaService _areaService;
        private readonly IEmailService _emailService;

        public BookingController(
            IBookingService bookingService,
            ApplicationDbContext context,
            IAreaService areaService,
            IEmailService emailService)
        {
            _bookingService = bookingService;
            _context = context;
            _areaService = areaService;
            _emailService = emailService;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }

        private string GetCurrentRole() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
         ?? User.FindFirst("role")?.Value ?? "";

        public async Task<IActionResult> Index(string? status, string? search, int? areaId)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.CurrentAreaId = areaId;

            var allBookings = await _bookingService.GetAllBookingsAsync();

            // Staff/Manager: chỉ thấy booking trong khu vực của mình
            if (role == "Staff" || role == "Manager")
            {
                var allowedLocationIds = await _areaService.GetLocationIdsForUserAsync(userId, role);
                if (!allowedLocationIds.Any())
                {
                    return View(new List<Booking>());
                }

                allBookings = allBookings
                    .Where(b => b.Car != null && allowedLocationIds.Contains(b.Car.LocationID))
                    .ToList();
            }
            else if (areaId.HasValue && areaId.Value > 0)
            {
                var areaLocationIds = await _areaService.GetLocationIdsForAreaAsync(areaId.Value);
                if (areaLocationIds.Any())
                {
                    allBookings = allBookings
                        .Where(b => b.Car != null && areaLocationIds.Contains(b.Car.LocationID))
                        .ToList();
                }
            }

            if (string.IsNullOrEmpty(status))
                allBookings = allBookings.Where(b => b.BookingStatus?.Name != "Draft").ToList();
            else
                allBookings = allBookings.Where(b => b.BookingStatus?.Name == status).ToList();

            if (!string.IsNullOrEmpty(search))
                allBookings = allBookings.Where(b =>
                    (b.User?.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (b.User?.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    ((b.Car?.CarBrand?.Name + " " + b.Car?.CarName)?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    b.BookingID.ToString().Contains(search)
                ).ToList();

            return View(allBookings.OrderByDescending(b => b.CreatedAt).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff" || role == "Manager")
            {
                var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == id);
                if (booking != null)
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                    if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                        return Forbid();
                }
            }

            await _bookingService.UpdateBookingStatusAsync(id, status);
            TempData["Success"] = "Cập nhật trạng thái thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPickup(int id, string? notes)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff" || role == "Manager")
            {
                var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == id);
                if (booking != null)
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                    if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                        return Forbid();
                }
            }

            await _bookingService.ConfirmPickupAsync(id, notes);
            TempData["Success"] = "Đã xác nhận nhận xe. Đơn đặt hiện đang hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmDeposit(int id)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff" || role == "Manager")
            {
                var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == id);
                if (booking != null)
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                    if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                        return Forbid();
                }
            }

            var bookingToConfirm = await _bookingService.GetBookingByIdAsync(id);
            if (bookingToConfirm == null)
                return NotFound();

            bookingToConfirm.DepositStatus = "Holding";
            await _context.SaveChangesAsync();

            _ = _emailService.SendBookingConfirmationEmailAsync(bookingToConfirm, bookingToConfirm.DepositAmount);

            TempData["Success"] = "Đã xác nhận đã nhận tiền cọc. Email xác nhận đã được gửi đến khách hàng!";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff" || role == "Manager")
            {
                var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == id);
                if (booking != null)
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                    if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                        return Forbid();
                }
            }

            await _bookingService.CancelBookingAsync(id, reason);
            TempData["Success"] = "Đã hủy đơn đặt xe thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Confirm(int id)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff" || role == "Manager")
            {
                var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == id);
                if (booking != null)
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                    if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                        return Forbid();
                }
            }

            await _bookingService.UpdateBookingStatusAsync(id, BookingStatusConstants.Confirmed);
            TempData["Success"] = "Đơn đặt xe đã được xác nhận thành công!";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> StartTrip(int id)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff" || role == "Manager")
            {
                var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == id);
                if (booking != null)
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                    if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                        return Forbid();
                }
            }

            await _bookingService.StartTripAsync(id);
            TempData["Success"] = "Chuyến xe đã bắt đầu!";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> EndTrip(int id)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff" || role == "Manager")
            {
                var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == id);
                if (booking != null)
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                    if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                        return Forbid();
                }
            }

            await _bookingService.EndTripAsync(id);
            TempData["Success"] = "Chuyến xe đã kết thúc!";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteReturn(int bookingId)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role == "Staff" || role == "Manager")
            {
                var booking = await _context.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == bookingId);
                if (booking != null)
                {
                    var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                    if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                        return Forbid();
                }
            }

            await _bookingService.CompleteBookingAsync(bookingId);
            TempData["Success"] = "Hoàn thành quy trình trả xe!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications()
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();
            var allBookings = await _bookingService.GetAllBookingsAsync();

            // Staff/Manager: chỉ thông báo trong khu vực của mình
            if (role == "Staff" || role == "Manager")
            {
                var allowedLocationIds = await _areaService.GetLocationIdsForUserAsync(userId, role);
                if (allowedLocationIds.Any())
                {
                    allBookings = allBookings
                        .Where(b => b.Car != null && allowedLocationIds.Contains(b.Car.LocationID))
                        .ToList();
                }
                else
                {
                    return Json(new { count = 0, items = new List<object>() });
                }
            }

            var since = DateTime.UtcNow.AddHours(-24);

            var notifications = allBookings
                .Where(b => b.CreatedAt >= since && b.BookingStatus?.Name != "Draft")
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .Select(b => new
                {
                    id = b.BookingID,
                    message = b.BookingStatus?.Name switch
                    {
                        BookingStatusConstants.Pending => $"Đơn mới #{b.BookingID} - {b.User?.Name ?? "Khách"} đặt xe {b.Car?.CarName ?? ""}",
                        BookingStatusConstants.Cancelled => $"Đơn #{b.BookingID} đã bị hủy bởi {b.User?.Name ?? "Khách"}",
                        BookingStatusConstants.Confirmed => $"Đơn #{b.BookingID} đã được xác nhận",
                        BookingStatusConstants.Completed => $"Đơn #{b.BookingID} đã hoàn thành",
                        _ => $"Đơn #{b.BookingID} - {b.BookingStatus?.Name}"
                    },
                    time = b.CreatedAt.ToString("HH:mm dd/MM"),
                    status = b.BookingStatus?.Name ?? "Unknown",
                    icon = b.BookingStatus?.Name switch
                    {
                        BookingStatusConstants.Pending => "bi-bell-fill text-warning",
                        BookingStatusConstants.Cancelled => "bi-x-circle-fill text-danger",
                        BookingStatusConstants.Confirmed => "bi-check-circle-fill text-success",
                        BookingStatusConstants.Completed => "bi-trophy-fill text-info",
                        _ => "bi-info-circle text-secondary"
                    }
                })
                .ToList();

            return Json(new { count = notifications.Count, items = notifications });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, int? areaId)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            var allBookings = await _bookingService.GetAllBookingsAsync();
            var booking = allBookings.FirstOrDefault(b => b.BookingID == id);

            if (booking == null)
                return NotFound();

            // Staff/Manager: chỉ xem booking trong khu vực của mình
            if (role == "Staff" || role == "Manager")
            {
                var allowedLocs = await _areaService.GetLocationIdsForUserAsync(userId, role);
                if (!allowedLocs.Contains(booking.Car?.LocationID ?? 0))
                    return Forbid();
            }

            ViewBag.CurrentAreaId = areaId;
            return View(booking);
        }
    }
}
