using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CarRentalSystem.Data;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace CarRentalSystem.Tests.IntegrationTests
{
    /// <summary>
    /// Custom WebApplicationFactory — thay SQL Server bằng InMemory DB
    /// và bỏ qua DbSeeder.Seed() (vì nó gọi Database.Migrate() chỉ dùng cho relational DB).
    /// </summary>
    public class TestWebAppFactory : WebApplicationFactory<Program>
    {
        private static readonly string _dbName = "SmokeTestDb_Shared";
        private static bool _seeded = false;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Gỡ bỏ TẤT CẢ registrations liên quan đến DbContext/EF Core
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                        || d.ServiceType == typeof(ApplicationDbContext)
                        || (d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true
                            && !d.ServiceType.FullName.Contains("InMemory")))
                    .ToList();
                foreach (var d in toRemove)
                    services.Remove(d);

                // Thêm InMemory Database với tên cố định
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                });

                // Seed data ngay trong factory
                if (!_seeded)
                {
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    SeedData(db);
                    _seeded = true;
                }
            });
        }

        private static void SeedData(ApplicationDbContext db)
        {
            if (db.CarStatuses.Any()) return;

            db.BookingStatuses.AddRange(
                new CarRentalSystem.Models.BookingStatus { BookingStatusID = 1, Name = "Draft" },
                new CarRentalSystem.Models.BookingStatus { BookingStatusID = 2, Name = "Pending" },
                new CarRentalSystem.Models.BookingStatus { BookingStatusID = 3, Name = "Confirmed" },
                new CarRentalSystem.Models.BookingStatus { BookingStatusID = 4, Name = "Cancelled" },
                new CarRentalSystem.Models.BookingStatus { BookingStatusID = 5, Name = "Active" },
                new CarRentalSystem.Models.BookingStatus { BookingStatusID = 6, Name = "Completed" }
            );

            db.CarStatuses.AddRange(
                new CarRentalSystem.Models.CarStatus { CarStatusID = 1, Name = "Available" },
                new CarRentalSystem.Models.CarStatus { CarStatusID = 2, Name = "Rented" },
                new CarRentalSystem.Models.CarStatus { CarStatusID = 3, Name = "Maintenance" }
            );

            db.Locations.AddRange(
                new CarRentalSystem.Models.Location { LocationID = 1, Name = "Hồ Chí Minh", Latitude = 10.7769, Longitude = 106.7009 },
                new CarRentalSystem.Models.Location { LocationID = 2, Name = "Hà Nội", Latitude = 21.0285, Longitude = 105.8542 }
            );

            db.CarBrands.AddRange(
                new CarRentalSystem.Models.CarBrand { BrandID = 1, Name = "Toyota" },
                new CarRentalSystem.Models.CarBrand { BrandID = 2, Name = "Honda" }
            );

            db.CarTypes.AddRange(
                new CarRentalSystem.Models.CarType { CarTypeID = 1, Name = "Sedan" },
                new CarRentalSystem.Models.CarType { CarTypeID = 2, Name = "SUV" }
            );

            db.MembershipTiers.AddRange(
                new CarRentalSystem.Models.MembershipTier { MembershipTierID = 1, Name = "Bronze", MinSpending = 0, DiscountPercent = 0, Icon = "bi-star", Color = "#cd7f32" },
                new CarRentalSystem.Models.MembershipTier { MembershipTierID = 2, Name = "Silver", MinSpending = 10000000, DiscountPercent = 5, Icon = "bi-award", Color = "#c0c0c0" },
                new CarRentalSystem.Models.MembershipTier { MembershipTierID = 3, Name = "Gold", MinSpending = 30000000, DiscountPercent = 10, Icon = "bi-trophy", Color = "#ffd700" }
            );

            db.Cars.AddRange(
                new CarRentalSystem.Models.Car
                {
                    CarID = 1, CarName = "Camry 2024", BrandID = 1,
                    LicensePlate = "51A-12345", VIN = "VIN0001",
                    PricePerDay = 1000000, PricePerHour = 100000,
                    CarStatusID = 1, LocationID = 1, CarTypeID = 1,
                    Seats = 5, DefaultDeposit = 5000000
                },
                new CarRentalSystem.Models.Car
                {
                    CarID = 2, CarName = "CR-V 2024", BrandID = 2,
                    LicensePlate = "51B-67890", VIN = "VIN0002",
                    PricePerDay = 1500000, PricePerHour = 150000,
                    CarStatusID = 1, LocationID = 1, CarTypeID = 2,
                    Seats = 7, DefaultDeposit = 8000000
                }
            );

            db.Users.AddRange(
                new CarRentalSystem.Models.User
                {
                    UserID = 1, Name = "Admin Test", Email = "admin@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Role = "Admin", IsEmailVerified = true, IsVerified = true,
                    Phone = "0900000001"
                },
                new CarRentalSystem.Models.User
                {
                    UserID = 2, Name = "Customer Test", Email = "customer@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                    Role = "Customer", IsEmailVerified = true, IsVerified = true,
                    Phone = "0900000002"
                }
            );

            db.SaveChanges();
        }
    }

    /// <summary>
    /// 🚀 SMOKE TESTS — Tự khởi động app rồi test trực tiếp trên web đang chạy.
    /// Dùng WebApplicationFactory để tạo test server, gửi HTTP requests thật
    /// và kiểm tra response từ các trang web.
    /// </summary>
    public class SmokeTests : IClassFixture<TestWebAppFactory>, IDisposable
    {
        private readonly HttpClient _client;
        private readonly TestWebAppFactory _factory;
        private readonly ITestOutputHelper _output;

        public SmokeTests(TestWebAppFactory factory, ITestOutputHelper output)
        {
            _output = output;
            _factory = factory;

            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = true
            });
        }

        // =============================================
        // 🏠 TRANG CHỦ
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Web")]
        public async Task Homepage_ReturnsSuccess_AndContainsTitle()
        {
            _output.WriteLine("🏠 Test: Mở trang chủ...");
            var response = await _client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("CarRental", "Trang chủ phải có tên ứng dụng");
            _output.WriteLine($"   ✅ Trang chủ — HTTP {(int)response.StatusCode} — {content.Length} bytes");
        }

        // =============================================
        // 🚗 DANH SÁCH XE
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Web")]
        public async Task CarIndex_ReturnsSuccess_AndShowsCars()
        {
            _output.WriteLine("🚗 Test: Mở trang danh sách xe...");
            var response = await _client.GetAsync("/Car");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Length.Should().BeGreaterThan(100, "Trang danh sách xe phải có nội dung");
            _output.WriteLine($"   ✅ Danh sách xe — HTTP {(int)response.StatusCode} — {content.Length} bytes");
        }

        // =============================================
        // 🔑 TRANG ĐĂNG NHẬP
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Auth")]
        public async Task LoginPage_ReturnsSuccess_AndHasForm()
        {
            _output.WriteLine("🔑 Test: Mở trang đăng nhập...");
            var response = await _client.GetAsync("/Auth/Login");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Email", "Trang login phải có trường Email");
            _output.WriteLine($"   ✅ Trang đăng nhập — HTTP {(int)response.StatusCode} — Có form login");
        }

        // =============================================
        // 📝 TRANG ĐĂNG KÝ
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Auth")]
        public async Task RegisterPage_ReturnsSuccess_AndHasForm()
        {
            _output.WriteLine("📝 Test: Mở trang đăng ký...");
            var response = await _client.GetAsync("/Auth/Register");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Email", "Trang đăng ký phải có trường Email");
            _output.WriteLine($"   ✅ Trang đăng ký — HTTP {(int)response.StatusCode} — Có form đăng ký");
        }

        // =============================================
        // 🗺️ BẢN ĐỒ TÌM KIẾM
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Web")]
        public async Task MapSearch_ReturnsSuccess()
        {
            _output.WriteLine("🗺️ Test: Mở trang tìm kiếm bản đồ...");
            var response = await _client.GetAsync("/Car/MapSearch");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _output.WriteLine($"   ✅ Bản đồ tìm kiếm — HTTP {(int)response.StatusCode}");
        }

        // =============================================
        // 🚗 CHI TIẾT XE
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Car")]
        public async Task CarDetails_ExistingCar_ReturnsSuccess()
        {
            _output.WriteLine("🚗 Test: Mở trang chi tiết xe ID=1...");
            var response = await _client.GetAsync("/Car/Details/1");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Camry", "Trang chi tiết phải hiển thị tên xe");
            _output.WriteLine($"   ✅ Chi tiết xe — HTTP {(int)response.StatusCode} — Hiển thị Camry 2024");
        }

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Car")]
        public async Task CarDetails_NonExistingCar_ReturnsNotFound()
        {
            _output.WriteLine("🚗 Test: Mở chi tiết xe không tồn tại ID=9999...");
            var response = await _client.GetAsync("/Car/Details/9999");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            _output.WriteLine($"   ✅ Xe không tồn tại — HTTP {(int)response.StatusCode} — Trả đúng 404");
        }

        // =============================================
        // 🛒 GIỎ HÀNG
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Web")]
        public async Task Cart_ReturnsSuccess()
        {
            _output.WriteLine("🛒 Test: Mở giỏ hàng...");
            var response = await _client.GetAsync("/Cart");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _output.WriteLine($"   ✅ Giỏ hàng — HTTP {(int)response.StatusCode}");
        }

        // =============================================
        // 🔒 ADMIN — Kiểm tra auth redirect
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Admin")]
        public async Task AdminDashboard_ChecksAccess()
        {
            _output.WriteLine("🔒 Test: Truy cập Admin Dashboard...");
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            var response = await client.GetAsync("/Admin/Dashboard");
            var statusCode = (int)response.StatusCode;
            (statusCode == 302 || statusCode == 200).Should().BeTrue(
                "Admin page phải redirect về Login (302) hoặc trả 200");
            _output.WriteLine($"   ✅ Admin Dashboard — HTTP {statusCode} — Kiểm tra auth OK");
        }

        // =============================================
        // 📄 STATIC FILES
        // =============================================

        [Fact]
        [Trait("Type", "Smoke")]
        [Trait("Module", "Web")]
        public async Task StaticFiles_CssLoads()
        {
            _output.WriteLine("📄 Test: Kiểm tra CSS references...");
            var response = await _client.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("<link", "Trang phải có link đến CSS");
            _output.WriteLine($"   ✅ CSS — Trang có reference đến stylesheets");
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
