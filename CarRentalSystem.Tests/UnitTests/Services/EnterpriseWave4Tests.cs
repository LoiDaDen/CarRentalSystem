using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class EnterpriseWave4Tests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public EnterpriseWave4Tests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
        }

        [Fact]
        [Trait("Module", "EnterpriseWave4")]
        [Trait("Type", "Regression")]
        public async Task SupportTicket_CreateAndUpdate_WritesHashChainAudit()
        {
            var service = new SupportTicketService(_context, CreateAuditTrailService());

            var ticket = await service.CreateAsync(new SupportTicketCreateRequest(
                2,
                SupportTicketTypeConstants.Dispute,
                SupportTicketPriorityConstants.P1,
                "Refund dispute",
                "Need help",
                BookingId: null));
            var result = await service.UpdateStatusAsync(ticket.SupportTicketID, SupportTicketStatusConstants.InProgress, 1, "Admin", "triaged");

            result.IsSuccess.Should().BeTrue();
            _context.SupportTickets.Single(t => t.SupportTicketID == ticket.SupportTicketID).AssignedToUserID.Should().Be(1);
            _context.AuditLogs.Should().Contain(a => a.Action == "CreateSupportTicket" && !string.IsNullOrWhiteSpace(a.RecordHash));
            _context.AuditLogs.Should().Contain(a => a.Action == "UpdateSupportTicket" && !string.IsNullOrWhiteSpace(a.PreviousHash));
        }

        [Fact]
        [Trait("Module", "EnterpriseWave4")]
        [Trait("Type", "Regression")]
        public async Task CustomerSelfService_ReturnsOnlyOwnData()
        {
            _context.Bookings.Add(new Booking
            {
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(2),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalHours = 24,
                TotalPrice = 1000000,
                BookingStatusID = 2
            });
            _context.Bookings.Add(new Booking
            {
                UserID = 3,
                CarID = 2,
                StartDate = DateTime.UtcNow.AddDays(3),
                EndDate = DateTime.UtcNow.AddDays(4),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalHours = 24,
                TotalPrice = 1000000,
                BookingStatusID = 2
            });
            await _context.SaveChangesAsync();
            var service = new CustomerSelfServiceService(_context);

            var summary = await service.GetSummaryAsync(2);

            summary.RecentBookings.Should().OnlyContain(b => b.UserID == 2);
        }

        [Fact]
        [Trait("Module", "EnterpriseWave4")]
        [Trait("Type", "Regression")]
        public async Task ApiKey_CreateVerifyAndRevoke_StoresOnlyHash()
        {
            var service = new ApiKeyService(_context, CreateAuditTrailService());

            var created = await service.CreateAsync(1, "Partner sandbox", "bookings:read", DateTime.UtcNow.AddDays(7), 1);
            var verified = await service.VerifyAsync(created.RawKey);
            var revoked = await service.RevokeAsync(created.ApiKey.ApiKeyID, 1);
            var afterRevoke = await service.VerifyAsync(created.RawKey);

            created.ApiKey.KeyHash.Should().NotBe(created.RawKey);
            created.ApiKey.KeyPrefix.Should().NotBeNullOrWhiteSpace();
            verified.Should().NotBeNull();
            revoked.Should().BeTrue();
            afterRevoke.Should().BeNull();
        }

        [Fact]
        [Trait("Module", "EnterpriseWave4")]
        [Trait("Type", "Regression")]
        public void WebhookSigning_VerifiesValidSignatureAndRejectsStaleTimestamp()
        {
            var service = new WebhookSigningService();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signature = service.Sign("{\"event\":\"test\"}", "secret", now);

            service.Verify("{\"event\":\"test\"}", "secret", now, signature, TimeSpan.FromMinutes(5)).Should().BeTrue();
            service.Verify("{\"event\":\"test\"}", "secret", now - 3600, signature, TimeSpan.FromMinutes(5)).Should().BeFalse();
        }

        private IAuditTrailService CreateAuditTrailService()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Audit:HashChainEnabled"] = "true"
                })
                .Build();
            return new AuditTrailService(_context, new HttpContextAccessor(), configuration);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
