using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    public class EnterpriseP0RegressionTests : IDisposable
    {
        private readonly ApplicationDbContext _context = MockRepositoryHelper.CreateSeededDbContext();

        [Fact]
        public async Task CancelBooking_Confirmed_PersistsCancelledAndCarAvailable()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Confirmed, paymentStatus: null);

            var result = await service.CancelAsync(booking.BookingID, 2, "Customer", "test cancel");

            result.IsSuccess.Should().BeTrue();
            var updated = await _context.Bookings.FindAsync(booking.BookingID);
            updated!.BookingStatusID.Should().Be(4);
            updated.DepositStatus.Should().Be(DepositStatusConstants.Unpaid);
            (await _context.Cars.FindAsync(booking.CarID))!.CarStatusID.Should().Be(1);
        }

        [Fact]
        public async Task CancelBooking_ConfirmedPaid_CreatesPendingRefund()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Confirmed, PaymentStatusConstants.Paid);

            var result = await service.CancelAsync(booking.BookingID, 2, "Customer", "test cancel");

            result.IsSuccess.Should().BeTrue();
            var updated = await _context.Bookings.FindAsync(booking.BookingID);
            updated!.DepositStatus.Should().Be(DepositStatusConstants.PendingRefund);
            _context.RefundRequests.Should().Contain(r => r.BookingID == booking.BookingID);
        }

        [Fact]
        public async Task CancelBooking_Active_MarksUnderReview()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Active, PaymentStatusConstants.Paid);

            var result = await service.CancelAsync(booking.BookingID, 2, "Customer", "active cancel");

            result.IsSuccess.Should().BeTrue();
            var updated = await _context.Bookings.FindAsync(booking.BookingID);
            updated!.DepositStatus.Should().Be(DepositStatusConstants.UnderReview);
            updated.ActualReturnDate.Should().NotBeNull();
        }

        [Fact]
        public async Task CartCheckout_SameCarGapUnderOneHour_IsRejected()
        {
            var user = (await _context.Users.FindAsync(2))!;
            var cart = new Cart { UserID = user.UserID };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            var start = DateTime.UtcNow.AddDays(3);
            _context.CartItems.AddRange(
                new CartItem { CartID = cart.CartID, CarID = 1, StartDate = start, EndDate = start.AddHours(2), EstimatedPrice = 100000 },
                new CartItem { CartID = cart.CartID, CarID = 1, StartDate = start.AddHours(2).AddMinutes(30), EndDate = start.AddHours(4), EstimatedPrice = 100000 });
            await _context.SaveChangesAsync();

            var controller = CreateCartController(user.UserID);
            var result = await controller.CheckoutAll();

            result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Index");
            controller.TempData["Error"]!.ToString().Should().Contain("cach nhau it nhat 1 tieng");
        }

        [Fact]
        public async Task SecureFile_RejectsUnknownFolder()
        {
            var controller = CreateSecureFileController();

            var result = await controller.GetSecureImage("Unknown", "2/id_front.jpg");

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task SecureFile_RejectsTraversalFileName()
        {
            var controller = CreateSecureFileController();

            var result = await controller.GetSecureImage("eKyc", "2/../secret.jpg");

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task TrackingIngest_MissingNonceWithSecret_IsRejected()
        {
            var controller = CreateTrackingController("tracking-secret");
            AddHeader(controller, "X-Device-Id", "device-1");
            AddHeader(controller, "X-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            AddHeader(controller, "X-Signature", "invalid");

            var result = await controller.ReceiveData(new VehicleTracking { CarID = 1, Latitude = 10, Longitude = 20 });

            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task TrackingIngest_ReusedNonce_IsRejected()
        {
            const string secret = "tracking-secret";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N");
            var signature = SignTracking(secret, "device-1", timestamp, nonce, 1, 10, 20);

            var controller = CreateTrackingController(secret);
            AddTrackingHeaders(controller, "device-1", timestamp, nonce, signature);
            var first = await controller.ReceiveData(new VehicleTracking { CarID = 1, Latitude = 10, Longitude = 20 });
            first.Should().BeOfType<OkObjectResult>();

            controller = CreateTrackingController(secret);
            AddTrackingHeaders(controller, "device-1", timestamp, nonce, signature);
            var second = await controller.ReceiveData(new VehicleTracking { CarID = 1, Latitude = 10, Longitude = 20 });
            second.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task EkycUpload_ExtensionMismatchMagicBytes_IsRejected()
        {
            var service = new FileUploadValidationService();
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-a-real-image"));
            var file = new FormFile(stream, 0, stream.Length, "file", "id_front.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var allowed = await service.IsAllowedImageAsync(file, 5 * 1024 * 1024);

            allowed.Should().BeFalse();
        }

        private BookingTransitionService CreateTransitionService()
        {
            return new BookingTransitionService(_context, new PaymentLedgerService(_context), CreateAuditTrailService());
        }

        private Booking SeedBooking(string statusName, string? paymentStatus)
        {
            var status = _context.BookingStatuses.First(s => s.Name == statusName);
            var booking = new Booking
            {
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(2),
                BookingStatusID = status.BookingStatusID,
                DepositStatus = DepositStatusConstants.Holding,
                DepositAmount = 5000000,
                TotalPrice = 1000000,
                PickupLocationID = 1,
                ReturnLocationID = 1
            };
            _context.Bookings.Add(booking);
            _context.SaveChanges();

            if (paymentStatus != null)
            {
                _context.Payments.Add(new Payment
                {
                    BookingID = booking.BookingID,
                    Amount = 500000,
                    PaymentMethod = "PayOS",
                    PaymentStatus = paymentStatus,
                    PaymentDate = DateTime.UtcNow
                });
                _context.SaveChanges();
            }

            return booking;
        }

        private CartController CreateCartController(int userId)
        {
            var eligibility = new Mock<IEligibilityService>();
            eligibility.Setup(s => s.CheckRentalEligibilityAsync(userId))
                .ReturnsAsync(new EligibilityResult(true, null));

            var controller = new CartController(
                _context,
                Mock.Of<IPiiEncryptionService>(),
                Mock.Of<IReservationService>(),
                Mock.Of<IBookingQuoteService>(),
                Mock.Of<IPayOSPaymentService>(),
                eligibility.Object);
            AttachHttpContext(controller, userId);
            return controller;
        }

        private SecureFileController CreateSecureFileController()
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
            var controller = new SecureFileController(env.Object, _context, CreateAuditTrailService());
            AttachHttpContext(controller, 2);
            return controller;
        }

        private IAuditTrailService CreateAuditTrailService()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Audit:HashChainEnabled"] = "true" })
                .Build();
            return new AuditTrailService(_context, new HttpContextAccessor(), configuration);
        }

        private TrackingController CreateTrackingController(string secret)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Tracking:IngestSecret"] = secret })
                .Build();
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns("Production");
            var tracking = new Mock<ITrackingService>();
            tracking.Setup(s => s.AddTrackingAsync(It.IsAny<VehicleTracking>())).Returns(Task.CompletedTask);
            tracking.Setup(s => s.CheckForAlertsAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>())).Returns(Task.CompletedTask);

            var controller = new TrackingController(
                tracking.Object,
                Mock.Of<ICarService>(),
                _context,
                configuration,
                env.Object,
                Mock.Of<ILogger<TrackingController>>());
            AttachHttpContext(controller, 1, "Admin");
            return controller;
        }

        private static void AttachHttpContext(Controller controller, int userId, string role = "Customer")
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userId.ToString()),
                    new(ClaimTypes.Role, role)
                }, "Cookies"))
            };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        }

        private static void AddTrackingHeaders(Controller controller, string deviceId, string timestamp, string nonce, string signature)
        {
            AddHeader(controller, "X-Device-Id", deviceId);
            AddHeader(controller, "X-Timestamp", timestamp);
            AddHeader(controller, "X-Nonce", nonce);
            AddHeader(controller, "X-Signature", signature);
        }

        private static void AddHeader(Controller controller, string key, string value)
        {
            controller.HttpContext.Request.Headers[key] = value;
        }

        private static string SignTracking(string secret, string deviceId, string timestamp, string nonce, int carId, double latitude, double longitude)
        {
            var payload = $"{deviceId}.{timestamp}.{nonce}.{carId}.{latitude:F6}.{longitude:F6}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
