using System.Security.Claims;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    public class EkycMobileSessionControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IEkycService> _ekycService = new();
        private readonly Mock<IPiiEncryptionService> _piiEncryption = new();
        private readonly Mock<IWebHostEnvironment> _environment = new();
        private readonly Mock<IReferralService> _referralService = new();
        private readonly Mock<IFileUploadValidationService> _fileValidation = new();
        private readonly Mock<ILegalComplianceService> _legalCompliance = new();
        private readonly Mock<IIdentityHashService> _identityHash = new();
        private readonly ICustomerDrivingLicenseSyncService _drivingLicenseSync;

        public EkycMobileSessionControllerTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
            _environment.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
            _piiEncryption.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(value => $"enc:{value}");
            _identityHash.Setup(s => s.HashForStorage(It.IsAny<string?>())).Returns<string?>(value => value == null ? null : $"hash:{value}");
            _identityHash.Setup(s => s.HashesForLookup(It.IsAny<string?>())).Returns<string?>(value => value == null ? Array.Empty<string>() : new[] { $"hash:{value}" });
            _fileValidation.Setup(s => s.IsAllowedImageAsync(It.IsAny<IFormFile>(), It.IsAny<long>())).ReturnsAsync(true);
            _legalCompliance
                .Setup(s => s.RecordConsentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<HttpContext>(), It.IsAny<string?>()))
                .ReturnsAsync((int userId, string documentCode, string purpose, bool granted, string source, HttpContext httpContext, string? evidence) =>
                    new ConsentRecord { UserID = userId, PurposeCode = purpose, IsGranted = granted, Source = source });
            _drivingLicenseSync = new CustomerDrivingLicenseSyncService(_context, _piiEncryption.Object, _identityHash.Object);
            _referralService.Setup(s => s.EnsureReferralCodeAsync(It.IsAny<int>())).ReturnsAsync("ABC123");
            _ekycService.Setup(s => s.ExtractIdCardAsync(It.IsAny<IFormFile>(), It.IsAny<IFormFile>()))
                .ReturnsAsync(new EkycIdResult
                {
                    IsSuccess = true,
                    CitizenId = "079200000001",
                    FullName = "Customer Test",
                    DateOfBirth = "01/01/1990",
                    Gender = "Nam",
                    Address = "TP HCM",
                    Message = "OK"
                });
            _ekycService.Setup(s => s.ExtractDrivingLicenseAsync(It.IsAny<IFormFile>()))
                .ReturnsAsync(new EkycLicenseResult
                {
                    IsSuccess = true,
                    LicenseNumber = "790000000001",
                    Class = "B2",
                    Message = "OK"
                });
            _ekycService.Setup(s => s.CompareFaceAsync(It.IsAny<IFormFile>(), It.IsAny<IFormFile>()))
                .ReturnsAsync(new EkycFaceResult
                {
                    IsSuccess = true,
                    IsMatch = true,
                    SimilarityPercent = 99,
                    Message = "OK"
                });
        }

        [Fact]
        public async Task CreateMobileSession_HashesTokenAndExpiresInFifteenMinutes()
        {
            var controller = CreateController(userId: 2);

            var result = await controller.CreateMobileSession("/Cart") as JsonResult;

            result.Should().NotBeNull();
            var session = _context.EkycMobileSessions.Single();
            session.UserID.Should().Be(2);
            session.TokenHash.Should().HaveLength(64);
            session.TokenHash.Should().NotContain("Ekyc/Mobile");
            session.ReturnUrl.Should().Be("/Cart");
            session.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task MobileSessionStatus_WhenSessionBelongsToAnotherUser_ReturnsNotFound()
        {
            _context.EkycMobileSessions.Add(new EkycMobileSession
            {
                UserID = 2,
                TokenHash = new string('a', 64),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            });
            await _context.SaveChangesAsync();

            var controller = CreateController(userId: 3);
            var result = await controller.MobileSessionStatus(1);

            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task MobileSessionStatus_WhenExpired_ReportsExpired()
        {
            _context.EkycMobileSessions.Add(new EkycMobileSession
            {
                UserID = 2,
                TokenHash = new string('b', 64),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });
            await _context.SaveChangesAsync();

            var controller = CreateController(userId: 2);
            await controller.MobileSessionStatus(1);

            _context.EkycMobileSessions.Single().Status.Should().Be(EkycMobileSessionStatuses.Expired);
        }

        [Fact]
        public async Task MobileSubmit_WithValidFiles_VerifiesUserAndSession()
        {
            var controller = CreateController(userId: 2);
            var createResult = await controller.CreateMobileSession("/Cart") as JsonResult;
            var token = ExtractTokenFromJson(createResult!);

            var mobileController = CreateController(userId: null);
            var result = await mobileController.Mobile(
                token,
                Image("id-front.jpg"),
                Image("id-back.jpg"),
                Image("license-front.jpg"),
                Image("license-back.jpg"),
                Image("face.jpg"),
                EkycPrivacyConsent: true,
                EkycBiometricConsent: true);

            result.Should().BeOfType<ViewResult>();
            _context.EkycMobileSessions.Single().Status.Should().Be(EkycMobileSessionStatuses.Verified);
            var user = await _context.Users.FindAsync(2);
            user!.VerificationStatus.Should().Be("Verified");
            user.IsVerified.Should().BeTrue();
            _context.CustomerDrivingLicenses.Should().ContainSingle(license =>
                license.UserID == 2 &&
                license.IsActive &&
                license.IsApproved &&
                license.VerificationStatus == "Verified");
        }

        private EkycController CreateController(int? userId)
        {
            var controller = new EkycController(
                _context,
                _ekycService.Object,
                _piiEncryption.Object,
                _environment.Object,
                _referralService.Object,
                _fileValidation.Object,
                _legalCompliance.Object,
                _identityHash.Object,
                _drivingLicenseSync);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost");
            if (userId.HasValue)
            {
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                }, "Cookies"));
            }

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            controller.Url = CreateUrlHelper();
            return controller;
        }

        private static IUrlHelper CreateUrlHelper()
        {
            var helper = new Mock<IUrlHelper>();
            helper.Setup(h => h.Action(It.IsAny<UrlActionContext>()))
                .Returns((UrlActionContext action) =>
                {
                    var token = action.Values?.GetType().GetProperty("token")?.GetValue(action.Values)?.ToString();
                    return $"https://localhost/Ekyc/Mobile/{token}";
                });
            helper.Setup(h => h.IsLocalUrl(It.IsAny<string>()))
                .Returns<string>(url => !string.IsNullOrWhiteSpace(url) && url.StartsWith("/") && !url.StartsWith("//"));
            return helper.Object;
        }

        private static string ExtractTokenFromJson(JsonResult result)
        {
            var value = result.Value!;
            var mobileUrl = value.GetType().GetProperty("mobileUrl")!.GetValue(value)!.ToString()!;
            return mobileUrl.Split('/').Last();
        }

        private static IFormFile Image(string fileName)
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9 };
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
