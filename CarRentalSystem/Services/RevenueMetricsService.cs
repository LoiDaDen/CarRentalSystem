using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public sealed record RevenueMetricsSnapshot(
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        decimal Gmv,
        decimal CustomerCharges,
        decimal Refunds,
        decimal Deductions,
        decimal PlatformCommission,
        decimal OwnerPayout,
        decimal NetRevenue,
        int PaidLedgerEntryCount);

    public sealed record MonthlyRevenuePoint(int Year, int Month, decimal NetRevenue);
    public sealed record DailyRevenuePoint(DateTime DateUtc, decimal NetRevenue);

    public interface IRevenueMetricsService
    {
        Task<RevenueMetricsSnapshot> GetSnapshotAsync(DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MonthlyRevenuePoint>> GetMonthlyNetRevenueAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DailyRevenuePoint>> GetDailyNetRevenueAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
    }

    public sealed class RevenueMetricsService : IRevenueMetricsService
    {
        private static readonly string[] PostedStatuses =
        {
            PaymentStatusConstants.Paid,
            PaymentStatusConstants.Refunded
        };

        private readonly ApplicationDbContext _context;

        public RevenueMetricsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RevenueMetricsSnapshot> GetSnapshotAsync(DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default)
        {
            var entries = await PostedLedgerEntries(periodStartUtc, periodEndUtc)
                .Select(l => new
                {
                    l.EntryType,
                    l.Amount
                })
                .ToListAsync(cancellationToken);

            var charges = entries.Where(e => e.EntryType == PaymentLedgerEntryTypeConstants.DepositCharge).Sum(e => e.Amount);
            var refunds = entries.Where(e => e.EntryType == PaymentLedgerEntryTypeConstants.Refund).Sum(e => e.Amount);
            var deductions = entries.Where(e => e.EntryType == PaymentLedgerEntryTypeConstants.Deduction).Sum(e => e.Amount);
            var payouts = entries.Where(e => e.EntryType == PaymentLedgerEntryTypeConstants.Payout).Sum(e => e.Amount);
            var commission = 0m;
            var ownerPayout = payouts;
            var netRevenue = deductions;

            return new RevenueMetricsSnapshot(
                periodStartUtc,
                periodEndUtc,
                charges,
                charges,
                refunds,
                deductions,
                commission,
                ownerPayout,
                netRevenue,
                entries.Count);
        }

        public async Task<IReadOnlyList<MonthlyRevenuePoint>> GetMonthlyNetRevenueAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
        {
            var rows = await PostedLedgerEntries(startUtc, endUtc)
                .Select(l => new
                {
                    l.CreatedAt.Year,
                    l.CreatedAt.Month,
                    l.EntryType,
                    l.Amount
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => new { r.Year, r.Month })
                .Select(g =>
                {
                    var deductions = g.Where(r => r.EntryType == PaymentLedgerEntryTypeConstants.Deduction).Sum(r => r.Amount);
                    return new MonthlyRevenuePoint(g.Key.Year, g.Key.Month, deductions);
                })
                .OrderBy(p => p.Year)
                .ThenBy(p => p.Month)
                .ToList();
        }

        public async Task<IReadOnlyList<DailyRevenuePoint>> GetDailyNetRevenueAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
        {
            var rows = await PostedLedgerEntries(startUtc, endUtc)
                .Select(l => new
                {
                    Date = l.CreatedAt.Date,
                    l.EntryType,
                    l.Amount
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(row => row.Date)
                .Select(group =>
                {
                    var deductions = group.Where(row => row.EntryType == PaymentLedgerEntryTypeConstants.Deduction).Sum(row => row.Amount);
                    return new DailyRevenuePoint(DateTime.SpecifyKind(group.Key, DateTimeKind.Utc), deductions);
                })
                .OrderBy(point => point.DateUtc)
                .ToList();
        }

        private IQueryable<PaymentLedgerEntry> PostedLedgerEntries(DateTime startUtc, DateTime endUtc)
        {
            return _context.PaymentLedgerEntries
                .AsNoTracking()
                .Include(l => l.Booking!)
                    .ThenInclude(b => b.Car)
                .Where(l => l.CreatedAt >= startUtc &&
                    l.CreatedAt < endUtc &&
                    PostedStatuses.Contains(l.Status));
        }
    }

    public interface IPartnerSettlementService
    {
        Task<PartnerPayoutStatement> CreateDraftStatementAsync(int partnerUserId, DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default);
    }

    public sealed class PartnerSettlementService : IPartnerSettlementService
    {
        private readonly ApplicationDbContext _context;

        public PartnerSettlementService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PartnerPayoutStatement> CreateDraftStatementAsync(int partnerUserId, DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default)
        {
            var key = $"partner-payout:{partnerUserId}:{periodStartUtc:yyyyMMddHHmmss}:{periodEndUtc:yyyyMMddHHmmss}";
            var existing = await _context.PartnerPayoutStatements
                .FirstOrDefaultAsync(s => s.IdempotencyKey == key, cancellationToken);
            if (existing != null)
                return existing;

            var bookings = await _context.Bookings.AsNoTracking()
                .Include(b => b.Car)
                .Include(b => b.BookingStatus)
                .Where(b => b.Car != null &&
                    b.Car.OwnerID == partnerUserId &&
                    b.BookingStatus != null &&
                    b.BookingStatus.Name == BookingStatusConstants.Completed &&
                    b.EndDate >= periodStartUtc &&
                    b.EndDate < periodEndUtc)
                .Select(b => new
                {
                    b.BookingID,
                    b.TotalPrice
                })
                .ToListAsync(cancellationToken);

            var bookingIds = bookings.Select(b => b.BookingID).ToList();
            var refunds = await _context.PaymentLedgerEntries.AsNoTracking()
                .Where(l => l.BookingID != null &&
                    bookingIds.Contains(l.BookingID.Value) &&
                    l.EntryType == PaymentLedgerEntryTypeConstants.Refund &&
                    l.Status == PaymentStatusConstants.Refunded)
                .SumAsync(l => (decimal?)l.Amount, cancellationToken) ?? 0m;

            var deductions = await _context.PaymentLedgerEntries.AsNoTracking()
                .Where(l => l.BookingID != null &&
                    bookingIds.Contains(l.BookingID.Value) &&
                    l.EntryType == PaymentLedgerEntryTypeConstants.Deduction &&
                    l.Status == PaymentStatusConstants.Paid)
                .SumAsync(l => (decimal?)l.Amount, cancellationToken) ?? 0m;

            var gross = bookings.Sum(b => b.TotalPrice);
            var commission = 0m;
            var ownerPayout = gross;
            var breakdown = bookings.Select(b => new
            {
                b.BookingID,
                b.TotalPrice,
                DepositDeductions = deductions,
                PlatformCommission = 0m
            });

            var statement = new PartnerPayoutStatement
            {
                PartnerUserID = partnerUserId,
                PeriodStartUtc = periodStartUtc,
                PeriodEndUtc = periodEndUtc,
                CompletedBookingCount = bookings.Count,
                GrossBookingAmount = gross,
                PlatformCommissionAmount = commission,
                OwnerPayoutAmount = ownerPayout,
                RefundAmount = refunds,
                DeductionAmount = deductions,
                IdempotencyKey = key,
                BookingBreakdownJson = JsonSerializer.Serialize(breakdown)
            };

            _context.PartnerPayoutStatements.Add(statement);
            await _context.SaveChangesAsync(cancellationToken);
            return statement;
        }
    }
}
