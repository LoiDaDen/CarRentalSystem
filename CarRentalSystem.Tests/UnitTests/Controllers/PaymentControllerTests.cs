using System.Security.Claims;
using System.Text.Json;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using CarRentalSystem.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    public class PaymentControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IPayOSPaymentService> _payOSPaymentService;

        public PaymentControllerTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
            _payOSPaymentService = new Mock<IPayOSPaymentService>();
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task Hold_WhenPayOSHasNotConfirmed_RendersPendingWithHoldFlow()
        {
            var transaction = await SeedHoldTransactionAsync();
            _payOSPaymentService
                .Setup(s => s.ReconcilePaymentAsync(transaction.OrderCode, "return-hold"))
                .ReturnsAsync(new PayOSWebhookResult(true, false, "Payment status is PENDING", null, transaction.Amount));
            var controller = CreateController();

            var result = await controller.Hold(transaction.OrderCode);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().Be("Pending");
            var model = view.Model.Should().BeOfType<PayOSHoldPaymentViewModel>().Subject;
            model.PaymentFlow.Should().Be(PayOSPaymentFlow.Hold);
            model.OrderCode.Should().Be(transaction.OrderCode);
            model.QrCode.Should().Be(transaction.QrCode);
            controller.TempData["Info"].Should().Be("Chưa hoàn thành yêu cầu thanh toán. Vui lòng quét QR hoặc mở lại PayOS để thanh toán.");
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task ConfirmHold_WhenPayOSHasNotConfirmed_StaysOnPendingWithError()
        {
            var transaction = await SeedHoldTransactionAsync();
            _payOSPaymentService
                .Setup(s => s.ReconcilePaymentAsync(transaction.OrderCode, "confirm-hold"))
                .ReturnsAsync(new PayOSWebhookResult(true, false, "Payment status is PENDING", null, transaction.Amount));
            var controller = CreateController();

            var result = await controller.ConfirmHold(transaction.OrderCode);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().Be("Pending");
            view.Model.Should().BeOfType<PayOSHoldPaymentViewModel>()
                .Subject.PaymentFlow.Should().Be(PayOSPaymentFlow.Hold);
            controller.TempData["Error"].Should().Be("Chưa hoàn thành yêu cầu thanh toán. Vui lòng quét QR hoặc mở lại PayOS để thanh toán.");
            _payOSPaymentService.Verify(s => s.ReconcilePaymentAsync(transaction.OrderCode, "confirm-hold"), Times.Once);
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task ConfirmHold_WhenPayOSConfirmed_RedirectsToMyBookings()
        {
            var transaction = await SeedHoldTransactionAsync();
            _payOSPaymentService
                .Setup(s => s.ReconcilePaymentAsync(transaction.OrderCode, "confirm-hold"))
                .ReturnsAsync(new PayOSWebhookResult(true, true, "Payment confirmed - booking created", 77, transaction.Amount));
            var controller = CreateController();

            var result = await controller.ConfirmHold(transaction.OrderCode);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ControllerName.Should().Be("Booking");
            redirect.ActionName.Should().Be("MyBookings");
            controller.TempData["Success"].Should().Be("PayOS đã xác nhận thanh toán. Đơn thuê xe đã được tạo.");
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Regression")]
        public async Task CancelHoldReturn_GetCallback_RendersCancelPageAndMarksHoldCancelled()
        {
            var transaction = await SeedHoldTransactionAsync();
            var controller = CreateController();

            var result = await controller.CancelHoldReturn(transaction.OrderCode);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().Be("CancelHold");
            view.Model.Should().BeOfType<PayOSTransaction>()
                .Subject.OrderCode.Should().Be(transaction.OrderCode);
            _payOSPaymentService.Verify(s => s.MarkHoldCancelledAsync(transaction.OrderCode), Times.Once);
        }

        private PaymentController CreateController(int userId = 2, string role = "Customer")
        {
            var controller = new PaymentController(
                Mock.Of<IBookingService>(),
                _payOSPaymentService.Object,
                Mock.Of<IDigitalContractService>(),
                _context,
                Mock.Of<ILogger<PaymentController>>());

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, role)
            };

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"))
            };

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            return controller;
        }

        private async Task<PayOSTransaction> SeedHoldTransactionAsync()
        {
            var start = DateTime.UtcNow.AddDays(2);
            var end = start.AddDays(1);
            var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var quote = new CheckoutQuoteViewModel
            {
                UserID = 2,
                CarID = 1,
                CarName = "Camry 2024",
                StartDate = start,
                EndDate = end,
                HoldingFee = 108000,
                TotalPrice = 1000000
            };

            var hold = new BookingHold
            {
                UserID = 2,
                CarID = 1,
                StartDate = start,
                EndDate = end,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                Status = BookingHoldStatusConstants.Active,
                QuoteSnapshotJson = JsonSerializer.Serialize(quote, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            };
            _context.BookingHolds.Add(hold);
            await _context.SaveChangesAsync();

            var transaction = new PayOSTransaction
            {
                BookingHoldID = hold.BookingHoldID,
                UserID = 2,
                OrderCode = orderCode,
                Amount = quote.HoldingFee,
                Status = PaymentStatusConstants.Pending,
                CheckoutUrl = "https://pay.payos.vn/web/checkout/test",
                QrCode = "https://img.vietqr.io/image/970422-123456789-compact.png",
                Bin = "970422",
                AccountNumber = "123456789",
                AccountName = "CAR RENTAL",
                PayOSDescription = "GIU CHO TEST"
            };
            _context.PayOSTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            transaction.BookingHold = hold;
            hold.Car = await _context.Cars.FindAsync(1);
            return transaction;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
