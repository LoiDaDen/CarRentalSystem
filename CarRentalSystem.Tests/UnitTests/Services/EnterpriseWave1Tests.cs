using CarRentalSystem.Data;
using CarRentalSystem.Areas.Admin.Controllers;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class EnterpriseWave1Tests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public EnterpriseWave1Tests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
        }

        [Fact]
        [Trait("Module", "EnterpriseWave1")]
        [Trait("Type", "Regression")]
        public async Task ConfirmManualPayment_CreatesLedgerAndHashAudit()
        {
            var booking = CreateBooking();
            var payment = CreatePayment(booking.BookingID, "Cash", PaymentStatusConstants.Pending);
            var service = CreatePaymentTransitionService();

            var result = await service.ConfirmManualPaymentAsync(payment.PaymentID, 1, "Admin", "cash received");

            result.IsSuccess.Should().BeTrue();
            _context.Payments.Single(p => p.PaymentID == payment.PaymentID).PaymentStatus.Should().Be(PaymentStatusConstants.Paid);
            _context.Bookings.Single(b => b.BookingID == booking.BookingID).DepositStatus.Should().Be(DepositStatusConstants.Holding);
            _context.PaymentLedgerEntries.Count(l => l.IdempotencyKey == $"manual-payment-confirmed:{payment.PaymentID}")
                .Should().Be(1);
            _context.AuditLogs.Should().Contain(a =>
                a.Action == "ConfirmManualPayment" &&
                !string.IsNullOrWhiteSpace(a.RecordHash));
        }

        [Fact]
        [Trait("Module", "EnterpriseWave1")]
        [Trait("Type", "Regression")]
        public async Task ConfirmManualPayment_RejectsPayOSPayment()
        {
            var booking = CreateBooking();
            var payment = CreatePayment(booking.BookingID, "PayOS", PaymentStatusConstants.Pending);
            var service = CreatePaymentTransitionService();

            var result = await service.ConfirmManualPaymentAsync(payment.PaymentID, 1, "Admin", "manual override");

            result.IsSuccess.Should().BeFalse();
            _context.Payments.Single(p => p.PaymentID == payment.PaymentID).PaymentStatus.Should().Be(PaymentStatusConstants.Pending);
            _context.PaymentLedgerEntries.Should().NotContain(l => l.PaymentID == payment.PaymentID);
        }

        [Fact]
        [Trait("Module", "EnterpriseWave1")]
        [Trait("Type", "Regression")]
        public async Task ConfirmManualPayment_DuplicateDoesNotDoubleWriteLedger()
        {
            var booking = CreateBooking();
            var payment = CreatePayment(booking.BookingID, "Cash", PaymentStatusConstants.Pending);
            var service = CreatePaymentTransitionService();

            await service.ConfirmManualPaymentAsync(payment.PaymentID, 1, "Admin", "cash received");
            await service.ConfirmManualPaymentAsync(payment.PaymentID, 1, "Admin", "cash received");

            _context.PaymentLedgerEntries.Count(l => l.IdempotencyKey == $"manual-payment-confirmed:{payment.PaymentID}")
                .Should().Be(1);
        }

        [Fact]
        [Trait("Module", "EnterpriseWave1")]
        [Trait("Type", "Regression")]
        public async Task PayOSExistingBookingPaid_CreatesOneLedgerAndProcessesWebhook()
        {
            var booking = CreateBooking();
            var payment = CreatePayment(booking.BookingID, "PayOS", PaymentStatusConstants.Pending);
            var transaction = new PayOSTransaction
            {
                BookingID = booking.BookingID,
                Booking = booking,
                PaymentID = payment.PaymentID,
                Payment = payment,
                UserID = booking.UserID,
                OrderCode = 900001,
                Amount = 250000,
                Status = PaymentStatusConstants.Pending
            };
            var webhookEvent = new PaymentWebhookEvent
            {
                Provider = "PayOS",
                EventKey = "payos:webhook:900001:00:250000",
                OrderCode = 900001,
                Status = "Received",
                PayloadHash = "hash"
            };
            _context.PayOSTransactions.Add(transaction);
            _context.PaymentWebhookEvents.Add(webhookEvent);
            _context.SaveChanges();
            var service = CreatePaymentTransitionService();

            var result = await service.MarkPayOSExistingBookingPaidAsync(transaction, webhookEvent);
            await service.MarkPayOSExistingBookingPaidAsync(transaction, webhookEvent);

            result.IsSuccess.Should().BeTrue();
            payment.PaymentStatus.Should().Be(PaymentStatusConstants.Paid);
            transaction.Status.Should().Be(PaymentStatusConstants.Paid);
            webhookEvent.Status.Should().Be("Processed");
            _context.PaymentLedgerEntries.Count(l => l.IdempotencyKey == "payos-paid:900001").Should().Be(1);
        }

        [Fact]
        [Trait("Module", "EnterpriseWave1")]
        [Trait("Type", "Regression")]
        public async Task SettlementReview_RequiresApprovalBeforeDeductionLedger()
        {
            var booking = CreateBooking();
            var settlement = CreateSettlementReviewService();

            var review = await settlement.CreateAsync(
                booking.BookingID,
                SettlementReviewTypeConstants.Late,
                120000,
                "late return",
                1);

            _context.PaymentLedgerEntries.Should().NotContain(l =>
                l.IdempotencyKey == $"settlement-review:{review.SettlementReviewID}:approved");

            var approveResult = await settlement.ApproveAsync(review.SettlementReviewID, 1, "approved");
            await settlement.ApproveAsync(review.SettlementReviewID, 1, "duplicate approval");

            approveResult.IsSuccess.Should().BeTrue();
            _context.PaymentLedgerEntries.Count(l =>
                l.IdempotencyKey == $"settlement-review:{review.SettlementReviewID}:approved").Should().Be(1);
        }

        [Fact]
        [Trait("Module", "EnterpriseWave1")]
        [Trait("Type", "Regression")]
        public async Task SetBookingDepositStatus_WritesHashAudit()
        {
            var booking = CreateBooking();
            var service = CreatePaymentTransitionService();

            var result = await service.SetBookingDepositStatusAsync(
                booking.BookingID,
                DepositStatusConstants.UnderReview,
                1,
                "Admin",
                "return requires settlement review");

            result.IsSuccess.Should().BeTrue();
            _context.Bookings.Single(b => b.BookingID == booking.BookingID).DepositStatus.Should().Be(DepositStatusConstants.UnderReview);
            _context.AuditLogs.Should().Contain(a =>
                a.Action == "BookingDepositTransition" &&
                !string.IsNullOrWhiteSpace(a.RecordHash));
        }

        [Fact]
        [Trait("Module", "EnterpriseWave1")]
        [Trait("Type", "Regression")]
        public async Task SnapshotDetails_ProductionRedactsRawDataJsonAndAudits()
        {
            var snapshot = new DbSnapshot
            {
                SnapshotID = 10,
                Name = "production snapshot",
                DataJson = "{\"Users\":[{\"Email\":\"customer@example.com\"}]}",
                Status = "Ready"
            };
            var snapshotService = new Mock<ISnapshotService>();
            snapshotService.Setup(s => s.GetSnapshotByIdAsync(snapshot.SnapshotID)).ReturnsAsync(snapshot);
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.EnvironmentName).Returns("Production");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Snapshot:AllowUnsafeDetails"] = "false",
                    ["Audit:HashChainEnabled"] = "true"
                })
                .Build();
            var controller = new SnapshotController(
                snapshotService.Object,
                configuration,
                environment.Object,
                CreateAuditTrailService());
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(
                            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1") },
                            "Cookies"))
                }
            };

            var result = await controller.Details(snapshot.SnapshotID);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<DbSnapshot>().Subject;
            model.DataJson.Should().Be("{}");
            _context.AuditLogs.Should().Contain(a =>
                a.Action == "SnapshotDetailsMetadataOnly" &&
                !string.IsNullOrWhiteSpace(a.RecordHash));
        }

        private Booking CreateBooking()
        {
            var booking = new Booking
            {
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(2),
                EndDate = DateTime.UtcNow.AddDays(3),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalHours = 24,
                TotalPrice = 1000000,
                BookingStatusID = 2,
                DepositAmount = 5000000,
                DepositStatus = DepositStatusConstants.Unpaid
            };
            _context.Bookings.Add(booking);
            _context.SaveChanges();
            return booking;
        }

        private Payment CreatePayment(int bookingId, string method, string status)
        {
            var payment = new Payment
            {
                BookingID = bookingId,
                Amount = 250000,
                PaymentMethod = method,
                PaymentStatus = status,
                PaymentDate = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            _context.SaveChanges();
            return payment;
        }

        private IPaymentTransitionService CreatePaymentTransitionService()
        {
            return new PaymentTransitionService(
                _context,
                new PaymentLedgerService(_context),
                CreateAuditTrailService(),
                Mock.Of<IReservationService>());
        }

        private ISettlementReviewService CreateSettlementReviewService()
        {
            var telemetry = new Mock<IEnterpriseTelemetryService>();
            telemetry.Setup(t => t.StartActivity(It.IsAny<string>(), It.IsAny<KeyValuePair<string, object?>[]>()))
                .Returns((System.Diagnostics.Activity?)null);

            return new SettlementReviewService(
                _context,
                new PaymentLedgerService(_context),
                CreateAuditTrailService(),
                telemetry.Object,
                Mock.Of<IProductAnalyticsService>());
        }

        private IAuditTrailService CreateAuditTrailService()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Audit:HashChainEnabled"] = "true"
                })
                .Build();

            return new AuditTrailService(
                _context,
                new HttpContextAccessor(),
                configuration);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
