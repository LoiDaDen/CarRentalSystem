using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        public async Task<IActionResult> Create(int CarID, int Rating, string? Comment)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            // Check user actually rented this car
            var hasRented = await _db.Bookings.AnyAsync(b => b.UserID == userId && b.CarID == CarID && b.BookingStatus != null && b.BookingStatus.Name == "Completed");
            if (!hasRented)
            {
                TempData["Error"] = "Bạn chỉ có thể đánh giá xe mà bạn đã thuê xong.";
                return RedirectToAction("Details", "Car", new { id = CarID });
            }

            var review = new Review
            {
                CarID = CarID,
                UserID = userId,
                Rating = Math.Clamp(Rating, 1, 5),
                Comment = Comment,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Cảm ơn bạn đã đánh giá!";
            return RedirectToAction("Details", "Car", new { id = CarID });
        }
    }
}
