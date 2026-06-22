using BCrypt.Net;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarRentalSystem.Data
{
    public static class DbSeeder
    {
        /// <summary>
        /// Seed dữ liệu mẫu — CHỈ chạy khi DB trống (lần đầu tiên).
        /// Migration đã được chạy trước bởi Program.cs.
        /// Mỗi block có guard .Any() riêng → idempotent, không trùng dữ liệu.
        /// </summary>
        public static void Seed(ApplicationDbContext context)
        {
            // ═══ 1. USERS ═══
            if (!context.Users.Any())
            {
                var users = new List<User>
                {
                    new User
                    {
                        Name = "Admin",
                        Email = "admin@admin.com",
                        Phone = "0900000000",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                        Role = "Admin",
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Name = "John Customer",
                        Email = "john@example.com",
                        Phone = "0901111111",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                        Role = "Customer",
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Name = "Mary Jane",
                        Email = "mary@example.com",
                        Phone = "0902222222",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                        Role = "Customer",
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Name = "Peter Parker",
                        Email = "peter@example.com",
                        Phone = "0903333333",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                        Role = "Customer",
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };
                context.Users.AddRange(users);
                context.SaveChanges();
            }

            // ═══ 2. LOOKUPS + CARS ═══
            var requiredBookingStatuses = new[]
            {
                BookingStatusConstants.Draft,
                BookingStatusConstants.Unpaid,
                BookingStatusConstants.Pending,
                BookingStatusConstants.Confirmed,
                BookingStatusConstants.Active,
                BookingStatusConstants.Completed,
                BookingStatusConstants.Cancelled,
                BookingStatusConstants.Rejected,
                BookingStatusConstants.Expired,
                BookingStatusConstants.NeedReview
            };

            foreach (var statusName in requiredBookingStatuses)
            {
                if (!context.BookingStatuses.Any(s => s.Name == statusName))
                    context.BookingStatuses.Add(new BookingStatus { Name = statusName });
            }
            context.SaveChanges();

            if (!context.Cars.Any() && !context.Locations.Any() && !context.CarBrands.Any())
            {
                // 2a. Car Statuses
                var statuses = new List<CarStatus> { 
                    new CarStatus { Name = "Available" }, 
                    new CarStatus { Name = "Rented" }, 
                    new CarStatus { Name = "Maintenance" },
                    new CarStatus { Name = "Pending" } 
                };
                context.CarStatuses.AddRange(statuses);
                
                var carTypes = new List<CarType>
                {
                    new CarType { Name = "Sedan 5 chỗ", Description = "Xe sedan 5 chỗ ngồi" },
                    new CarType { Name = "SUV 5 chỗ", Description = "Xe thể thao 5 chỗ" },
                    new CarType { Name = "SUV 7 chỗ", Description = "Xe thể thao 7 chỗ" },
                    new CarType { Name = "MPV 7 chỗ", Description = "Xe đa dụng 7 chỗ" },
                    new CarType { Name = "Bán tải", Description = "Xe bán tải" },
                    new CarType { Name = "Hatchback", Description = "Xe hatchback cỡ nhỏ" }
                };
                context.CarTypes.AddRange(carTypes);

                var locations = new List<Location>
                {
                    new Location { Name = "TP. Hồ Chí Minh" },
                    new Location { Name = "Hà Nội" },
                    new Location { Name = "Đà Nẵng" },
                    new Location { Name = "Cần Thơ" },
                    new Location { Name = "Bình Dương" },
                    new Location { Name = "Đồng Nai" },
                    new Location { Name = "Nha Trang" },
                    new Location { Name = "Hải Phòng" },
                    new Location { Name = "Bà Rịa - VÅ©ng Tàu" },
                    new Location { Name = "Cà Mau" },
                    new Location { Name = "Lâm Đồng" },
                    new Location { Name = "Phú Quốc" }
                };
                context.Locations.AddRange(locations);
                context.SaveChanges();

                var brands = new List<CarBrand>
                {
                    new CarBrand { Name = "Toyota" },
                    new CarBrand { Name = "Honda" },
                    new CarBrand { Name = "Hyundai" },
                    new CarBrand { Name = "VinFast" },
                    new CarBrand { Name = "Mazda" },
                    new CarBrand { Name = "Ford" },
                    new CarBrand { Name = "Mitsubishi" },
                    new CarBrand { Name = "Kia" }
                };
                context.CarBrands.AddRange(brands);
                context.SaveChanges();

                // 2b. Cars
                var cars = new List<Car>
                {
                    new Car { CarName = "Vios G", BrandID = brands[0].BrandID, Seats = 5, LicensePlate = "51A-12345", VIN = "TOYOTA12345678901", PricePerDay = 800000, PricePerHour = 100000, CarStatusID = statuses[0].CarStatusID, Description = "Xe phổ thông đi phố.", CurrentOdometer = 12500, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[0].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = false, ImageURL = "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800" },
                    new Car { CarName = "City RS", BrandID = brands[1].BrandID, Seats = 5, LicensePlate = "51G-67890", VIN = "HONDA123456789012", PricePerDay = 900000, PricePerHour = 110000, CarStatusID = statuses[0].CarStatusID, Description = "Thể thao, tiết kiệm.", CurrentOdometer = 8000, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[0].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[4].LocationID, IsPremium = false, ImageURL = "https://images.unsplash.com/photo-1606611013016-969c19ba27b5?w=800" },
                    new Car { CarName = "Accent MT", BrandID = brands[2].BrandID, Seats = 5, LicensePlate = "51D-11111", VIN = "HYUN123456789012", PricePerDay = 600000, PricePerHour = 80000, CarStatusID = statuses[0].CarStatusID, Description = "Xe gia đình.", CurrentOdometer = 20000, CreatedAt = DateTime.UtcNow.AddDays(-2), CarTypeID = carTypes[0].CarTypeID, FuelType = "Xăng", Transmission = "Số sàn", LocationID = locations[8].LocationID, IsPremium = false, DiscountPrice = 500000, ImageURL = "https://images.unsplash.com/photo-1580273916550-e323be2ae537?w=800" },
                    new Car { CarName = "VF5 Plus", BrandID = brands[3].BrandID, Seats = 5, LicensePlate = "51E-22222", VIN = "VFE1234567890123", PricePerDay = 750000, PricePerHour = 90000, CarStatusID = statuses[0].CarStatusID, Description = "Xe điện cỡ nhỏ.", CurrentOdometer = 5000, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[0].CarTypeID, FuelType = "Điện", Transmission = "Tự động", LocationID = locations[1].LocationID, IsPremium = false, ImageURL = "https://images.unsplash.com/photo-1560958089-b8a1929cea89?w=800" },
                    new Car { CarName = "CX-5 Signature", BrandID = brands[4].BrandID, Seats = 5, LicensePlate = "51H-99887", VIN = "MAZDA123456789012", PricePerDay = 1500000, PricePerHour = 180000, CarStatusID = statuses[0].CarStatusID, Description = "Premium SUV.", CurrentOdometer = 15000, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[1].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[2].LocationID, IsPremium = true, ImageURL = "https://images.unsplash.com/photo-1533473359331-0135ef1b58bf?w=800" },
                    new Car { CarName = "Everest Titanium", BrandID = brands[5].BrandID, Seats = 7, LicensePlate = "51F-33333", VIN = "FORD123456789012X", PricePerDay = 1800000, PricePerHour = 200000, CarStatusID = statuses[0].CarStatusID, Description = "SUV 7 chỗ cỡ lớn.", CurrentOdometer = 10000, CreatedAt = DateTime.UtcNow.AddDays(-5), CarTypeID = carTypes[2].CarTypeID, FuelType = "Dầu", Transmission = "Tự động", LocationID = locations[6].LocationID, IsPremium = true, ImageURL = "https://images.unsplash.com/photo-1519641471654-76ce0107ad1b?w=800" },
                    new Car { CarName = "Santa Fe Premium", BrandID = brands[2].BrandID, Seats = 7, LicensePlate = "51K-44444", VIN = "HYUN723456789012", PricePerDay = 1700000, PricePerHour = 190000, CarStatusID = statuses[0].CarStatusID, Description = "Sang trọng, đẳng cấp.", CurrentOdometer = 8500, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[2].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = true, DiscountPrice = 1600000, ImageURL = "https://images.unsplash.com/photo-1503376780353-7e6692767b70?w=800" },
                    new Car { CarName = "VF8 Eco", BrandID = brands[3].BrandID, Seats = 5, LicensePlate = "51V-55555", VIN = "VFE8234567890123", PricePerDay = 1600000, PricePerHour = 180000, CarStatusID = statuses[0].CarStatusID, Description = "SUV điện mạnh mẽ.", CurrentOdometer = 3000, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[1].CarTypeID, FuelType = "Điện", Transmission = "Tự động", LocationID = locations[11].LocationID, IsPremium = true, ImageURL = "https://images.unsplash.com/photo-1549399542-7e3f8b79c341?w=800" },
                    new Car { CarName = "Xpander AT", BrandID = brands[6].BrandID, Seats = 7, LicensePlate = "51L-66666", VIN = "MITSU12345678901", PricePerDay = 950000, PricePerHour = 120000, CarStatusID = statuses[0].CarStatusID, Description = "MPV Quốc dân.", CurrentOdometer = 18000, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[3].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[7].LocationID, IsPremium = false, ImageURL = "https://images.unsplash.com/photo-1552519507-da3b142c6e3d?w=800" },
                    new Car { CarName = "Carnival Signature", BrandID = brands[7].BrandID, Seats = 7, LicensePlate = "51C-77777", VIN = "KIA1234567890123", PricePerDay = 2200000, PricePerHour = 250000, CarStatusID = statuses[0].CarStatusID, Description = "MPV VIP Rộng rãi.", CurrentOdometer = 5000, CreatedAt = DateTime.UtcNow.AddDays(-10), CarTypeID = carTypes[3].CarTypeID, FuelType = "Dầu", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = true, ImageURL = "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?w=800" },
                    new Car { CarName = "Innova Cross", BrandID = brands[0].BrandID, Seats = 7, LicensePlate = "51I-88888", VIN = "TOYOI12345678901", PricePerDay = 1200000, PricePerHour = 150000, CarStatusID = statuses[0].CarStatusID, Description = "Bền bỉ rộng rãi.", CurrentOdometer = 2000, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[3].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[5].LocationID, IsPremium = false, ImageURL = "https://images.unsplash.com/photo-1542362567-b07e54358753?w=800" },
                    new Car { CarName = "Ranger Raptor", BrandID = brands[5].BrandID, Seats = 5, LicensePlate = "51C-11223", VIN = "FORDR12345678901", PricePerDay = 2500000, PricePerHour = 300000, CarStatusID = statuses[0].CarStatusID, Description = "Siêu bán tải.", CurrentOdometer = 5400, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[4].CarTypeID, FuelType = "Dầu", Transmission = "Tự động", LocationID = locations[1].LocationID, IsPremium = true, ImageURL = "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?w=800" },
                    new Car { CarName = "Hilux Adventure", BrandID = brands[0].BrandID, Seats = 5, LicensePlate = "51C-99999", VIN = "TOYOH12345678901", PricePerDay = 1300000, PricePerHour = 160000, CarStatusID = statuses[0].CarStatusID, Description = "Bán tải bền bỉ.", CurrentOdometer = 25000, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[4].CarTypeID, FuelType = "Dầu", Transmission = "Số tự động", LocationID = locations[9].LocationID, IsPremium = false, DiscountPrice = 1100000, ImageURL = "https://images.unsplash.com/photo-1612825173281-9a193378527e?w=800" },
                    new Car { CarName = "Morning MT", BrandID = brands[7].BrandID, Seats = 5, LicensePlate = "51H-10101", VIN = "KIAM123456789012", PricePerDay = 450000, PricePerHour = 60000, CarStatusID = statuses[0].CarStatusID, Description = "Xe cỡ nhỏ di chuyển hẻm.", CurrentOdometer = 35000, CreatedAt = DateTime.UtcNow.AddDays(-1), CarTypeID = carTypes[5].CarTypeID, FuelType = "Xăng", Transmission = "Số sàn", LocationID = locations[3].LocationID, IsPremium = false, ImageURL = "https://images.unsplash.com/photo-1609521263047-f8f205293f24?w=800" },
                    new Car { CarName = "Grand i10", BrandID = brands[2].BrandID, Seats = 5, LicensePlate = "51H-20202", VIN = "HYUNI12345678901", PricePerDay = 550000, PricePerHour = 70000, CarStatusID = statuses[0].CarStatusID, Description = "Thiết kế mới, rộng hàng đầu phân khúc nhỏ.", CurrentOdometer = 12000, CreatedAt = DateTime.UtcNow, CarTypeID = carTypes[5].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[10].LocationID, IsPremium = false, ImageURL = "https://images.unsplash.com/photo-1616422285623-13ff0162193c?w=800" }
                };
                
                context.Cars.AddRange(cars);
                context.SaveChanges();

                // Seed sample Review + MaintenanceLog
                var car1 = cars.FirstOrDefault(c => c.IsPremium);
                var user = context.Users.FirstOrDefault(u => u.Role == "Customer");
                if (car1 != null && user != null)
                {
                    context.Reviews.Add(new Review { CarID = car1.CarID, UserID = user.UserID, Rating = 5, Comment = "Rất sạch sẽ và chạy bốc!", CreatedAt = DateTime.UtcNow.AddDays(-5) });
                    context.MaintenanceLogs.Add(new MaintenanceLog { CarID = car1.CarID, ServiceDate = DateTime.UtcNow.AddMonths(-1), Description = "Bảo trì định kì", Cost = 1500000, OdometerAtService = 12000, ServiceCenter = "Toyota Official Service" });
                    context.SaveChanges();
                }
            }

            // ═══ 3. ADD-ONS ═══
            if (!context.AddOns.Any())
            {
                context.AddOns.AddRange(new List<AddOn>
                {
                    new AddOn { Name = "Ghế trẻ em", PricePerDay = 50000, IsActive = true },
                    new AddOn { Name = "Combo nước suối + khăn ướt", PricePerDay = 25000, IsActive = true },
                    new AddOn { Name = "Gói nước ngọt (6 lon)", PricePerDay = 60000, IsActive = true },
                    new AddOn { Name = "Nệm hơi ô tô", PricePerDay = 80000, IsActive = true },
                    new AddOn { Name = "Bộ dụng cụ cắm trại", PricePerDay = 100000, IsActive = true }
                });
                context.SaveChanges();
            }

            // ═══ 4. PROMOTIONS ═══
            if (!context.Promotions.Any())
            {
                context.Promotions.AddRange(new List<Promotion>
                {
                    new Promotion { PromoCode = "WELCOME10", DiscountPercent = 10, MaxDiscountAmount = 200000, ValidFrom = DateTime.UtcNow.AddDays(-30), ValidUntil = DateTime.UtcNow.AddDays(90), UsageLimit = 100, IsActive = true },
                    new Promotion { PromoCode = "SUMMER20", DiscountPercent = 20, MaxDiscountAmount = 500000, ValidFrom = DateTime.UtcNow.AddDays(-10), ValidUntil = DateTime.UtcNow.AddDays(60), UsageLimit = 50, IsActive = true },
                    new Promotion { PromoCode = "VIP30", DiscountPercent = 30, MaxDiscountAmount = 1000000, ValidFrom = DateTime.UtcNow, ValidUntil = DateTime.UtcNow.AddDays(30), UsageLimit = 10, IsActive = true }
                });
                context.SaveChanges();
            }

            // ═══ 5. MEMBERSHIP TIERS ═══
            if (!context.MembershipTiers.Any())
            {
                var tiers = new List<MembershipTier>
                {
                    new MembershipTier { Name = "Bronze", MinSpending = 0, DiscountPercent = 0, Icon = "bi-star", Color = "#cd7f32" },
                    new MembershipTier { Name = "Silver", MinSpending = 10000000, DiscountPercent = 5, Icon = "bi-award", Color = "#c0c0c0" },
                    new MembershipTier { Name = "Gold", MinSpending = 30000000, DiscountPercent = 10, Icon = "bi-trophy", Color = "#ffd700" }
                };
                context.MembershipTiers.AddRange(tiers);
                context.SaveChanges();
            }

            // ═══ 6. ASSIGN MEMBERSHIPS ═══
            if (context.Users.Any() && !context.Users.Any(u => u.MembershipTierID != null))
            {
                var goldTier = context.MembershipTiers.FirstOrDefault(t => t.Name == "Gold");
                var silverTier = context.MembershipTiers.FirstOrDefault(t => t.Name == "Silver");
                var users = context.Users.Where(u => u.Role == "Customer").ToList();
                if (goldTier != null && users.Count > 0)
                {
                    users[0].MembershipTierID = goldTier.MembershipTierID;
                }
                if (silverTier != null && users.Count > 1)
                {
                    users[1].MembershipTierID = silverTier.MembershipTierID;
                }
                context.SaveChanges();
            }

            // ═══ 7. AVAILABILITY SCHEDULE ═══
            if (!context.AvailabilitySchedules.Any())
            {
                var car1 = context.Cars.FirstOrDefault();
                if (car1 != null)
                {
                    context.AvailabilitySchedules.Add(new AvailabilitySchedule
                    {
                        EntityID = car1.CarID,
                        EntityType = "Car",
                        StartTime = DateTime.UtcNow.AddDays(1),
                        EndTime = DateTime.UtcNow.AddDays(3),
                        Status = "Booked"
                    });
                    context.SaveChanges();
                }
            }

            // ═══ 8. SUB-SEEDERS (Xe mở rộng, Tài xế, Đối tác) ═══
            CarMockDataSeeder.Seed(context);
            BlogMockDataSeeder.Seed(context);
            EnsureDefaultCarInventory(context);
        }

        private static void EnsureDefaultCarInventory(ApplicationDbContext context)
        {
            var carIdsWithInventory = context.CarInventoryItems
                .IgnoreQueryFilters()
                .Select(i => i.CarID)
                .Distinct()
                .ToHashSet();

            var carsWithoutInventory = context.Cars
                .Where(c => !carIdsWithInventory.Contains(c.CarID))
                .Select(c => c.CarID)
                .ToList();

            if (carsWithoutInventory.Count == 0)
                return;

            foreach (var carId in carsWithoutInventory)
            {
                context.CarInventoryItems.AddRange(BuildDefaultInventory(carId));
            }

            context.SaveChanges();
        }

        private static IEnumerable<CarInventoryItem> BuildDefaultInventory(int carId)
        {
            return new[]
            {
                new CarInventoryItem { CarID = carId, ItemName = "Dang kiem/bao hiem photo", Quantity = 1, UnitReplacementCost = 200000, Description = "Bo giay to ban sao tren xe." },
                new CarInventoryItem { CarID = carId, ItemName = "Tham san", Quantity = 1, UnitReplacementCost = 500000 },
                new CarInventoryItem { CarID = carId, ItemName = "Banh du phong", Quantity = 1, UnitReplacementCost = 2500000 },
                new CarInventoryItem { CarID = carId, ItemName = "Bo kich va co le", Quantity = 1, UnitReplacementCost = 1200000 },
                new CarInventoryItem { CarID = carId, ItemName = "Camera hanh trinh/the nho", Quantity = 1, UnitReplacementCost = 1800000 },
                new CarInventoryItem { CarID = carId, ItemName = "Cap sac dien thoai", Quantity = 1, UnitReplacementCost = 250000 },
                new CarInventoryItem { CarID = carId, ItemName = "Bom lop mini", Quantity = 1, UnitReplacementCost = 700000 }
            };
        }
    }
}

