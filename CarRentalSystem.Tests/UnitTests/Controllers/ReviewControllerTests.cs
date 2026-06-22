using System.Security.Claims;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    public class ReviewControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public ReviewControllerTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
        }

        [Fact]
        public async Task Create_WithCompletedBooking_PublishesReviewImmediately()
        {
            var completedStatus = await _context.BookingStatuses.SingleAsync(s => s.Name == BookingStatusConstants.Completed);
            _context.Bookings.Add(new Booking
            {
                BookingID = 9001,
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(-3),
                EndDate = DateTime.UtcNow.AddDays(-2),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalPrice = 1_000_000m,
                TotalHours = 24,
                BookingStatusID = completedStatus.BookingStatusID,
                BookingStatus = completedStatus
            });
            await _context.SaveChangesAsync();

            var controller = CreateController("2");

            var result = await controller.Create(1, 5, " Xe rất ổn ") as RedirectToActionResult;

            result.Should().NotBeNull();
            result!.ControllerName.Should().Be("Car");
            result.ActionName.Should().Be("Details");
            var review = await _context.Reviews.SingleAsync(r => r.UserID == 2 && r.CarID == 1);
            review.IsApproved.Should().BeTrue();
            review.Comment.Should().Be("Xe rất ổn");
            controller.TempData["SuccessMessage"]!.ToString().Should().Contain("hiển thị công khai");
        }

        private ReviewController CreateController(string userId)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }, "TestAuth"))
            };

            var controller = new ReviewController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };

            return controller;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
