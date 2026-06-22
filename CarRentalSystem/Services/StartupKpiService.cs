using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CarRentalSystem.Services;

public sealed class StartupKpiOptions
{
    public DateTime PeriodStartUtc { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public int PeriodMonths { get; set; } = 6;
    public decimal DefaultCommissionRate { get; set; } = 0m;
    public int TargetRegisteredUsers { get; set; } = 100;
    public int TargetApprovedCars { get; set; } = 30;
    public int TargetValidBookings { get; set; } = 80;
    public int TargetCompletedBookings { get; set; } = 56;
    public decimal TargetSuccessRatePercent { get; set; } = 70m;
    public decimal TargetGmv { get; set; } = 250_000_000m;
    public decimal TargetPlatformCommission { get; set; } = 0m;
    public int TargetApprovedReviews { get; set; } = 25;
    public decimal TargetSatisfactionRatePercent { get; set; } = 80m;
}

public sealed record StartupKpiSnapshot(
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int RegisteredUsers,
    int ApprovedSelfDriveCars,
    int ValidBookings,
    int CompletedBookings,
    int OutcomeBookings,
    decimal SuccessRatePercent,
    decimal Gmv,
    decimal PlatformCommission,
    int ApprovedReviews,
    int FavorableReviews,
    decimal SatisfactionRatePercent,
    decimal LedgerNetPostedAmount,
    int LedgerPostedEntryCount);

public interface IStartupKpiService
{
    Task<StartupKpiSnapshot> GetSnapshotAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);
}

public sealed class StartupKpiService : IStartupKpiService
{
    private static readonly string[] InvalidBookingStatuses =
    {
        BookingStatusConstants.Draft,
        BookingStatusConstants.Unpaid
    };

    private static readonly string[] OutcomeBookingStatuses =
    {
        BookingStatusConstants.Completed,
        BookingStatusConstants.Cancelled,
        BookingStatusConstants.Rejected,
        BookingStatusConstants.Expired
    };

    private static readonly string[] PostedLedgerStatuses =
    {
        PaymentStatusConstants.Paid,
        PaymentStatusConstants.Refunded
    };

    private readonly ApplicationDbContext _context;
    private readonly StartupKpiOptions _options;

    public StartupKpiService(ApplicationDbContext context, IOptions<StartupKpiOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<StartupKpiSnapshot> GetSnapshotAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        if (periodEndUtc <= periodStartUtc)
            throw new ArgumentException("KPI period end must be later than period start.", nameof(periodEndUtc));

        var registeredUsers = await _context.Users.AsNoTracking()
            .CountAsync(user => user.CreatedAt >= periodStartUtc && user.CreatedAt < periodEndUtc, cancellationToken);

        var approvedSelfDriveCars = await _context.Cars.AsNoTracking()
            .CountAsync(car =>
                !car.IsDeleted &&
                car.IsSelfDriveAllowed &&
                car.IsApproved &&
                car.ApprovalStatus == CarApprovalStatusConstants.Approved &&
                car.CreatedAt >= periodStartUtc &&
                car.CreatedAt < periodEndUtc,
                cancellationToken);

        var validBookings = _context.Bookings.AsNoTracking()
            .Where(booking =>
                !booking.IsDeleted &&
                booking.CreatedAt >= periodStartUtc &&
                booking.CreatedAt < periodEndUtc &&
                booking.BookingStatus != null &&
                !InvalidBookingStatuses.Contains(booking.BookingStatus.Name));

        var validBookingCount = await validBookings.CountAsync(cancellationToken);
        var outcomeBookingCount = await validBookings
            .CountAsync(booking => OutcomeBookingStatuses.Contains(booking.BookingStatus!.Name), cancellationToken);

        var completedRows = await validBookings
            .Where(booking => booking.BookingStatus!.Name == BookingStatusConstants.Completed)
            .Select(booking => new
            {
                booking.TotalPrice
            })
            .ToListAsync(cancellationToken);

        var completedBookingCount = completedRows.Count;
        var gmv = completedRows.Sum(row => row.TotalPrice);
        var platformCommission = 0m;
        var successRate = outcomeBookingCount == 0
            ? 0m
            : Math.Round(completedBookingCount * 100m / outcomeBookingCount, 1);

        var approvedReviews = _context.Reviews.AsNoTracking()
            .Where(review =>
                review.IsApproved &&
                !review.IsDeleted &&
                review.CreatedAt >= periodStartUtc &&
                review.CreatedAt < periodEndUtc);
        var approvedReviewCount = await approvedReviews.CountAsync(cancellationToken);
        var favorableReviewCount = await approvedReviews.CountAsync(review => review.Rating >= 4, cancellationToken);
        var satisfactionRate = approvedReviewCount == 0
            ? 0m
            : Math.Round(favorableReviewCount * 100m / approvedReviewCount, 1);

        var ledgerRows = await _context.PaymentLedgerEntries.AsNoTracking()
            .Where(entry =>
                entry.CreatedAt >= periodStartUtc &&
                entry.CreatedAt < periodEndUtc &&
                PostedLedgerStatuses.Contains(entry.Status))
            .Select(entry => new { entry.EntryType, entry.Amount })
            .ToListAsync(cancellationToken);
        var ledgerNetPostedAmount = ledgerRows.Sum(entry => entry.EntryType switch
        {
            PaymentLedgerEntryTypeConstants.Refund => -entry.Amount,
            PaymentLedgerEntryTypeConstants.Payout => -entry.Amount,
            _ => entry.Amount
        });

        return new StartupKpiSnapshot(
            periodStartUtc,
            periodEndUtc,
            registeredUsers,
            approvedSelfDriveCars,
            validBookingCount,
            completedBookingCount,
            outcomeBookingCount,
            successRate,
            gmv,
            platformCommission,
            approvedReviewCount,
            favorableReviewCount,
            satisfactionRate,
            ledgerNetPostedAmount,
            ledgerRows.Count);
    }
}
