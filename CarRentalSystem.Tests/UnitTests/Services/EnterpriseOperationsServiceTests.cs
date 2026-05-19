using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class EnterpriseOperationsServiceTests : IDisposable
    {
        private readonly CarRentalSystem.Data.ApplicationDbContext _context;

        public EnterpriseOperationsServiceTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
        }

        [Fact]
        [Trait("Module", "EnterpriseCommandCenter")]
        [Trait("Type", "Regression")]
        public async Task CommandCenter_SurfacesP0QueuesFromExistingEnterpriseData()
        {
            var now = DateTime.UtcNow;
            SeedEnterpriseSignals(now);
            var service = new EnterpriseOperationsService(_context);

            var snapshot = await service.GetCommandCenterAsync();

            snapshot.Queues.Single(q => q.Title == "Overdue returns").Count.Should().Be(1);
            snapshot.Queues.Single(q => q.Title == "Expired active holds").Count.Should().Be(1);
            snapshot.Queues.Single(q => q.Title == "Settlement reviews").Count.Should().Be(1);
            snapshot.Queues.Single(q => q.Title == "Refund queue").Count.Should().Be(1);
            snapshot.Queues.Single(q => q.Title == "PayOS reconciliation").Count.Should().Be(2);
            snapshot.Queues.Single(q => q.Title == "Support SLA breach").Count.Should().Be(1);
            snapshot.Queues.Single(q => q.Title == "Privacy requests").Count.Should().Be(1);
            snapshot.Queues.Single(q => q.Title == "Contract evidence").Count.Should().Be(2);
        }

        [Fact]
        [Trait("Module", "EnterpriseCommandCenter")]
        [Trait("Type", "Regression")]
        public async Task CommandCenter_MapsRoadmapCapabilitiesToRuntimeReadiness()
        {
            SeedEnterpriseSignals(DateTime.UtcNow);
            var service = new EnterpriseOperationsService(_context);

            var snapshot = await service.GetCommandCenterAsync();

            snapshot.Capabilities.Should().HaveCount(14);
            snapshot.Capabilities.Single(c => c.Module == "Payment, ledger, refund, payout")
                .Status.Should().Be("NeedsEvidence");
            snapshot.WorkflowGroups.Select(g => g.Role).Should().Contain(new[]
            {
                "Customer operations",
                "Finance operations",
                "Fleet and area operations",
                "Security and compliance"
            });
        }

        private void SeedEnterpriseSignals(DateTime now)
        {
            _context.Bookings.Add(new Booking
            {
                BookingID = 1001,
                UserID = 2,
                CarID = 1,
                StartDate = now.AddDays(-2),
                EndDate = now.AddHours(-2),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalHours = 48,
                TotalPrice = 2000000m,
                BookingStatusID = 5,
                BookingStatus = _context.BookingStatuses.Single(s => s.Name == BookingStatusConstants.Active),
                DepositStatus = DepositStatusConstants.Holding
            });

            _context.BookingHolds.Add(new BookingHold
            {
                UserID = 2,
                CarID = 2,
                StartDate = now.AddDays(1),
                EndDate = now.AddDays(2),
                ExpiresAt = now.AddMinutes(-5),
                Status = BookingHoldStatusConstants.Active
            });

            _context.PayOSTransactions.Add(new PayOSTransaction
            {
                OrderCode = 500001,
                Amount = 500000m,
                Status = PaymentStatusConstants.Paid,
                PaidAt = now
            });

            _context.PaymentLedgerEntries.Add(new PaymentLedgerEntry
            {
                Provider = "PayOS",
                OrderCode = 500002,
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = 500000m,
                Status = PaymentStatusConstants.Paid,
                CreatedAt = now
            });

            _context.SettlementReviews.Add(new SettlementReview
            {
                BookingID = 1001,
                ReviewType = SettlementReviewTypeConstants.Damage,
                Amount = 300000m,
                Status = SettlementReviewStatusConstants.PendingReview,
                Reason = "scratch"
            });

            _context.RefundRequests.Add(new RefundRequest
            {
                BookingID = 1001,
                Amount = 100000m,
                Reason = "customer cancellation",
                Status = RefundRequestStatusConstants.PendingReview
            });

            _context.SupportTickets.Add(new SupportTicket
            {
                UserID = 2,
                TicketType = SupportTicketTypeConstants.Dispute,
                Priority = SupportTicketPriorityConstants.P1,
                Status = SupportTicketStatusConstants.Open,
                Subject = "Deposit dispute",
                Description = "Need review",
                FirstResponseDueAt = now.AddMinutes(-30)
            });

            _context.PrivacyRequests.Add(new PrivacyRequest
            {
                UserID = 2,
                RequestType = PrivacyRequestTypeConstants.Export,
                Status = PrivacyRequestStatusConstants.PendingReview,
                Reason = "export"
            });

            _context.DigitalContracts.Add(new DigitalContract
            {
                ContractNo = "DC-1001",
                BookingID = 1001,
                CustomerName = "Customer Test",
                CustomerIdentityCard = "012345678901",
                CustomerPhone = "0900000002",
                CustomerAddress = "Test address",
                CarLicensePlate = "51A-12345",
                CarModel = "Camry 2024",
                RentalStartDate = now.AddDays(-2),
                RentalEndDate = now.AddHours(-2),
                TermsAndConditions = "Terms",
                IsSignedByCustomer = true,
                IsSignedByOwner = false
            });

            _context.HandoverRecords.Add(new HandoverRecord
            {
                BookingID = 1001,
                Type = "CheckOut",
                RecordDate = now,
                Odo = 10000,
                FuelLevel = "F"
            });

            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
