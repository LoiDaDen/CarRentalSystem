using System.Diagnostics;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class EnterprisePhase3Tests
    {
        [Fact]
        [Trait("Module", "Telemetry")]
        [Trait("Type", "Unit")]
        public void EnterpriseTelemetry_FiltersSensitiveTags()
        {
            var service = new EnterpriseTelemetryService();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == EnterpriseTelemetryService.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
            };
            ActivitySource.AddActivityListener(listener);

            using var activity = service.StartActivity(
                "phase3_test",
                new KeyValuePair<string, object?>("booking.id", 123),
                new KeyValuePair<string, object?>("user.email", "customer@example.com"),
                new KeyValuePair<string, object?>("token", "secret"));

            activity.Should().NotBeNull();
            activity!.TagObjects.Should().Contain(t => t.Key == "booking.id" && t.Value != null && t.Value.ToString() == "123");
            activity.TagObjects.Should().NotContain(t => t.Key.Contains("email", StringComparison.OrdinalIgnoreCase));
            activity.TagObjects.Should().NotContain(t => t.Key.Contains("token", StringComparison.OrdinalIgnoreCase));
            service.ContainsSensitiveTelemetryKey("citizen_id").Should().BeTrue();
        }

        [Fact]
        [Trait("Module", "Analytics")]
        [Trait("Type", "Unit")]
        public async Task AnalyticsNoop_DoesNotThrowAndRejectsSensitiveKeys()
        {
            var service = new NoopProductAnalyticsService(NullLogger<NoopProductAnalyticsService>.Instance);

            await service.TrackAsync("checkout_started", new Dictionary<string, object?>
            {
                ["booking_id"] = 1,
                ["customer_email"] = "customer@example.com",
                ["amount"] = 250000m
            });

            service.ContainsSensitiveProperty("customer_phone").Should().BeTrue();
            service.ContainsSensitiveProperty("booking_id").Should().BeFalse();
        }

        [Fact]
        [Trait("Module", "SRE")]
        [Trait("Type", "Unit")]
        public async Task SloMetrics_ReturnsDashboardWithErrorBudget()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            SeedSloData(context);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Sre:SloAvailabilityTarget"] = "99.9"
                })
                .Build();
            var service = new SloMetricsService(context, configuration);

            var dashboard = await service.GetDashboardAsync(TimeSpan.FromDays(1));

            dashboard.Metrics.Should().Contain(m => m.Name == "availability" && m.Status == "Healthy");
            dashboard.Metrics.Should().Contain(m => m.Name == "payment_webhook_success");
            dashboard.Metrics.Should().Contain(m => m.Name == "checkout_success");
            dashboard.ErrorBudgetRemainingPercent.Should().Be(100m);
        }

        private static void SeedSloData(ApplicationDbContext context)
        {
            var now = DateTime.UtcNow;
            if (!context.BookingStatuses.Any(s => s.BookingStatusID == 3))
            {
                context.BookingStatuses.Add(new BookingStatus { BookingStatusID = 3, Name = "Confirmed" });
            }

            context.Bookings.Add(new Booking
            {
                UserID = 2,
                CarID = 1,
                StartDate = now.AddHours(2),
                EndDate = now.AddHours(4),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalPrice = 500000m,
                TotalHours = 2,
                BookingStatusID = 3,
                BookingStatus = context.BookingStatuses.Local.FirstOrDefault(s => s.BookingStatusID == 3)
                    ?? context.BookingStatuses.First(s => s.BookingStatusID == 3),
                CreatedAt = now.AddMinutes(-30)
            });
            context.BookingHolds.Add(new BookingHold
            {
                UserID = 2,
                CarID = 1,
                StartDate = now.AddHours(2),
                EndDate = now.AddHours(4),
                ExpiresAt = now.AddHours(1),
                CreatedAt = now.AddMinutes(-35),
                Status = BookingHoldStatusConstants.Converted
            });
            context.PaymentWebhookEvents.Add(new PaymentWebhookEvent
            {
                Provider = "PayOS",
                EventKey = "payos:webhook:1:00:500000",
                PayloadHash = new string('a', 64),
                OrderCode = 1,
                Status = "Processed",
                ReceivedAt = now.AddMinutes(-10),
                ProcessedAt = now.AddMinutes(-9)
            });
            context.AuditLogs.Add(new AuditLog
            {
                Action = "JobCompleted",
                EntityType = "Hangfire",
                UserId = 1,
                Timestamp = now.AddMinutes(-1)
            });
            context.SaveChanges();
        }
    }
}
