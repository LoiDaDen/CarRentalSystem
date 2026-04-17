using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = UserRoleConstants.Admin)]
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        public async Task<IActionResult> Index(string? status, string? search)
        {
            ViewBag.Status = status;
            ViewBag.Search = search;
            var bookings = await _bookingService.GetAllBookingsAsync();

            if (string.IsNullOrEmpty(status))
                bookings = bookings.Where(b => b.BookingStatus?.Name != BookingStatusConstants.Draft);
            else
                bookings = bookings.Where(b => b.BookingStatus?.Name == status);

            if (!string.IsNullOrEmpty(search))
                bookings = bookings.Where(b =>
                    b.User.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    b.User.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (b.Car.CarBrand?.Name + " " + b.Car.CarName).Contains(search, StringComparison.OrdinalIgnoreCase));

            return View(bookings.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            await _bookingService.UpdateBookingStatusAsync(id, status);
            TempData["Success"] = "Cập nhật trạng thái thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPickup(int id, string? notes)
        {
            await _bookingService.ConfirmPickupAsync(id, notes);
            TempData["Success"] = "Đã xác nhận nhận xe. Đơn đặt hiện đang hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            await _bookingService.CancelBookingAsync(id, reason);
            TempData["Success"] = "Đã hủy đơn đặt xe thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CompleteReturn(int bookingId)
        {
            await _bookingService.CompleteBookingAsync(bookingId);
            TempData["Success"] = "Hoàn thành quy trình trả xe!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications()
        {
            var allBookings = await _bookingService.GetAllBookingsAsync();
            var since = DateTime.Now.AddHours(-24);
            
            var notifications = allBookings
                .Where(b => b.CreatedAt >= since && b.BookingStatus?.Name != BookingStatusConstants.Draft)
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .Select(b => new {
                    id = b.BookingID,
                    message = b.BookingStatus?.Name switch {
                        BookingStatusConstants.Pending => $"Đơn mới #{b.BookingID} - {b.User?.Name ?? "Khách"} đặt xe {b.Car?.CarName ?? ""}",
                        BookingStatusConstants.Cancelled => $"Đơn #{b.BookingID} đã bị hủy bởi {b.User?.Name ?? "Khách"}",
                        BookingStatusConstants.Confirmed => $"Đơn #{b.BookingID} đã được xác nhận",
                        BookingStatusConstants.Completed => $"Đơn #{b.BookingID} đã hoàn thành",
                        _ => $"Đơn #{b.BookingID} - {b.BookingStatus?.Name}"
                    },
                    time = b.CreatedAt.ToString("HH:mm dd/MM"),
                    status = b.BookingStatus?.Name ?? "Unknown",
                    icon = b.BookingStatus?.Name switch {
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
    }
}
