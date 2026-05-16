using System.IO.Compression;
using System.Text;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class EnterprisePhase2Tests
    {
        [Fact]
        [Trait("Module", "Audit")]
        [Trait("Type", "Unit")]
        public async Task AuditTrail_RecordAsync_CreatesHashChain()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            var service = CreateAuditService(context);

            var first = await service.RecordAsync(new AuditLog { Action = "Create", EntityType = "Booking", EntityId = 1, UserId = 1 });
            var second = await service.RecordAsync(new AuditLog { Action = "Update", EntityType = "Booking", EntityId = 1, UserId = 1, Reason = "test" });

            first.RecordHash.Should().NotBeNullOrWhiteSpace();
            second.PreviousHash.Should().Be(first.RecordHash);
            second.RecordHash.Should().NotBeNullOrWhiteSpace();
            var verification = await service.VerifyAsync();
            verification.IsValid.Should().BeTrue();
            verification.CheckedCount.Should().BeGreaterThanOrEqualTo(2);
        }

        [Fact]
        [Trait("Module", "Audit")]
        [Trait("Type", "Unit")]
        public async Task AuditTrail_VerifyAsync_FailsAfterTampering()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            var service = CreateAuditService(context);
            var entry = await service.RecordAsync(new AuditLog { Action = "Create", EntityType = "Payment", EntityId = 7, UserId = 1 });

            entry.NewValues = "{\"tampered\":true}";
            await context.SaveChangesAsync();

            var verification = await service.VerifyAsync();

            verification.IsValid.Should().BeFalse();
            verification.FailedAuditLogID.Should().Be(entry.AuditLogID);
        }

        [Fact]
        [Trait("Module", "Evidence")]
        [Trait("Type", "Unit")]
        public void EvidenceHash_HandoverHashChanges_WhenEvidenceChanges()
        {
            var service = new EvidenceHashService();
            var record = new HandoverRecord
            {
                BookingID = 1,
                Type = "CheckIn",
                Odo = 100,
                FuelLevel = "F",
                FrontImgUrl = "front-a.jpg",
                RecordDate = new DateTime(2026, 5, 16, 0, 0, 0, DateTimeKind.Utc)
            };

            var first = service.ComputeHandoverEvidenceHash(record);
            record.FrontImgUrl = "front-b.jpg";
            var second = service.ComputeHandoverEvidenceHash(record);

            first.Should().NotBe(second);
        }

        [Fact]
        [Trait("Module", "Settlement")]
        [Trait("Type", "Unit")]
        public async Task SettlementReview_ApprovalCreatesLedgerOnlyAfterApproval()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            SeedBooking(context);
            var audit = CreateAuditService(context);
            var ledger = new PaymentLedgerService(context);
            var service = new SettlementReviewService(
                context,
                ledger,
                audit,
                new EnterpriseTelemetryService(),
                new NoopProductAnalyticsService(NullLogger<NoopProductAnalyticsService>.Instance));

            var review = await service.CreateAsync(1, SettlementReviewTypeConstants.Damage, 250000m, "scratch", 1);

            context.PaymentLedgerEntries.Should().NotContain(l => l.IdempotencyKey == $"settlement-review:{review.SettlementReviewID}:approved");

            var result = await service.ApproveAsync(review.SettlementReviewID, 1, "approved");

            result.IsSuccess.Should().BeTrue();
            context.PaymentLedgerEntries.Should().Contain(l => l.IdempotencyKey == $"settlement-review:{review.SettlementReviewID}:approved");
        }

        [Fact]
        [Trait("Module", "Dispute")]
        [Trait("Type", "Unit")]
        public async Task DisputeExport_IncludesManifestLedgerAndAudit()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            SeedBooking(context);
            var audit = CreateAuditService(context);
            var hash = new EvidenceHashService();
            var export = new DisputeExportService(
                context,
                audit,
                hash,
                new EnterpriseTelemetryService(),
                new NoopProductAnalyticsService(NullLogger<NoopProductAnalyticsService>.Instance));
            await audit.RecordAsync(new AuditLog { Action = "TestBooking", EntityType = nameof(Booking), EntityId = 1, UserId = 1 });
            context.PaymentLedgerEntries.Add(new PaymentLedgerEntry
            {
                BookingID = 1,
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = 100000m,
                Status = PaymentStatusConstants.Paid,
                IdempotencyKey = "test-ledger"
            });
            await context.SaveChangesAsync();

            var result = await export.ExportBookingAsync(1, 1);

            result.Should().NotBeNull();
            using var archive = new ZipArchive(new MemoryStream(result!.Content), ZipArchiveMode.Read);
            archive.GetEntry("manifest.json").Should().NotBeNull();
            archive.GetEntry("payment-ledger.json").Should().NotBeNull();
            archive.GetEntry("audit-trail.json").Should().NotBeNull();
            result.ContentHash.Should().HaveLength(64);
        }

        private static AuditTrailService CreateAuditService(ApplicationDbContext context)
        {
            var http = new DefaultHttpContext();
            http.TraceIdentifier = "test-correlation";
            var accessor = new HttpContextAccessor { HttpContext = http };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Audit:HashChainEnabled"] = "true"
                })
                .Build();

            return new AuditTrailService(context, accessor, configuration);
        }

        private static void SeedBooking(ApplicationDbContext context)
        {
            if (context.Bookings.Any(b => b.BookingID == 1))
                return;

            context.Bookings.Add(new Booking
            {
                BookingID = 1,
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(2),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalPrice = 1000000m,
                TotalHours = 24,
                BookingStatusID = 3,
                DepositAmount = 5000000m,
                DepositStatus = DepositStatusConstants.Holding
            });
            context.SaveChanges();
        }
    }
}
