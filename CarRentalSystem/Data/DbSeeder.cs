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
        public static void Seed(ApplicationDbContext context)
        {
            context.Database.Migrate();

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
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        Name = "John Customer",
                        Email = "john@example.com",
                        Phone = "0901111111",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                        Role = "Customer",
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        Name = "Mary Jane",
                        Email = "mary@example.com",
                        Phone = "0902222222",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                        Role = "Customer",
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        Name = "Peter Parker",
                        Email = "peter@example.com",
                        Phone = "0903333333",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                        Role = "Customer",
                        CreatedAt = DateTime.Now
                    }
                };
                context.Users.AddRange(users);
                context.SaveChanges();
            }
            
            // Rename existing locations from Districts to Cities if they haven't been renamed yet
            var existingLocs = context.Locations.ToList();
            if (existingLocs.Any() && existingLocs.Any(l => l.Name.Contains("Quận 1, TP.HCM")))
            {
                var cityNames = new[] { "TP. Hồ Chí Minh", "Hà Nội", "Đà Nẵng", "Cần Thơ", "Bình Dương", "Đồng Nai", "Nha Trang", "Hải Phòng", "Bà Rịa - Vũng Tàu", "Cà Mau", "Lâm Đồng", "Phú Quốc" };
                for (int i = 0; i < existingLocs.Count && i < cityNames.Length; i++)
                {
                    existingLocs[i].Name = cityNames[i];
                }
                context.SaveChanges();
            }


            if (!context.Cars.Any() && !context.Locations.Any() && !context.CarBrands.Any())
            {


            // 1. Seed Lookups
            var statuses = new List<CarStatus> { 
                new CarStatus { Name = "Available" }, 
                new CarStatus { Name = "Rented" }, 
                new CarStatus { Name = "Maintenance" },
                new CarStatus { Name = "Pending" } 
            };
            context.CarStatuses.AddRange(statuses);
            
            var bookingStatuses = new List<BookingStatus> { 
                new BookingStatus { Name = "Draft" },
                new BookingStatus { Name = "Pending" }, 
                new BookingStatus { Name = "Confirmed" }, 
                new BookingStatus { Name = "Active" },
                new BookingStatus { Name = "Completed" }, 
                new BookingStatus { Name = "Cancelled" } 
            };
            context.BookingStatuses.AddRange(bookingStatuses);

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
                new Location { Name = "Bà Rịa - Vũng Tàu" },
                new Location { Name = "Cà Mau" },
                new Location { Name = "Lâm Đồng" },
                new Location { Name = "Phú Quốc" }
            };
            context.Locations.AddRange(locations);
            context.SaveChanges();

            var brands = new List<CarBrand>
            {
                new CarBrand { Name = "Toyota" },      // 0
                new CarBrand { Name = "Honda" },       // 1
                new CarBrand { Name = "Hyundai" },     // 2
                new CarBrand { Name = "VinFast" },     // 3
                new CarBrand { Name = "Mazda" },       // 4
                new CarBrand { Name = "Ford" },        // 5
                new CarBrand { Name = "Mitsubishi" },  // 6
                new CarBrand { Name = "Kia" }          // 7
            };
            context.CarBrands.AddRange(brands);
            context.SaveChanges();

            var cars = new List<Car>
            {
                // Sedan 5 chỗ (Xăng & Điện)
                new Car { CarName = "Vios G", BrandID = brands[0].BrandID, Seats = 5, LicensePlate = "51A-12345", VIN = "TOYOTA12345678901", PricePerDay = 800000, PricePerHour = 100000, CarStatusID = statuses[0].CarStatusID, Description = "Xe phổ thông đi phố.", CurrentOdometer = 12500, CreatedAt = DateTime.Now, CarTypeID = carTypes[0].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = false, ImageURL = "https://images.toyota.com.vn/toyota/vios/exterior/vios_exterior_1.jpg" },
                new Car { CarName = "City RS", BrandID = brands[1].BrandID, Seats = 5, LicensePlate = "51G-67890", VIN = "HONDA123456789012", PricePerDay = 900000, PricePerHour = 110000, CarStatusID = statuses[0].CarStatusID, Description = "Thể thao, tiết kiệm.", CurrentOdometer = 8000, CreatedAt = DateTime.Now, CarTypeID = carTypes[0].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[4].LocationID, IsPremium = false, ImageURL = "https://www.honda.com.vn/o-to/san-pham/honda-city/images/pc/img-city-color-5.png" },
                new Car { CarName = "Accent MT", BrandID = brands[2].BrandID, Seats = 5, LicensePlate = "51D-11111", VIN = "HYUN123456789012", PricePerDay = 600000, PricePerHour = 80000, CarStatusID = statuses[0].CarStatusID, Description = "Xe gia đình.", CurrentOdometer = 20000, CreatedAt = DateTime.Now.AddDays(-2), CarTypeID = carTypes[0].CarTypeID, FuelType = "Xăng", Transmission = "Số sàn", LocationID = locations[8].LocationID, IsPremium = false, DiscountPrice = 500000, ImageURL = "https://hyundai-vietnam.vn/wp-content/uploads/2021/04/hyundai-accent-trang.png" },
                new Car { CarName = "VF5 Plus", BrandID = brands[3].BrandID, Seats = 5, LicensePlate = "51E-22222", VIN = "VFE1234567890123", PricePerDay = 750000, PricePerHour = 90000, CarStatusID = statuses[0].CarStatusID, Description = "Xe điện cỡ nhỏ.", CurrentOdometer = 5000, CreatedAt = DateTime.Now, CarTypeID = carTypes[0].CarTypeID, FuelType = "Điện", Transmission = "Tự động", LocationID = locations[1].LocationID, IsPremium = false, ImageURL = "https://shop.vinfastauto.com/on/demandware.static/-/Sites-app_vinfast_vn-Library/default/dw1062b9a7/images/VF5/VF5-Plus-Cam.png" },

                // SUV 5 chỗ & 7 chỗ
                new Car { CarName = "CX-5 Signature", BrandID = brands[4].BrandID, Seats = 5, LicensePlate = "51H-99887", VIN = "MAZDA123456789012", PricePerDay = 1500000, PricePerHour = 180000, CarStatusID = statuses[0].CarStatusID, Description = "Premium SUV.", CurrentOdometer = 15000, CreatedAt = DateTime.Now, CarTypeID = carTypes[1].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[2].LocationID, IsPremium = true, ImageURL = "https://mazdamotors.vn/media/m3ojj3p1/mazda-cx-5-v8_exterior-360-10.jpg" },
                new Car { CarName = "Everest Titanium", BrandID = brands[5].BrandID, Seats = 7, LicensePlate = "51F-33333", VIN = "FORD123456789012X", PricePerDay = 1800000, PricePerHour = 200000, CarStatusID = statuses[0].CarStatusID, Description = "SUV 7 chỗ cỡ lớn.", CurrentOdometer = 10000, CreatedAt = DateTime.Now.AddDays(-5), CarTypeID = carTypes[2].CarTypeID, FuelType = "Dầu", Transmission = "Tự động", LocationID = locations[6].LocationID, IsPremium = true, ImageURL = "https://www.ford.com.vn/content/dam/Ford/website/VNM/home/car-cuv-suv/everest/2023/gallery/gallery-next-gen-everest-1.jpg" },
                new Car { CarName = "Santa Fe Premium", BrandID = brands[2].BrandID, Seats = 7, LicensePlate = "51K-44444", VIN = "HYUN723456789012", PricePerDay = 1700000, PricePerHour = 190000, CarStatusID = statuses[0].CarStatusID, Description = "Sang trọng, đẳng cấp.", CurrentOdometer = 8500, CreatedAt = DateTime.Now, CarTypeID = carTypes[2].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = true, DiscountPrice = 1600000, ImageURL = "https://hyundai-vietnam.vn/wp-content/uploads/2021/05/santafe-trang.png" },
                new Car { CarName = "VF8 Eco", BrandID = brands[3].BrandID, Seats = 5, LicensePlate = "51V-55555", VIN = "VFE8234567890123", PricePerDay = 1600000, PricePerHour = 180000, CarStatusID = statuses[0].CarStatusID, Description = "SUV điện mạnh mẽ.", CurrentOdometer = 3000, CreatedAt = DateTime.Now, CarTypeID = carTypes[1].CarTypeID, FuelType = "Điện", Transmission = "Tự động", LocationID = locations[11].LocationID, IsPremium = true, ImageURL = "https://shop.vinfastauto.com/on/demandware.static/-/Sites-app_vinfast_vn-Library/default/dw1062b9a7/images/VF8/VF8-Eco-Xanh.png" },

                // MPV 7 chỗ
                new Car { CarName = "Xpander AT", BrandID = brands[6].BrandID, Seats = 7, LicensePlate = "51L-66666", VIN = "MITSU12345678901", PricePerDay = 950000, PricePerHour = 120000, CarStatusID = statuses[0].CarStatusID, Description = "MPV Quốc dân.", CurrentOdometer = 18000, CreatedAt = DateTime.Now, CarTypeID = carTypes[3].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[7].LocationID, IsPremium = false, ImageURL = "https://www.mitsubishi-motors.com.vn/storage/xpander/xpander-trang.png" },
                new Car { CarName = "Carnival Signature", BrandID = brands[7].BrandID, Seats = 7, LicensePlate = "51C-77777", VIN = "KIA1234567890123", PricePerDay = 2200000, PricePerHour = 250000, CarStatusID = statuses[0].CarStatusID, Description = "MPV VIP Rộng rãi.", CurrentOdometer = 5000, CreatedAt = DateTime.Now.AddDays(-10), CarTypeID = carTypes[3].CarTypeID, FuelType = "Dầu", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = true, ImageURL = "https://kia-vietnam.com.vn/storage/carnival/carnival-trang.png" },
                new Car { CarName = "Innova Cross", BrandID = brands[0].BrandID, Seats = 7, LicensePlate = "51I-88888", VIN = "TOYOI12345678901", PricePerDay = 1200000, PricePerHour = 150000, CarStatusID = statuses[0].CarStatusID, Description = "Bền bỉ rộng rãi.", CurrentOdometer = 2000, CreatedAt = DateTime.Now, CarTypeID = carTypes[3].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[5].LocationID, IsPremium = false, ImageURL = "https://toyota-vietnam.vn/wp-content/uploads/2023/10/innova-cross-trang.png" },

                // Bán Tải (Pickup)
                new Car { CarName = "Ranger Raptor", BrandID = brands[5].BrandID, Seats = 5, LicensePlate = "51C-11223", VIN = "FORDR12345678901", PricePerDay = 2500000, PricePerHour = 300000, CarStatusID = statuses[0].CarStatusID, Description = "Siêu bán tải.", CurrentOdometer = 5400, CreatedAt = DateTime.Now, CarTypeID = carTypes[4].CarTypeID, FuelType = "Dầu", Transmission = "Tự động", LocationID = locations[1].LocationID, IsPremium = true, ImageURL = "https://www.ford.com.vn/content/dam/Ford/website/VNM/home/car-trucks/ranger-raptor/raptor-gallery-1.jpg" },
                new Car { CarName = "Hilux Adventure", BrandID = brands[0].BrandID, Seats = 5, LicensePlate = "51C-99999", VIN = "TOYOH12345678901", PricePerDay = 1300000, PricePerHour = 160000, CarStatusID = statuses[0].CarStatusID, Description = "Bán tải bền bỉ.", CurrentOdometer = 25000, CreatedAt = DateTime.Now, CarTypeID = carTypes[4].CarTypeID, FuelType = "Dầu", Transmission = "Số tự động", LocationID = locations[9].LocationID, IsPremium = false, DiscountPrice = 1100000, ImageURL = "https://toyota-vietnam.vn/wp-content/uploads/2023/03/toyota-hilux-trang.png" },

                // Hatchback
                new Car { CarName = "Morning MT", BrandID = brands[7].BrandID, Seats = 5, LicensePlate = "51H-10101", VIN = "KIAM123456789012", PricePerDay = 450000, PricePerHour = 60000, CarStatusID = statuses[0].CarStatusID, Description = "Xe cỡ nhỏ di chuyển hẻm.", CurrentOdometer = 35000, CreatedAt = DateTime.Now.AddDays(-1), CarTypeID = carTypes[5].CarTypeID, FuelType = "Xăng", Transmission = "Số sàn", LocationID = locations[3].LocationID, IsPremium = false, ImageURL = "https://kia-vietnam.com.vn/storage/morning/morning-trang.png" },
                new Car { CarName = "Grand i10", BrandID = brands[2].BrandID, Seats = 5, LicensePlate = "51H-20202", VIN = "HYUNI12345678901", PricePerDay = 550000, PricePerHour = 70000, CarStatusID = statuses[0].CarStatusID, Description = "Thiết kế mới, rộng hàng đầu phân khúc nhỏ.", CurrentOdometer = 12000, CreatedAt = DateTime.Now, CarTypeID = carTypes[5].CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[10].LocationID, IsPremium = false, ImageURL = "https://hyundai-vietnam.vn/wp-content/uploads/2021/08/i10-trang.png" }
            };
            
            context.Cars.AddRange(cars);
            context.SaveChanges();

            // Phụ thuộc Foreign Key - Seed lại Review cho xe mới nhất
            var car1 = cars.FirstOrDefault(c => c.IsPremium);
            var user = context.Users.FirstOrDefault(u => u.Role == "Customer");
            if (car1 != null && user != null)
            {
                context.Reviews.Add(new Review { CarID = car1.CarID, UserID = user.UserID, Rating = 5, Comment = "Rất sạch sẽ và chạy bốc!", CreatedAt = DateTime.Now.AddDays(-5) });
                context.MaintenanceLogs.Add(new MaintenanceLog { CarID = car1.CarID, ServiceDate = DateTime.Now.AddMonths(-1), Description = "Bảo trì định kì", Cost = 1500000, OdometerAtService = 12000, ServiceCenter = "Toyota Official Service" });
                context.SaveChanges();
            }

            }

            // Seed AddOns
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

            // Seed Promotions
            if (!context.Promotions.Any())
            {
                context.Promotions.AddRange(new List<Promotion>
                {
                    new Promotion { PromoCode = "WELCOME10", DiscountPercent = 10, MaxDiscountAmount = 200000, ValidFrom = DateTime.Now.AddDays(-30), ValidUntil = DateTime.Now.AddDays(90), UsageLimit = 100, IsActive = true },
                    new Promotion { PromoCode = "SUMMER20", DiscountPercent = 20, MaxDiscountAmount = 500000, ValidFrom = DateTime.Now.AddDays(-10), ValidUntil = DateTime.Now.AddDays(60), UsageLimit = 50, IsActive = true },
                    new Promotion { PromoCode = "VIP30", DiscountPercent = 30, MaxDiscountAmount = 1000000, ValidFrom = DateTime.Now, ValidUntil = DateTime.Now.AddDays(30), UsageLimit = 10, IsActive = true }
                });
                context.SaveChanges();
            }
            // Seed Membership Tiers
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

            // Assign memberships to users
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

            // Seed Availability Schedule
            if (!context.AvailabilitySchedules.Any())
            {
                var car1 = context.Cars.FirstOrDefault();
                if (car1 != null)
                {
                    context.AvailabilitySchedules.Add(new AvailabilitySchedule
                    {
                        EntityID = car1.CarID,
                        EntityType = "Car",
                        StartTime = DateTime.Now.AddDays(1),
                        EndTime = DateTime.Now.AddDays(3),
                        Status = "Booked" // This car is unavailable for the next 1-3 days
                    });
                    context.SaveChanges();
                }
            }

            CarMockDataSeeder.Seed(context);
            DriverMockDataSeeder.Seed(context);
            PartnerMockDataSeeder.Seed(context);
        }
    }
}
