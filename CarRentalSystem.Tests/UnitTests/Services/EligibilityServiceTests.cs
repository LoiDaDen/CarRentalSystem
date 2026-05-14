using System;
using System.Threading.Tasks;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class EligibilityServiceTests : IDisposable
    {
        private readonly CarRentalSystem.Data.ApplicationDbContext _context;
        private readonly EligibilityService _service;

        public EligibilityServiceTests()
        {
            _context = MockRepositoryHelper.CreateInMemoryDbContext();
            _service = new EligibilityService(_context);
        }

        [Fact]
        public async Task CheckRentalEligibilityAsync_ReturnsEkycCode_WhenProfileIsNotVerified()
        {
            var user = CreateBaseUser();
            user.IsVerified = false;
            user.VerificationStatus = "Pending";
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var result = await _service.CheckRentalEligibilityAsync(user.UserID);

            result.IsEligible.Should().BeFalse();
            result.FailureCode.Should().Be(EligibilityFailureCode.EkycNotVerified);
            result.Reason.Should().Contain("eKYC");
        }

        [Fact]
        public async Task CheckRentalEligibilityAsync_ReturnsMissingDocumentsCode_WhenIdentityHashesAreMissing()
        {
            var user = CreateBaseUser();
            user.IsVerified = true;
            user.VerificationStatus = "Verified";
            user.CitizenIdHash = null;
            user.DrivingLicenseHash = "license-hash";
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var result = await _service.CheckRentalEligibilityAsync(user.UserID);

            result.IsEligible.Should().BeFalse();
            result.FailureCode.Should().Be(EligibilityFailureCode.MissingIdentityDocuments);
        }

        [Fact]
        public async Task CheckRentalEligibilityAsync_ReturnsLockedCode_WhenAccountIsBlacklisted()
        {
            var user = CreateBaseUser();
            user.IsBlacklisted = true;
            user.BlacklistReason = "Vi phạm điều khoản thuê xe.";
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var result = await _service.CheckRentalEligibilityAsync(user.UserID);

            result.IsEligible.Should().BeFalse();
            result.FailureCode.Should().Be(EligibilityFailureCode.LockedOrBlacklisted);
            result.Reason.Should().Be("Vi phạm điều khoản thuê xe.");
        }

        [Fact]
        public async Task CheckRentalEligibilityAsync_ReturnsEligible_WhenAccountAndDocumentsAreValid()
        {
            var user = CreateBaseUser();
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var result = await _service.CheckRentalEligibilityAsync(user.UserID);

            result.IsEligible.Should().BeTrue();
            result.FailureCode.Should().Be(EligibilityFailureCode.None);
            result.Reason.Should().BeNull();
        }

        private static User CreateBaseUser() => new()
        {
            UserID = Random.Shared.Next(1000, 9999),
            Name = "Nguyễn Văn A",
            Email = $"customer-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            Role = "Customer",
            IsEmailVerified = true,
            IsVerified = true,
            VerificationStatus = "Verified",
            CitizenIdHash = "citizen-hash",
            DrivingLicenseHash = "license-hash"
        };

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
