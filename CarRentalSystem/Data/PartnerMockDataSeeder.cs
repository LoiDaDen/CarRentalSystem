using BCrypt.Net;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarRentalSystem.Data
{
    public static class PartnerMockDataSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            if (!context.Locations.Any() || !context.CarBrands.Any() || !context.CarTypes.Any())
            {
                return;
            }

            var locations = context.Locations.ToList();
            var brands = context.CarBrands.ToList();
            var carTypes = context.CarTypes.ToList();
            var statuses = context.CarStatuses.ToList();

            if (!statuses.Any()) return;
            var availableStatus = statuses.FirstOrDefault(s => s.Name == "Available")?.CarStatusID ?? statuses.First().CarStatusID;

            var partners = new List<User>
            {
                new User
                {
                    Name = "Vina Auto (Partner)",
                    Email = "vina.partner@seed.local",
                    Phone = "0944000111",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Partner@123"),
                    Role = "Partner",
                    IsEmailVerified = true,
                    IsVerified = true,
                    PartnerAddress = "Quận 3, TP. Hồ Chí Minh",
                    CreatedAt = DateTime.UtcNow.AddMonths(-12)
                },
                new User
                {
                    Name = "Thanh Thanh Car",
                    Email = "thanhthanh.partner@seed.local",
                    Phone = "0944000222",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Partner@123"),
                    Role = "Partner",
                    IsEmailVerified = true,
                    IsVerified = true,
                    PartnerAddress = "Quận Cầu Giấy, Hà Nội",
                    CreatedAt = DateTime.UtcNow.AddMonths(-10)
                },
                new User
                {
                    Name = "Minh Tri Transport",
                    Email = "minhtri.partner@seed.local",
                    Phone = "0944000333",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Partner@123"),
                    Role = "Partner",
                    IsEmailVerified = true,
                    IsVerified = true,
                    PartnerAddress = "Quận Hải Châu, Đà Nẵng",
                    CreatedAt = DateTime.UtcNow.AddMonths(-8)
                }
            };

            foreach (var partner in partners)
            {
                if (!context.Users.Any(u => u.Email == partner.Email))
                {
                    context.Users.Add(partner);
                }
            }
            context.SaveChanges();

            // Load saved partners
            var p1 = context.Users.FirstOrDefault(u => u.Email == "vina.partner@seed.local");
            var p2 = context.Users.FirstOrDefault(u => u.Email == "thanhthanh.partner@seed.local");
            var p3 = context.Users.FirstOrDefault(u => u.Email == "minhtri.partner@seed.local");

            var tBrand = brands.FirstOrDefault(b => b.Name == "Toyota") ?? brands[0];
            var hBrand = brands.FirstOrDefault(b => b.Name == "Hyundai") ?? brands[0];
            var vBrand = brands.FirstOrDefault(b => b.Name == "VinFast") ?? brands[0];
            var kBrand = brands.FirstOrDefault(b => b.Name == "Kia") ?? brands[0];
            var cTypeSedan = carTypes.FirstOrDefault(t => t.Name.Contains("Sedan")) ?? carTypes[0];
            var cTypeSUV = carTypes.FirstOrDefault(t => t.Name.Contains("SUV")) ?? carTypes[0];

            var partnerCars = new List<Car>
            {
                // Vina Auto Cars
                new Car { OwnerID = p1?.UserID, CarName = "Camry 2.0Q", BrandID = tBrand.BrandID, Seats = 5, LicensePlate = "51H-123.45", VIN = "TOYCAMRY123456789", PricePerDay = 1200000, PricePerHour = 150000, CarStatusID = availableStatus, Description = "Sedan cao cấp, êm ái.", CurrentOdometer = 25000, CarTypeID = cTypeSedan.CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = true, CommissionRate = 18.00m, ImageURL = "https://images.toyota.com.vn/toyota/camry/exterior/camry_exterior_1.jpg" },
                new Car { OwnerID = p1?.UserID, CarName = "Tucson 1.6T", BrandID = hBrand.BrandID, Seats = 5, LicensePlate = "51G-543.21", VIN = "HYUNTUCSON9876543", PricePerDay = 1300000, PricePerHour = 160000, CarStatusID = availableStatus, Description = "CUV 5 chỗ rộng rãi, thiết kế tương lai.", CurrentOdometer = 12000, CarTypeID = cTypeSUV.CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = false, CommissionRate = 20.00m, ImageURL = "https://hyundai-vietnam.vn/wp-content/uploads/2022/01/tucson-trang.png" },
                new Car { OwnerID = p1?.UserID, CarName = "Sorento Signature", BrandID = kBrand.BrandID, Seats = 7, LicensePlate = "51K-987.65", VIN = "KIASORENTO9876543", PricePerDay = 1800000, PricePerHour = 200000, CarStatusID = availableStatus, Description = "Xe gầm cao 7 chỗ bản full option.", CurrentOdometer = 8000, CarTypeID = cTypeSUV.CarTypeID, FuelType = "Dầu", Transmission = "Tự động", LocationID = locations[0].LocationID, IsPremium = true, CommissionRate = 15.00m, ImageURL = "https://kia-vietnam.com.vn/storage/sorento/sorento-trang.png" },

                // Thanh Thanh Cars
                new Car { OwnerID = p2?.UserID, CarName = "VF9 Plus", BrandID = vBrand.BrandID, Seats = 7, LicensePlate = "29A-111.11", VIN = "VINFASTVF91234567", PricePerDay = 2500000, PricePerHour = 300000, CarStatusID = availableStatus, Description = "SUV Điện siêu sang trọng.", CurrentOdometer = 3000, CarTypeID = cTypeSUV.CarTypeID, FuelType = "Điện", Transmission = "Tự động", LocationID = locations.Count > 1 ? locations[1].LocationID : locations[0].LocationID, IsPremium = true, CommissionRate = 20.00m, IsElectric = true, ImageURL = "https://shop.vinfastauto.com/on/demandware.static/-/Sites-app_vinfast_vn-Library/default/dw1062b9a7/images/VF9/VF9-Plus-Trang.png" },
                new Car { OwnerID = p2?.UserID, CarName = "Cerato 2.0 Premium", BrandID = kBrand.BrandID, Seats = 5, LicensePlate = "29A-222.22", VIN = "KIACERATO98765432", PricePerDay = 750000, PricePerHour = 90000, CarStatusID = availableStatus, Description = "Sedan trẻ trung, thể thao.", CurrentOdometer = 45000, CarTypeID = cTypeSedan.CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations.Count > 1 ? locations[1].LocationID : locations[0].LocationID, IsPremium = false, CommissionRate = 20.00m, ImageURL = "https://kia-vietnam.com.vn/storage/k3/k3-trang.png" },

                // Minh Tri Cars
                new Car { OwnerID = p3?.UserID, CarName = "Corolla Cross 1.8V", BrandID = tBrand.BrandID, Seats = 5, LicensePlate = "43A-333.33", VIN = "TOYOCROSS12345678", PricePerDay = 1100000, PricePerHour = 140000, CarStatusID = availableStatus, Description = "Crossover bền bỉ của Toyota.", CurrentOdometer = 18000, CarTypeID = cTypeSUV.CarTypeID, FuelType = "Xăng", Transmission = "Tự động", LocationID = locations.Count > 2 ? locations[2].LocationID : locations[0].LocationID, IsPremium = false, CommissionRate = 20.00m, ImageURL = "https://toyota-vietnam.vn/wp-content/uploads/2021/08/cross-trang.png" },
                new Car { OwnerID = p3?.UserID, CarName = "VF e34", BrandID = vBrand.BrandID, Seats = 5, LicensePlate = "43A-444.44", VIN = "VINFASTE341234567", PricePerDay = 800000, PricePerHour = 100000, CarStatusID = availableStatus, Description = "Xe điện nội đô nhỏ gọn.", CurrentOdometer = 22000, CarTypeID = cTypeSUV.CarTypeID, FuelType = "Điện", Transmission = "Tự động", LocationID = locations.Count > 2 ? locations[2].LocationID : locations[0].LocationID, IsPremium = false, CommissionRate = 20.00m, IsElectric = true, ImageURL = "https://shop.vinfastauto.com/on/demandware.static/-/Sites-app_vinfast_vn-Library/default/dw1062b9a7/images/VFe34/vfe34-trang.png" },
            };

            foreach (var car in partnerCars)
            {
                if (!context.Cars.Any(c => c.LicensePlate == car.LicensePlate))
                {
                    context.Cars.Add(car);
                }
            }
            context.SaveChanges();
        }
    }
}
