using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PayOS.Models.Webhooks;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class PayOSPaymentWebhookTests
    {
        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task HandleWebhook_ReturnsNotConfigured_WhenGatewayDisabled()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var service = CreateService(context, configured: false);

            var result = await service.HandleWebhookAsync(CreateWebhook(1001, 200_000, "00"));

            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be("PayOS not configured");
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task HandleWebhook_RejectsInvalidSignature()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var service = CreateService(context, verifiedData: null);

            var result = await service.HandleWebhookAsync(CreateWebhook(1002, 200_000, "00"));

            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be("Invalid webhook signature");
            context.PaymentWebhookEvents.Should().BeEmpty();
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task HandleWebhook_RecordsUnknownOrderAsFailed()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var service = CreateService(context, verifiedData: CreateData(1003, 200_000, "00"));

            var result = await service.HandleWebhookAsync(CreateWebhook(1003, 200_000, "00"));

            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be("Transaction not found");
            context.PaymentWebhookEvents.Single().Status.Should().Be("Failed");
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task HandleWebhook_RejectsAmountMismatch()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            SeedPayOSTransaction(context, orderCode: 1004, amount: 200_000m);
            var service = CreateService(context, verifiedData: CreateData(1004, 150_000, "00"));

            var result = await service.HandleWebhookAsync(CreateWebhook(1004, 150_000, "00"));

            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be("Amount mismatch");
            context.PaymentWebhookEvents.Single().Status.Should().Be("Rejected");
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task HandleWebhook_IgnoresDuplicateProcessedEvent()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            SeedPayOSTransaction(context, orderCode: 1005, amount: 200_000m, status: PaymentStatusConstants.Paid);
            context.PaymentWebhookEvents.Add(new PaymentWebhookEvent
            {
                Provider = "PayOS",
                EventKey = "payos:webhook:1005:00:200000",
                PayloadHash = "hash",
                OrderCode = 1005,
                Status = "Processed"
            });
            await context.SaveChangesAsync();
            var service = CreateService(context, verifiedData: CreateData(1005, 200_000, "00"));

            var result = await service.HandleWebhookAsync(CreateWebhook(1005, 200_000, "00"));

            result.IsSuccess.Should().BeTrue();
            result.IsPaid.Should().BeTrue();
            result.Message.Should().Be("Duplicate webhook ignored");
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task HandleWebhook_ConfirmsExistingBookingPayment_WhenPayloadIsValid()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var transaction = SeedPayOSTransaction(context, orderCode: 1006, amount: 200_000m);
            var transition = new Mock<IPaymentTransitionService>();
            transition
                .Setup(s => s.MarkPayOSExistingBookingPaidAsync(It.IsAny<PayOSTransaction>(), It.IsAny<PaymentWebhookEvent>()))
                .ReturnsAsync((PayOSTransaction payos, PaymentWebhookEvent evt) =>
                {
                    evt.Status = "Processed";
                    payos.Status = PaymentStatusConstants.Paid;
                    return new PaymentTransitionResult(true, "Payment confirmed", null, payos.Booking);
                });
            var service = CreateService(context, verifiedData: CreateData(1006, 200_000, "00"), transition: transition);

            var result = await service.HandleWebhookAsync(CreateWebhook(1006, 200_000, "00"));

            result.IsSuccess.Should().BeTrue();
            result.IsPaid.Should().BeTrue();
            result.BookingId.Should().Be(transaction.BookingID);
            transition.Verify(s => s.MarkPayOSExistingBookingPaidAsync(
                It.Is<PayOSTransaction>(t => t.OrderCode == 1006),
                It.IsAny<PaymentWebhookEvent>()), Times.Once);
        }

        private static PayOSPaymentService CreateService(
            CarRentalSystem.Data.ApplicationDbContext context,
            bool configured = true,
            WebhookData? verifiedData = null,
            Mock<IPaymentTransitionService>? transition = null)
        {
            var gateway = new Mock<IPayOSGateway>();
            gateway.SetupGet(g => g.IsConfigured).Returns(configured);
            gateway.Setup(g => g.VerifyWebhookAsync(It.IsAny<Webhook>())).ReturnsAsync(verifiedData);

            return new PayOSPaymentService(
                context,
                Mock.Of<IEmailService>(),
                gateway.Object,
                Mock.Of<IPaymentLedgerService>(),
                transition?.Object ?? Mock.Of<IPaymentTransitionService>(),
                Mock.Of<IReservationService>(),
                Mock.Of<IReferralService>(),
                Mock.Of<ILongTermRentalService>(),
                Mock.Of<IAuditTrailService>(),
                Mock.Of<IDigitalContractService>(),
                NullLogger<PayOSPaymentService>.Instance);
        }

        private static PayOSTransaction SeedPayOSTransaction(
            CarRentalSystem.Data.ApplicationDbContext context,
            long orderCode,
            decimal amount,
            string status = PaymentStatusConstants.Pending)
        {
            var pending = context.BookingStatuses.Single(s => s.Name == BookingStatusConstants.Pending);
            var booking = new Booking
            {
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(2),
                EndDate = DateTime.UtcNow.AddDays(3),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalHours = 24,
                TotalPrice = 1_000_000m,
                ReservationFee = amount,
                BookingStatusID = pending.BookingStatusID
            };
            context.Bookings.Add(booking);
            context.SaveChanges();

            var transaction = new PayOSTransaction
            {
                BookingID = booking.BookingID,
                UserID = 2,
                OrderCode = orderCode,
                Amount = amount,
                Status = status
            };
            context.PayOSTransactions.Add(transaction);
            context.SaveChanges();
            return transaction;
        }

        private static Webhook CreateWebhook(long orderCode, long amount, string code)
        {
            return new Webhook
            {
                Code = "00",
                Success = true,
                Signature = "test-signature",
                Data = CreateData(orderCode, amount, code)
            };
        }

        private static WebhookData CreateData(long orderCode, long amount, string code)
        {
            return new WebhookData
            {
                OrderCode = orderCode,
                Amount = amount,
                Code = code,
                Description = "Webhook test",
                AccountNumber = "123456789",
                Reference = $"PAYOS-{orderCode}",
                TransactionDateTime = DateTime.UtcNow.ToString("O"),
                Currency = "VND",
                PaymentLinkId = $"plink-{orderCode}"
            };
        }
    }
}
