using CarRentalSystem.Areas.Admin.Models;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

using AdminDashboardController = CarRentalSystem.Areas.Admin.Controllers.DashboardController;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    public class AdminDashboardControllerTests
    {
        [Fact]
        [Trait("Module", "AdminDashboard")]
        [Trait("Type", "Regression")]
        public async Task Index_CountsOnlyUnreturnedHoldingDeposits_AndUsesDepositLedgerForDashboardCashflow()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var confirmed = context.BookingStatuses.Single(s => s.Name == BookingStatusConstants.Confirmed);
            var completed = context.BookingStatuses.Single(s => s.Name == BookingStatusConstants.Completed);
            var returnedAt = DateTime.UtcNow.AddMinutes(-5);

            context.Bookings.AddRange(
                CreateBooking(confirmed.BookingStatusID, DepositStatusConstants.Holding, null, totalPrice: 100m, depositAmount: 1_000m),
                CreateBooking(confirmed.BookingStatusID, DepositStatusConstants.Holding, returnedAt, totalPrice: 200m, depositAmount: 2_000m),
                CreateBooking(completed.BookingStatusID, DepositStatusConstants.Holding, returnedAt, totalPrice: 300m, depositAmount: 3_000m),
                CreateBooking(completed.BookingStatusID, DepositStatusConstants.Refunded, returnedAt, totalPrice: 400m, depositAmount: 4_000m));
            await context.SaveChangesAsync();
            context.PaymentLedgerEntries.Add(new PaymentLedgerEntry
            {
                BookingID = context.Bookings.OrderByDescending(b => b.BookingID).Select(b => b.BookingID).First(),
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = 5_000m,
                Status = PaymentStatusConstants.Paid,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = new AdminDashboardController(
                Mock.Of<ICarService>(),
                Mock.Of<IBookingService>(),
                context);

            var result = await controller.Index();

            var model = result.Should().BeOfType<ViewResult>().Which.Model
                .Should().BeOfType<DashboardViewModel>().Subject;
            model.TotalHeldDepositsCount.Should().Be(1);
            model.TotalHeldDepositsAmount.Should().Be(1_000m);
            model.TotalRevenue.Should().Be(1_000m);
            model.TodayDepositAmount.Should().Be(5_000m);
            model.DepositData.Sum().Should().Be(0.005m);
            model.TopCars.Should().ContainSingle()
                .Which.TotalRevenue.Should().Be(700m);
        }

        [Fact]
        [Trait("Module", "AdminDashboard")]
        [Trait("Type", "Regression")]
        public async Task Index_WhenRevenueQueryFails_ReturnsInitializedFallbackModel()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var revenue = new Mock<IRevenueMetricsService>();
            revenue.Setup(service => service.GetSnapshotAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Ledger unavailable"));
            var controller = new AdminDashboardController(
                Mock.Of<ICarService>(),
                Mock.Of<IBookingService>(),
                context,
                revenue.Object);

            var result = await controller.Index();

            var model = result.Should().BeOfType<ViewResult>().Which.Model
                .Should().BeOfType<DashboardViewModel>().Subject;
            model.HasLoadError.Should().BeTrue();
            model.RecentBookings.Should().NotBeNull();
            model.PendingCars.Should().NotBeNull();
            model.DepositLabels.Should().NotBeNull();
            model.DepositData.Should().NotBeNull();
        }

        private static Booking CreateBooking(
            int statusId,
            string depositStatus,
            DateTime? actualReturnDate,
            decimal totalPrice,
            decimal depositAmount)
        {
            return new Booking
            {
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(-2),
                EndDate = DateTime.UtcNow.AddDays(-1),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                BookingStatusID = statusId,
                DepositStatus = depositStatus,
                ActualReturnDate = actualReturnDate,
                TotalHours = 24,
                TotalPrice = totalPrice,
                DepositAmount = depositAmount
            };
        }
    }
}
