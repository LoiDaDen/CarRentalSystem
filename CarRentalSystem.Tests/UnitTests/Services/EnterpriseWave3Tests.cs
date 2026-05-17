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
    public class EnterpriseWave3Tests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public EnterpriseWave3Tests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
        }

        [Fact]
        [Trait("Module", "EnterpriseWave3")]
        [Trait("Type", "Regression")]
        public async Task PrivacyRequest_WorkflowCreatesHashChainAuditAndManifest()
        {
            var service = CreatePrivacyService();

            var request = await service.CreateAsync(2, PrivacyRequestTypeConstants.Export, "customer export");
            var approve = await service.ApproveAsync(request.PrivacyRequestID, 1, "approved");
            var complete = await service.CompleteAsync(request.PrivacyRequestID, 1, "secure export prepared");

            approve.IsSuccess.Should().BeTrue();
            complete.IsSuccess.Should().BeTrue();
            complete.Request!.Status.Should().Be(PrivacyRequestStatusConstants.Completed);
            complete.Request.ExportManifestJson.Should().Contain("Bookings");
            _context.AuditLogs.Should().Contain(a => a.Action == "CreatePrivacyRequest" && !string.IsNullOrWhiteSpace(a.RecordHash));
            _context.AuditLogs.Should().Contain(a => a.Action == "CompletePrivacyRequest" && !string.IsNullOrWhiteSpace(a.PreviousHash));
        }

        [Fact]
        [Trait("Module", "EnterpriseWave3")]
        [Trait("Type", "Regression")]
        public async Task PrivacyDeleteRequest_DoesNotHardDeleteProtectedRecords()
        {
            var booking = new Booking
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
            };
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            var service = CreatePrivacyService();

            var request = await service.CreateAsync(2, PrivacyRequestTypeConstants.Delete, "delete me");
            await service.ApproveAsync(request.PrivacyRequestID, 1, "approved for review");
            await service.CompleteAsync(request.PrivacyRequestID, 1, "manual anonymization package prepared");

            _context.Users.Should().Contain(u => u.UserID == 2);
            _context.Bookings.Should().Contain(b => b.BookingID == booking.BookingID);
            _context.PrivacyRequests.Single(r => r.PrivacyRequestID == request.PrivacyRequestID).Status
                .Should().Be(PrivacyRequestStatusConstants.Completed);
        }

        [Fact]
        [Trait("Module", "EnterpriseWave3")]
        [Trait("Type", "Regression")]
        public async Task RetentionPreview_ReturnsCandidatesWithoutMutatingData()
        {
            _context.VehicleTrackings.Add(new VehicleTracking
            {
                CarID = 1,
                Latitude = 10,
                Longitude = 20,
                Timestamp = DateTime.UtcNow.AddDays(-120)
            });
            await _context.SaveChangesAsync();
            var trackingCount = _context.VehicleTrackings.Count();
            var service = CreatePrivacyService();

            var candidates = await service.PreviewRetentionAsync(DateTime.UtcNow);

            candidates.Should().Contain(c => c.DataDomain == "Tracking");
            _context.VehicleTrackings.Count().Should().Be(trackingCount);
        }

        private IPrivacyRequestService CreatePrivacyService()
        {
            return new PrivacyRequestService(_context, CreateAuditTrailService());
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
