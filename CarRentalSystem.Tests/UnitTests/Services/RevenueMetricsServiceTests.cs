using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class RevenueMetricsServiceTests
    {
        [Fact]
        [Trait("Module", "AdminDashboard")]
        [Trait("Type", "Regression")]
        public async Task GetDailyNetRevenueAsync_UsesDepositDeductionsAsNetRevenue()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            var booking = new Booking
            {
                UserID = 2,
                CarID = 1,
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                BookingStatusID = 6,
                TotalHours = 24,
                TotalPrice = 1_000m
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            context.PaymentLedgerEntries.AddRange(
                Entry(booking.BookingID, PaymentLedgerEntryTypeConstants.DepositCharge, 1_000m, PaymentStatusConstants.Paid, new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc)),
                Entry(booking.BookingID, PaymentLedgerEntryTypeConstants.Deduction, 50m, PaymentStatusConstants.Paid, new DateTime(2026, 6, 1, 2, 0, 0, DateTimeKind.Utc)),
                Entry(booking.BookingID, PaymentLedgerEntryTypeConstants.Refund, 100m, PaymentStatusConstants.Refunded, new DateTime(2026, 6, 1, 3, 0, 0, DateTimeKind.Utc)),
                Entry(booking.BookingID, PaymentLedgerEntryTypeConstants.DepositCharge, 9_999m, PaymentStatusConstants.Pending, new DateTime(2026, 6, 1, 4, 0, 0, DateTimeKind.Utc)),
                Entry(booking.BookingID, PaymentLedgerEntryTypeConstants.DepositCharge, 500m, PaymentStatusConstants.Paid, new DateTime(2026, 6, 2, 1, 0, 0, DateTimeKind.Utc)));
            await context.SaveChangesAsync();

            var service = new RevenueMetricsService(context);

            var points = await service.GetDailyNetRevenueAsync(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc));

            points.Should().HaveCount(2);
            points[0].NetRevenue.Should().Be(50m);
            points[1].NetRevenue.Should().Be(0m);
        }

        private static PaymentLedgerEntry Entry(int bookingId, string type, decimal amount, string status, DateTime createdAt)
        {
            return new PaymentLedgerEntry
            {
                BookingID = bookingId,
                EntryType = type,
                Amount = amount,
                Status = status,
                CreatedAt = createdAt
            };
        }
    }
}
