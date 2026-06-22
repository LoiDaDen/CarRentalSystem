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
using Microsoft.EntityFrameworkCore;
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
            var updated = await _context.Bookings
                .Include(b => b.BookingStatus)
                .FirstAsync(b => b.BookingID == booking.BookingID);
            updated.BookingStatus!.Name.Should().Be(BookingStatusConstants.Cancelled);
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
            _context.RefundRequests.Should().Contain(r =>
                r.BookingID == booking.BookingID &&
                r.Amount == booking.DepositAmount &&
                r.Status == RefundRequestStatusConstants.PendingReview);
        }

        [Fact]
        public async Task CancelBooking_Completed_IsRejected()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Completed, PaymentStatusConstants.Paid);

            var result = await service.CancelAsync(booking.BookingID, 2, "Customer", "too late");

            result.IsSuccess.Should().BeFalse();
            var updated = await _context.Bookings.Include(b => b.BookingStatus).FirstAsync(b => b.BookingID == booking.BookingID);
            updated.BookingStatus!.Name.Should().Be(BookingStatusConstants.Completed);
            _context.RefundRequests.Should().NotContain(r => r.BookingID == booking.BookingID);
        }

        [Fact]
        public async Task Transition_UnpaidToPending_Succeeds()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Unpaid, paymentStatus: null);

            var result = await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Pending, 1, "System");

            result.IsSuccess.Should().BeTrue();
            (await FindStatusNameAsync(booking.BookingID)).Should().Be(BookingStatusConstants.Pending);
        }

        [Fact]
        public async Task Transition_PendingToConfirmed_RequiresPaidPayment()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Pending, paymentStatus: null);

            var result = await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Confirmed, 1, "System");

            result.IsSuccess.Should().BeFalse();
            (await FindStatusNameAsync(booking.BookingID)).Should().Be(BookingStatusConstants.Pending);
        }

        [Fact]
        public async Task Transition_PendingToConfirmed_WithPaidPayment_HoldsDeposit()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Pending, PaymentStatusConstants.Paid);

            var result = await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Confirmed, 1, "System");

            result.IsSuccess.Should().BeTrue();
            var updated = await _context.Bookings.Include(b => b.BookingStatus).FirstAsync(b => b.BookingID == booking.BookingID);
            updated.BookingStatus!.Name.Should().Be(BookingStatusConstants.Confirmed);
            updated.DepositStatus.Should().Be(DepositStatusConstants.Holding);
        }

        [Fact]
        public async Task Transition_ConfirmedToActive_RequiresCheckIn()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Confirmed, PaymentStatusConstants.Paid);

            var result = await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Active, 1, "System");

            result.IsSuccess.Should().BeFalse();
            (await FindStatusNameAsync(booking.BookingID)).Should().Be(BookingStatusConstants.Confirmed);
        }

        [Fact]
        public async Task Transition_ConfirmedToActive_WithCheckIn_RentsCar()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Confirmed, PaymentStatusConstants.Paid);
            AddHandover(booking.BookingID, "CheckIn");

            var result = await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Active, 1, "System");

            result.IsSuccess.Should().BeTrue();
            (await FindStatusNameAsync(booking.BookingID)).Should().Be(BookingStatusConstants.Active);
            (await _context.Cars.FindAsync(booking.CarID))!.CarStatusID.Should().Be(2);
        }

        [Fact]
        public async Task Transition_ActiveToCompleted_RequiresCheckOut()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Active, PaymentStatusConstants.Paid);

            var result = await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Completed, 1, "System");

            result.IsSuccess.Should().BeFalse();
            (await FindStatusNameAsync(booking.BookingID)).Should().Be(BookingStatusConstants.Active);
        }

        [Fact]
        public async Task Transition_ActiveToCompleted_WithCheckOut_MarksDepositRefunded()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Active, PaymentStatusConstants.Paid);
            AddHandover(booking.BookingID, "CheckOut");

            var result = await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Completed, 1, "System");

            result.IsSuccess.Should().BeTrue();
            var updated = await _context.Bookings.Include(b => b.BookingStatus).FirstAsync(b => b.BookingID == booking.BookingID);
            updated.BookingStatus!.Name.Should().Be(BookingStatusConstants.Completed);
            updated.DepositStatus.Should().Be(DepositStatusConstants.Refunded);
            updated.ActualReturnDate.Should().NotBeNull();
            (await _context.Cars.FindAsync(booking.CarID))!.CarStatusID.Should().Be(1);
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "LifecycleEvidence")]
        public async Task FullLifecycle_PersistsPaymentContractHandoversSettlementAndAvailableCar()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Confirmed, PaymentStatusConstants.Paid);
            var payment = await _context.Payments.FirstAsync(p => p.BookingID == booking.BookingID);

            _context.PaymentLedgerEntries.Add(new PaymentLedgerEntry
            {
                BookingID = booking.BookingID,
                PaymentID = payment.PaymentID,
                Provider = "MockPayOS",
                OrderCode = 990000 + booking.BookingID,
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = payment.Amount,
                Status = PaymentStatusConstants.Paid,
                IdempotencyKey = $"lifecycle-{booking.BookingID}-deposit"
            });
            _context.DigitalContracts.Add(new DigitalContract
            {
                BookingID = booking.BookingID,
                ContractNo = $"LIFECYCLE-{booking.BookingID}",
                CustomerName = "Lifecycle Customer",
                CustomerIdentityCard = "MASKED",
                CustomerPhone = "MASKED",
                CustomerAddress = "MASKED",
                CarLicensePlate = "TEST-LIFECYCLE",
                CarModel = "Lifecycle Car",
                RentalStartDate = booking.StartDate,
                RentalEndDate = booking.EndDate,
                TotalContractValue = booking.TotalPrice,
                DepositAmount = booking.DepositAmount,
                TermsAndConditions = "Lifecycle test terms",
                IsSignedByCustomer = true,
                CustomerSignDate = DateTime.UtcNow
            });
            AddHandover(booking.BookingID, "CheckIn");
            await _context.SaveChangesAsync();

            (await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Active, 1, "Staff")).IsSuccess.Should().BeTrue();

            AddHandover(booking.BookingID, "CheckOut");
            _context.SettlementReviews.Add(new SettlementReview
            {
                BookingID = booking.BookingID,
                ReviewType = SettlementReviewTypeConstants.Cleaning,
                Amount = 0,
                Reason = "Lifecycle return accepted",
                RequestedByUserID = 1
            });
            await _context.SaveChangesAsync();

            (await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Completed, 1, "Staff")).IsSuccess.Should().BeTrue();

            var completed = await _context.Bookings.Include(b => b.BookingStatus).FirstAsync(b => b.BookingID == booking.BookingID);
            completed.BookingStatus!.Name.Should().Be(BookingStatusConstants.Completed);
            completed.DepositStatus.Should().Be(DepositStatusConstants.Refunded);
            (await _context.Cars.FindAsync(booking.CarID))!.CarStatusID.Should().Be(1);
            _context.PaymentLedgerEntries.Should().ContainSingle(e => e.BookingID == booking.BookingID && e.Status == PaymentStatusConstants.Paid);
            _context.DigitalContracts.Should().ContainSingle(c => c.BookingID == booking.BookingID && c.IsSignedByCustomer);
            _context.HandoverRecords.Count(h => h.BookingID == booking.BookingID).Should().Be(2);
            _context.SettlementReviews.Should().ContainSingle(s => s.BookingID == booking.BookingID);
        }

        [Fact]
        public async Task Transition_PendingToExpired_Succeeds()
        {
            var service = CreateTransitionService();
            var booking = SeedBooking(BookingStatusConstants.Pending, paymentStatus: null);

            var result = await service.TransitionAsync(booking.BookingID, BookingStatusConstants.Expired, 1, "System");

            result.IsSuccess.Should().BeTrue();
            (await FindStatusNameAsync(booking.BookingID)).Should().Be(BookingStatusConstants.Expired);
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
            var result = await controller.CheckoutAll(true, true);

            result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Index");
            controller.TempData["Error"]!.ToString().Should().Contain("cách nhau ít nhất 1 tiếng");
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

        private void AddHandover(int bookingId, string type)
        {
            _context.HandoverRecords.Add(new HandoverRecord
            {
                BookingID = bookingId,
                Type = type,
                Odo = 1000,
                FuelLevel = "F"
            });
            _context.SaveChanges();
        }

        private async Task<string?> FindStatusNameAsync(int bookingId)
        {
            return (await _context.Bookings
                .Include(b => b.BookingStatus)
                .FirstAsync(b => b.BookingID == bookingId))
                .BookingStatus?.Name;
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
                eligibility.Object,
                Mock.Of<ILegalComplianceService>());
            AttachHttpContext(controller, userId);
            return controller;
        }

        private SecureFileController CreateSecureFileController()
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
            var controller = new SecureFileController(env.Object, _context, CreateAuditTrailService(), Mock.Of<IAreaService>(), Mock.Of<IPiiAccessService>());
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
