using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarRentalSystem.Data
{
    public static class CarMockDataSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            var locations = context.Locations.ToList();
            if (!locations.Any()) return;
            
            // Fix old cars that have missing Latitude/Longitude
            var oldCars = context.Cars.Where(c => c.Latitude == null || c.Longitude == null || c.Latitude == 0).ToList();
            if (oldCars.Any())
            {
                var locOffsets = GetLocationOffsets();
                foreach (var car in oldCars)
                {
                    var loc = locations.FirstOrDefault(l => l.LocationID == car.LocationID);
                    if (loc != null && locOffsets.ContainsKey(loc.Name))
                    {
                        var coords = locOffsets[loc.Name];
                        car.Latitude = coords.lat;
                        car.Longitude = coords.lng;
                        car.CarAddress = loc.Name;
                    }
                }
                context.SaveChanges();
            }

            // Fix old cars with broken image URLs (manufacturer sites blocked)
            var brokenImgCars = context.Cars.Where(c => c.ImageURL == null || !c.ImageURL.StartsWith("https://images.unsplash.com")).ToList();
            if (brokenImgCars.Any())
            {
                var fallbackImages = new[] {
                    "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1619767886558-efdc259cde1a?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1580273916550-e323be2ae537?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1560958089-b8a1929cea89?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1609521263047-f8f205293f24?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1533473359331-0135ef1b58bf?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1606611013016-969c19ba27b5?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1549317661-bd32c8ce0db2?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1552519507-da3b142c6e3d?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1583121274602-3e2820c69888?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1568844293986-8d0400f4e5b1?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1502877338535-766e1452684a?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1541899481282-d53bffe3c35d?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1617531653332-bd46c24f2068?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1590362891991-f776e747a588?w=800&h=450&fit=crop",
                    "https://images.unsplash.com/photo-1619405399517-d7fce0f13302?w=800&h=450&fit=crop"
                };
                var rng = new Random();
                foreach (var car in brokenImgCars)
                {
                    car.ImageURL = fallbackImages[rng.Next(fallbackImages.Length)];
                }
                context.SaveChanges();
            }

            if (context.Cars.Count() > 50) return; // Already seeded enough cars
            var brands = context.CarBrands.ToList();
            var types = context.CarTypes.ToList();
            var statuses = context.CarStatuses.ToList();

            if (!locations.Any() || !brands.Any() || !types.Any() || !statuses.Any()) return;

            var random = new Random();
            var newCars = new List<Car>();

            var templates = new List<CarTemplate>
            {
                new CarTemplate("Vios G", "Toyota", "Sedan 5 chỗ", "Xăng", "Tự động", 800000, 100000, 5, false, "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800&h=450&fit=crop"),
                new CarTemplate("City RS", "Honda", "Sedan 5 chỗ", "Xăng", "Tự động", 900000, 110000, 5, false, "https://images.unsplash.com/photo-1619767886558-efdc259cde1a?w=800&h=450&fit=crop"),
                new CarTemplate("Accent MT", "Hyundai", "Sedan 5 chỗ", "Xăng", "Số sàn", 600000, 80000, 5, false, "https://images.unsplash.com/photo-1580273916550-e323be2ae537?w=800&h=450&fit=crop"),
                new CarTemplate("VF5 Plus", "VinFast", "Sedan 5 chỗ", "Điện", "Tự động", 750000, 90000, 5, false, "https://images.unsplash.com/photo-1560958089-b8a1929cea89?w=800&h=450&fit=crop"),
                new CarTemplate("CX-5 Signature", "Mazda", "SUV 5 chỗ", "Xăng", "Tự động", 1500000, 180000, 5, true, "https://images.unsplash.com/photo-1609521263047-f8f205293f24?w=800&h=450&fit=crop"),
                new CarTemplate("Everest Titanium", "Ford", "SUV 7 chỗ", "Dầu", "Tự động", 1800000, 200000, 7, true, "https://images.unsplash.com/photo-1533473359331-0135ef1b58bf?w=800&h=450&fit=crop"),
                new CarTemplate("Santa Fe Premium", "Hyundai", "SUV 7 chỗ", "Xăng", "Tự động", 1700000, 190000, 7, true, "https://images.unsplash.com/photo-1606611013016-969c19ba27b5?w=800&h=450&fit=crop"),
                new CarTemplate("VF8 Eco", "VinFast", "SUV 5 chỗ", "Điện", "Tự động", 1600000, 180000, 5, true, "https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=800&h=450&fit=crop"),
                new CarTemplate("Xpander AT", "Mitsubishi", "MPV 7 chỗ", "Xăng", "Tự động", 950000, 120000, 7, false, "https://images.unsplash.com/photo-1549317661-bd32c8ce0db2?w=800&h=450&fit=crop"),
                new CarTemplate("Carnival Signature", "Kia", "MPV 7 chỗ", "Dầu", "Tự động", 2200000, 250000, 7, true, "https://images.unsplash.com/photo-1619405399517-d7fce0f13302?w=800&h=450&fit=crop"),
                new CarTemplate("Innova Cross", "Toyota", "MPV 7 chỗ", "Xăng", "Tự động", 1200000, 150000, 7, false, "https://images.unsplash.com/photo-1552519507-da3b142c6e3d?w=800&h=450&fit=crop"),
                new CarTemplate("Ranger Raptor", "Ford", "Bán tải", "Dầu", "Tự động", 2500000, 300000, 5, true, "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?w=800&h=450&fit=crop"),
                new CarTemplate("Hilux Adventure", "Toyota", "Bán tải", "Dầu", "Số tự động", 1300000, 160000, 5, false, "https://images.unsplash.com/photo-1590362891991-f776e747a588?w=800&h=450&fit=crop"),
                new CarTemplate("Morning MT", "Kia", "Hatchback", "Xăng", "Số sàn", 450000, 60000, 5, false, "https://images.unsplash.com/photo-1502877338535-766e1452684a?w=800&h=450&fit=crop"),
                new CarTemplate("Grand i10", "Hyundai", "Hatchback", "Xăng", "Tự động", 550000, 70000, 5, false, "https://images.unsplash.com/photo-1541899481282-d53bffe3c35d?w=800&h=450&fit=crop"),
                new CarTemplate("VF9 Plus", "VinFast", "SUV 7 chỗ", "Điện", "Tự động", 2000000, 220000, 7, true, "https://images.unsplash.com/photo-1617531653332-bd46c24f2068?w=800&h=450&fit=crop"),
                new CarTemplate("Tucson 2.0", "Hyundai", "SUV 5 chỗ", "Xăng", "Tự động", 1400000, 170000, 5, true, "https://images.unsplash.com/photo-1583121274602-3e2820c69888?w=800&h=450&fit=crop"),
                new CarTemplate("CR-V L", "Honda", "SUV 5 chỗ", "Xăng", "Tự động", 1600000, 190000, 5, true, "https://images.unsplash.com/photo-1568844293986-8d0400f4e5b1?w=800&h=450&fit=crop")
            };
            var statusAvailable = statuses.FirstOrDefault(s => s.Name == "Available");
            if (statusAvailable == null) return;
            
            var locationOffsetsList = GetLocationOffsets();
            foreach (var loc in locations)
            {
                if (!locationOffsetsList.ContainsKey(loc.Name)) continue;

                var baseCoords = locationOffsetsList[loc.Name];
                loc.Latitude = baseCoords.lat;
                loc.Longitude = baseCoords.lng;

                int carsToSeed = random.Next(10, 13);
                for (int i = 0; i < carsToSeed; i++)
                {
                    var tmpl = templates[random.Next(templates.Count)];
                    var bId = brands.FirstOrDefault(b => b.Name == tmpl.BrandName)?.BrandID ?? brands.First().BrandID;
                    var tId = types.FirstOrDefault(t => t.Name == tmpl.TypeName)?.CarTypeID ?? types.First().CarTypeID;

                    // Random coordinate offset to spread out within city (~5km radius)
                    double latOffset = (random.NextDouble() - 0.5) * 0.08; 
                    double lngOffset = (random.NextDouble() - 0.5) * 0.08;

                    string plateNum = $"{(random.Next(11, 99))}{((char)random.Next('A', 'Z'))}-{random.Next(10000, 99999)}";

                    var car = new Car
                    {
                        CarName = tmpl.Name,
                        BrandID = bId,
                        Seats = tmpl.Seats,
                        LicensePlate = plateNum,
                        VIN = $"VIN{Guid.NewGuid().ToString().Substring(0,8).ToUpper()}",
                        PricePerDay = tmpl.PricePerDay,
                        PricePerHour = tmpl.PricePerHour,
                        CarStatusID = statusAvailable.CarStatusID,
                        Description = $"Xe {tmpl.Name} tại {loc.Name}, đời mới, sạch sẽ.",
                        CurrentOdometer = random.Next(1000, 50000),
                        CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 100)),
                        CarTypeID = tId,
                        FuelType = tmpl.FuelType,
                        Transmission = tmpl.Transmission,
                        LocationID = loc.LocationID,
                        IsPremium = tmpl.IsPremium,
                        ImageURL = tmpl.ImageURL,
                        Latitude = baseCoords.lat + latOffset,
                        Longitude = baseCoords.lng + lngOffset,
                        CarAddress = $"Bãi đỗ xe {((char)random.Next('A', 'Z'))}{random.Next(1, 99)}, {loc.Name}"
                    };
                    
                    if (random.Next(100) < 20) {
                        car.DiscountPrice = car.PricePerDay * 0.8m;
                    }

                    newCars.Add(car);
                }
            }

            context.Cars.AddRange(newCars);
            context.SaveChanges();
        }

        private static Dictionary<string, (double lat, double lng)> GetLocationOffsets()
        {
            return new Dictionary<string, (double lat, double lng)>
            {
                { "TP. Hồ Chí Minh", (10.7769, 106.7009) },
                { "Hà Nội", (21.0285, 105.8542) },
                { "Đà Nẵng", (16.0544, 108.2022) },
                { "Cần Thơ", (10.0452, 105.7469) },
                { "Bình Dương", (10.9794, 106.6516) },
                { "Đồng Nai", (10.9465, 106.8209) },
                { "Nha Trang", (12.2388, 109.1967) },
                { "Hải Phòng", (20.8449, 106.6881) },
                { "Bà Rịa - Vũng Tàu", (10.4938, 107.1683) },
                { "Cà Mau", (9.1769, 105.1524) },
                { "Lâm Đồng", (11.9404, 108.4583) },
                { "Phú Quốc", (10.2289, 103.9573) }
            };
        }

        private class CarTemplate
        {
            public string Name { get; }
            public string BrandName { get; }
            public string TypeName { get; }
            public string FuelType { get; }
            public string Transmission { get; }
            public decimal PricePerDay { get; }
            public decimal PricePerHour { get; }
            public int Seats { get; }
            public bool IsPremium { get; }
            public string ImageURL { get; }

            public CarTemplate(string name, string brandName, string typeName, string fuelType, string transmission, decimal pricePerDay, decimal pricePerHour, int seats, bool isPremium, string imageURL)
            {
                Name = name;
                BrandName = brandName;
                TypeName = typeName;
                FuelType = fuelType;
                Transmission = transmission;
                PricePerDay = pricePerDay;
                PricePerHour = pricePerHour;
                Seats = seats;
                IsPremium = isPremium;
                ImageURL = imageURL;
            }
        }
    }
}
