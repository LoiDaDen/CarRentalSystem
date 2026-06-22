using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services;

public class StartupKpiServiceTests
{
    private static readonly DateTime StartUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EndUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Module", "StartupKpi")]
    [Trait("Type", "Regression")]
    public async Task GetSnapshotAsync_UsesFinalOutcomesAndSeparatesGmvFromDepositLedger()
    {
        await using var context = MockRepositoryHelper.CreateSeededDbContext();
        var completed = context.BookingStatuses.Single(status => status.Name == BookingStatusConstants.Completed);
        var cancelled = context.BookingStatuses.Single(status => status.Name == BookingStatusConstants.Cancelled);
        var pending = context.BookingStatuses.Single(status => status.Name == BookingStatusConstants.Pending);
        var draft = context.BookingStatuses.Single(status => status.Name == BookingStatusConstants.Draft);
        var car = context.Cars.Single(item => item.CarID == 1);
        car.CommissionRate = 20m;
        car.CreatedAt = StartUtc.AddDays(1);
        car.IsApproved = true;
        car.ApprovalStatus = CarApprovalStatusConstants.Approved;
        car.IsSelfDriveAllowed = true;

        var completedBooking = AddBooking(context, completed.BookingStatusID, 10_000_000m, StartUtc.AddDays(10));
        AddBooking(context, cancelled.BookingStatusID, 4_000_000m, StartUtc.AddDays(11));
        AddBooking(context, pending.BookingStatusID, 5_000_000m, StartUtc.AddDays(12));
        AddBooking(context, draft.BookingStatusID, 9_000_000m, StartUtc.AddDays(13));
        context.Reviews.AddRange(
            new Review { UserID = 2, CarID = 1, Rating = 5, IsApproved = true, CreatedAt = StartUtc.AddDays(20) },
            new Review { UserID = 3, CarID = 1, Rating = 3, IsApproved = true, CreatedAt = StartUtc.AddDays(21) },
            new Review { UserID = 1, CarID = 1, Rating = 5, IsApproved = false, CreatedAt = StartUtc.AddDays(22) });
        context.PaymentLedgerEntries.AddRange(
            Ledger(completedBooking.BookingID, PaymentLedgerEntryTypeConstants.DepositCharge, 1_000_000m, PaymentStatusConstants.Paid),
            Ledger(completedBooking.BookingID, PaymentLedgerEntryTypeConstants.Refund, 200_000m, PaymentStatusConstants.Refunded),
            Ledger(completedBooking.BookingID, PaymentLedgerEntryTypeConstants.Payout, 300_000m, PaymentStatusConstants.Paid),
            Ledger(completedBooking.BookingID, PaymentLedgerEntryTypeConstants.DepositCharge, 9_000_000m, PaymentStatusConstants.Pending));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetSnapshotAsync(StartUtc, EndUtc);

        result.ValidBookings.Should().Be(3);
        result.CompletedBookings.Should().Be(1);
        result.OutcomeBookings.Should().Be(2);
        result.SuccessRatePercent.Should().Be(50m);
        result.Gmv.Should().Be(10_000_000m);
        result.PlatformCommission.Should().Be(0m);
        result.ApprovedReviews.Should().Be(2);
        result.FavorableReviews.Should().Be(1);
        result.SatisfactionRatePercent.Should().Be(50m);
        result.LedgerNetPostedAmount.Should().Be(500_000m);
        result.LedgerPostedEntryCount.Should().Be(3);
    }

    [Fact]
    [Trait("Module", "StartupKpi")]
    [Trait("Type", "Regression")]
    public async Task GetSnapshotAsync_WhenNoFinalOutcomesOrApprovedReviews_ReturnsZeroRates()
    {
        await using var context = MockRepositoryHelper.CreateSeededDbContext();
        var pending = context.BookingStatuses.Single(status => status.Name == BookingStatusConstants.Pending);
        AddBooking(context, pending.BookingStatusID, 1_000_000m, StartUtc.AddDays(1));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetSnapshotAsync(StartUtc, EndUtc);

        result.OutcomeBookings.Should().Be(0);
        result.SuccessRatePercent.Should().Be(0m);
        result.ApprovedReviews.Should().Be(0);
        result.SatisfactionRatePercent.Should().Be(0m);
    }

    private static StartupKpiService CreateService(CarRentalSystem.Data.ApplicationDbContext context) =>
        new(context, Options.Create(new StartupKpiOptions { DefaultCommissionRate = 0m }));

    private static Booking AddBooking(
        CarRentalSystem.Data.ApplicationDbContext context,
        int statusId,
        decimal totalPrice,
        DateTime createdAt)
    {
        var booking = new Booking
        {
            UserID = 2,
            CarID = 1,
            StartDate = createdAt.AddDays(2),
            EndDate = createdAt.AddDays(4),
            PickupLocationID = 1,
            ReturnLocationID = 1,
            BookingStatusID = statusId,
            TotalHours = 48,
            TotalPrice = totalPrice,
            CreatedAt = createdAt
        };
        context.Bookings.Add(booking);
        context.SaveChanges();
        return booking;
    }

    private static PaymentLedgerEntry Ledger(int bookingId, string type, decimal amount, string status) =>
        new()
        {
            BookingID = bookingId,
            EntryType = type,
            Amount = amount,
            Status = status,
            CreatedAt = StartUtc.AddDays(30)
        };
}
