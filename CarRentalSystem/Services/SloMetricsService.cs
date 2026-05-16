using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ISloMetricsService
    {
        Task<SloDashboard> GetDashboardAsync(TimeSpan? window = null, CancellationToken cancellationToken = default);
    }

    public record SloMetric(string Name, decimal? Value, decimal Target, string Unit, string Status, string Description);

    public record SloDashboard(DateTime WindowStartUtc, DateTime WindowEndUtc, IReadOnlyList<SloMetric> Metrics, decimal ErrorBudgetRemainingPercent);

    public class SloMetricsService : ISloMetricsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public SloMetricsService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<SloDashboard> GetDashboardAsync(TimeSpan? window = null, CancellationToken cancellationToken = default)
        {
            var end = DateTime.UtcNow;
            var start = end.Subtract(window ?? TimeSpan.FromDays(30));
            var availabilityTarget = _configuration.GetValue("Sre:SloAvailabilityTarget", 99.9m);

            var webhookCount = await _context.PaymentWebhookEvents.CountAsync(e => e.ReceivedAt >= start && e.ReceivedAt <= end, cancellationToken);
            var webhookProcessed = await _context.PaymentWebhookEvents.CountAsync(e => e.ReceivedAt >= start && e.ReceivedAt <= end && e.Status == "Processed", cancellationToken);
            var webhookSuccess = Percent(webhookProcessed, webhookCount);

            var webhookLatencyPairs = await _context.PaymentWebhookEvents
                .Where(e => e.ReceivedAt >= start && e.ReceivedAt <= end && e.ProcessedAt != null)
                .Select(e => new { e.ReceivedAt, e.ProcessedAt })
                .ToListAsync(cancellationToken);
            var webhookLatencies = webhookLatencyPairs
                .Select(e => (int)Math.Max(0, (e.ProcessedAt!.Value - e.ReceivedAt).TotalMilliseconds))
                .ToList();
            var webhookP95 = Percentile(webhookLatencies, 0.95m);

            var bookingCount = await _context.Bookings.CountAsync(b => b.CreatedAt >= start && b.CreatedAt <= end, cancellationToken);
            var completedOrConfirmed = await _context.Bookings.CountAsync(b => b.CreatedAt >= start && b.CreatedAt <= end &&
                b.BookingStatus != null &&
                (b.BookingStatus.Name == "Confirmed" || b.BookingStatus.Name == "Completed" || b.BookingStatus.Name == "Active"), cancellationToken);
            var checkoutSuccess = Percent(completedOrConfirmed, bookingCount);

            var holdCount = await _context.BookingHolds.CountAsync(h => h.CreatedAt >= start && h.CreatedAt <= end, cancellationToken);
            var conflictCount = await _context.AuditLogs.CountAsync(a => a.Timestamp >= start && a.Timestamp <= end &&
                (a.Action.Contains("BookingConflict") || (a.Description != null && a.Description.Contains("conflict"))), cancellationToken);
            var conflictRate = Percent(conflictCount, holdCount);

            var jobFreshness = await GetJobFreshnessMinutesAsync(start, cancellationToken);
            var availability = await EstimateAvailabilityAsync(start, end, cancellationToken);
            var errorBudgetRemaining = CalculateErrorBudgetRemaining(availability, availabilityTarget);

            var metrics = new List<SloMetric>
            {
                new("availability", availability, availabilityTarget, "%", StatusAtLeast(availability, availabilityTarget), "Estimated availability from app/system audit events."),
                new("checkout_success", checkoutSuccess, 95m, "%", StatusAtLeast(checkoutSuccess, 95m), "Confirmed/completed/active bookings over created bookings."),
                new("payment_webhook_success", webhookSuccess, 99m, "%", StatusAtLeast(webhookSuccess, 99m), "Processed PayOS webhook events over received events."),
                new("payment_webhook_p95_latency", webhookP95, 60000m, "ms", StatusAtMost(webhookP95, 60000m), "P95 webhook received-to-processed latency."),
                new("booking_conflict_rate", conflictRate, 1m, "%", StatusAtMost(conflictRate, 1m), "Conflict holds over all holds."),
                new("job_freshness", jobFreshness, 15m, "minutes", StatusAtMost(jobFreshness, 15m), "Minutes since latest background-job audit signal.")
            };

            return new SloDashboard(start, end, metrics, errorBudgetRemaining);
        }

        private async Task<decimal?> EstimateAvailabilityAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            var outageSignals = await _context.AuditLogs.CountAsync(a =>
                a.Timestamp >= start && a.Timestamp <= end &&
                (a.Action.Contains("Outage") || a.Action.Contains("HealthCheckFailed")), cancellationToken);
            if (outageSignals == 0)
                return 100m;

            var windowMinutes = Math.Max(1m, (decimal)(end - start).TotalMinutes);
            var estimatedOutageMinutes = outageSignals * 5m;
            return Math.Max(0m, Math.Round((windowMinutes - estimatedOutageMinutes) / windowMinutes * 100m, 3));
        }

        private async Task<decimal?> GetJobFreshnessMinutesAsync(DateTime start, CancellationToken cancellationToken)
        {
            var latest = await _context.AuditLogs
                .Where(a => a.Timestamp >= start && (a.EntityType == "Hangfire" || a.Action.Contains("Job")))
                .OrderByDescending(a => a.Timestamp)
                .Select(a => (DateTime?)a.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            return latest.HasValue ? Math.Round((decimal)(DateTime.UtcNow - latest.Value).TotalMinutes, 2) : null;
        }

        private static decimal? Percent(int numerator, int denominator)
        {
            return denominator == 0 ? null : Math.Round((decimal)numerator / denominator * 100m, 3);
        }

        private static decimal? Percentile(List<int> values, decimal percentile)
        {
            if (values.Count == 0)
                return null;
            values.Sort();
            var index = (int)Math.Ceiling(values.Count * percentile) - 1;
            index = Math.Clamp(index, 0, values.Count - 1);
            return values[index];
        }

        private static string StatusAtLeast(decimal? value, decimal target)
        {
            return value.HasValue ? (value.Value >= target ? "Healthy" : "Burning") : "Unknown";
        }

        private static string StatusAtMost(decimal? value, decimal target)
        {
            return value.HasValue ? (value.Value <= target ? "Healthy" : "Burning") : "Unknown";
        }

        private static decimal CalculateErrorBudgetRemaining(decimal? availability, decimal target)
        {
            if (!availability.HasValue)
                return 0m;
            var allowedFailure = 100m - target;
            if (allowedFailure <= 0)
                return availability.Value >= target ? 100m : 0m;
            var actualFailure = Math.Max(0m, 100m - availability.Value);
            return Math.Clamp(Math.Round((allowedFailure - actualFailure) / allowedFailure * 100m, 2), 0m, 100m);
        }
    }
}
