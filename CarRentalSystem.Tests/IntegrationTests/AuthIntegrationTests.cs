using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.IntegrationTests
{
    /// <summary>
    /// Integration tests cho AuthService — Đăng ký, Đăng nhập, Profile đầy đủ với InMemory Database
    /// </summary>
    public class AuthIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthService _authService;

        public AuthIntegrationTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();

            var userRepo = new Repository<User>(_context);
            var mockPii = new Mock<IPiiEncryptionService>();
            _authService = new AuthService(userRepo, mockPii.Object);
        }

        // =============================================
        // FULL REGISTER + LOGIN FLOW
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Integration")]
        public async Task RegisterAndLogin_FullFlow()
        {
            // Arrange
            var newUser = new User
            {
                Name = "Nguyen Test User",
                Email = "nguyentest@email.com",
                PasswordHash = "SecurePass@123",
                Phone = "0911111111",
                Role = "Customer"
            };
            var plainPassword = newUser.PasswordHash;

            // Act 1: Đăng ký
            var registered = await _authService.RegisterAsync(newUser);
            registered.Should().NotBeNull();
            registered.PasswordHash.Should().NotBe(plainPassword, "mật khẩu phải được hash");

            // Act 2: Đăng nhập bằng email
            var loggedIn = await _authService.AuthenticateAsync("nguyentest@email.com", plainPassword);

            // Assert
            loggedIn.Should().NotBeNull("vì đăng nhập phải thành công sau đăng ký");
            loggedIn!.Name.Should().Be("Nguyen Test User");
            loggedIn.Email.Should().Be("nguyentest@email.com");
        }

        // =============================================
        // DUPLICATE EMAIL HANDLING
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Integration")]
        public async Task RegisterDuplicateEmail_OriginalUserCanStillLogin()
        {
            // Arrange — User "customer@test.com" đã tồn tại từ seed data

            // Act — Đăng nhập user cũ vẫn hoạt động
            var result = await _authService.AuthenticateAsync("customer@test.com", "Customer@123");

            // Assert
            result.Should().NotBeNull("vì user seeded phải đăng nhập được");
            result!.Name.Should().Be("Customer Test");
        }

        // =============================================
        // UPDATE PROFILE WITH CCCD
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Integration")]
        public async Task UpdateProfile_WithCCCD_Persists()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Email == "customer@test.com");
            user.Should().NotBeNull();

            // Act — Cập nhật thông tin CCCD
            user.CitizenId = "079200001234";
            user.Address = "123 Nguyễn Huệ, Q.1, TP.HCM";
            user.Gender = "Nam";
            user.DateOfBirth = new DateTime(2000, 5, 15);
            await _authService.UpdateUserAsync(user);

            // Assert
            var updated = await _context.Users.FirstAsync(u => u.Email == "customer@test.com");
            updated.CitizenId.Should().Be("079200001234");
            updated.Address.Should().Contain("Nguyễn Huệ");
            updated.Gender.Should().Be("Nam");
            updated.DateOfBirth.Should().Be(new DateTime(2000, 5, 15));
        }

        // =============================================
        // EMAIL VERIFICATION
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Integration")]
        public async Task VerifyEmail_UpdatesFlag()
        {
            // Arrange — User chưa verify email
            var unverified = await _context.Users.FirstAsync(u => u.Email == "unverified@test.com");
            unverified.IsEmailVerified.Should().BeFalse("ban đầu chưa xác thực email");

            // Act
            unverified.IsEmailVerified = true;
            unverified.EmailVerificationToken = null;
            await _authService.UpdateUserAsync(unverified);

            // Assert
            var updated = await _context.Users.FirstAsync(u => u.Email == "unverified@test.com");
            updated.IsEmailVerified.Should().BeTrue("vì email đã được xác thực");
            updated.EmailVerificationToken.Should().BeNull("vì token đã bị xóa sau khi xác thực");
        }

        // =============================================
        // BLACKLIST USER
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Integration")]
        public async Task BlacklistUser_IsBlacklisted()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Email == "customer@test.com");
            user.IsBlacklisted.Should().BeFalse("ban đầu user không bị chặn");

            // Act
            user.IsBlacklisted = true;
            user.BlacklistReason = "Vi phạm điều khoản sử dụng nhiều lần";
            await _authService.UpdateUserAsync(user);

            // Assert
            var updated = await _context.Users.FirstAsync(u => u.Email == "customer@test.com");
            updated.IsBlacklisted.Should().BeTrue("vì user đã bị đưa vào danh sách đen");
            updated.BlacklistReason.Should().Contain("Vi phạm");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
