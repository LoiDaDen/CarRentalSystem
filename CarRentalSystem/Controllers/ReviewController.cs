using System.Security.Claims;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ReviewController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int CarID, int Rating, string? Comment)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

            if (Rating < 1 || Rating > 5)
            {
                TempData["BookingError"] = "Điểm đánh giá phải từ 1 đến 5 sao.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            var hasCompletedBooking = await _db.Bookings.AnyAsync(b =>
                b.UserID == userId &&
                b.CarID == CarID &&
                b.BookingStatus != null &&
                b.BookingStatus.Name == BookingStatusConstants.Completed);

            if (!hasCompletedBooking)
            {
                TempData["BookingError"] = "Bạn chỉ có thể đánh giá xe sau khi chuyến thuê đã hoàn tất.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            var alreadyReviewed = await _db.Reviews.AnyAsync(r => r.CarID == CarID && r.UserID == userId);
            if (alreadyReviewed)
            {
                TempData["BookingError"] = "Bạn đã gửi đánh giá cho xe này rồi.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            var review = new Review
            {
                CarID = CarID,
                UserID = userId,
                Rating = Math.Clamp(Rating, 1, 5),
                Comment = string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim(),
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá. Đánh giá của bạn đã được hiển thị công khai.";
            return RedirectToAction("Details", "Car", new { id = CarID });
        }
    }
}
