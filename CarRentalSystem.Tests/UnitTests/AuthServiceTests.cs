using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    /// <summary>
    /// Unit tests cho AuthService — Đăng nhập, Đăng ký, CCCD
    /// </summary>
    public class AuthServiceTests
    {
        private readonly List<User> _users;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            // Setup test data
            _users = new List<User>
            {
                new User
                {
                    UserID = 1,
                    Name = "Nguyen Van A",
                    Email = "nguyenvana@email.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                    Role = "Customer",
                    IsEmailVerified = true,
                    IsVerified = true,
                    CitizenId = "012345678901",
                    Phone = "0901234567"
                },
                new User
                {
                    UserID = 2,
                    Name = "Tran Thi B",
                    Email = "tranthib@email.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password456!"),
                    Role = "Customer",
                    IsEmailVerified = false,
                    IsVerified = false,
                    CitizenId = "098765432101",
                    Phone = "0907654321"
                },
            };

            var userRepo = MockRepositoryHelper.Create(_users);
            var mockPii = new Mock<IPiiEncryptionService>();
            _authService = new AuthService(userRepo.Object, mockPii.Object);
        }

        // =============================================
        // AUTHENTICATE TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Authenticate_ValidEmailAndPassword_ReturnsUser()
        {
            // Act
            var result = await _authService.AuthenticateAsync("nguyenvana@email.com", "Password123!");

            // Assert
            result.Should().NotBeNull();
            result!.Email.Should().Be("nguyenvana@email.com");
            result.Name.Should().Be("Nguyen Van A");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Authenticate_WrongPassword_ReturnsNull()
        {
            // Act
            var result = await _authService.AuthenticateAsync("nguyenvana@email.com", "WrongPassword!");

            // Assert
            result.Should().BeNull("vì mật khẩu không khớp");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Authenticate_NonExistentEmail_ReturnsNull()
        {
            // Act
            var result = await _authService.AuthenticateAsync("khongtontai@email.com", "AnyPassword");

            // Assert
            result.Should().BeNull("vì email không tồn tại trong hệ thống");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Authenticate_ByUsername_ReturnsUser()
        {
            // Act - Hệ thống hỗ trợ đăng nhập bằng cả Username (Name)
            var result = await _authService.AuthenticateAsync("Nguyen Van A", "Password123!");

            // Assert
            result.Should().NotBeNull("vì hệ thống cho phép đăng nhập bằng tên người dùng");
            result!.Email.Should().Be("nguyenvana@email.com");
        }

        // =============================================
        // REGISTER TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Register_NewUser_HashesPassword()
        {
            // Arrange
            var newUser = new User
            {
                UserID = 0,
                Name = "Le Van C",
                Email = "levanc@email.com",
                PasswordHash = "PlainTextPassword!",
                Phone = "0912345678"
            };
            var plainPassword = newUser.PasswordHash;

            // Act
            var result = await _authService.RegisterAsync(newUser);

            // Assert
            result.Should().NotBeNull();
            result.PasswordHash.Should().NotBe(plainPassword, "vì mật khẩu phải được hash trước khi lưu");
            BCrypt.Net.BCrypt.Verify(plainPassword, result.PasswordHash).Should().BeTrue("vì hash phải xác minh được với mật khẩu gốc");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task Register_NewUser_AddsToRepository()
        {
            // Arrange
            var initialCount = _users.Count;
            var newUser = new User
            {
                UserID = 0,
                Name = "Pham Thi D",
                Email = "phamthid@email.com",
                PasswordHash = "NewPassword123!",
                Phone = "0923456789"
            };

            // Act
            await _authService.RegisterAsync(newUser);

            // Assert
            _users.Count.Should().Be(initialCount + 1, "vì user mới phải được thêm vào repository");
        }

        // =============================================
        // CITIZEN ID TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task IsCitizenIdTaken_DuplicateCCCDByCifferentUser_ReturnsTrue()
        {
            // Act - CCCD "012345678901" đã thuộc về UserID=1, check với UserID=99 (khác người)
            var result = await _authService.IsCitizenIdTakenAsync("012345678901", excludeUserId: 99);

            // Assert
            result.Should().BeTrue("vì CCCD này đã tồn tại cho một người dùng khác");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task IsCitizenIdTaken_SameUserCCCD_ReturnsFalse()
        {
            // Act - CCCD "012345678901" thuộc về UserID=1, exclude chính UserID=1
            var result = await _authService.IsCitizenIdTakenAsync("012345678901", excludeUserId: 1);

            // Assert
            result.Should().BeFalse("vì đây là CCCD của chính người dùng đang cập nhật");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task IsCitizenIdTaken_EmptyCCCD_ReturnsFalse()
        {
            // Act
            var result = await _authService.IsCitizenIdTakenAsync("", excludeUserId: 1);

            // Assert
            result.Should().BeFalse("vì CCCD rỗng không được tính là trùng");
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task GetUserByEmail_ExistingEmail_ReturnsCorrectUser()
        {
            // Act
            var result = await _authService.GetUserByEmailAsync("tranthib@email.com");

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Tran Thi B");
            result.IsEmailVerified.Should().BeFalse();
        }

        [Fact]
        [Trait("Module", "Auth")]
        [Trait("Type", "Unit")]
        public async Task UpdateUser_ChangesArePersisted()
        {
            // Arrange
            var user = _users.First();
            var originalName = user.Name;

            // Act
            user.Name = "Ten Moi Cap Nhat";
            await _authService.UpdateUserAsync(user);

            // Assert
            _users.First().Name.Should().Be("Ten Moi Cap Nhat", "vì tên đã được cập nhật");
        }
    }
}
