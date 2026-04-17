using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = UserRoleConstants.Admin)]
    public class SeedController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SeedController(ApplicationDbContext context) { _context = context; }

        [HttpGet]
        public async Task<IActionResult> SeedCars()
        {
            if (await _context.Cars.CountAsync() >= 50)
                return Content("DB already has 50+ cars. Skipping seed.");

            var locationData = new[]
            {
                new { Name = "TP. Ho Chi Minh", Address = "268 Ly Thuong Kiet, Q.10, TP.HCM", Lat = 10.7769, Lng = 106.6580 },
                new { Name = "Ha Noi", Address = "1 Trang Tien, Hoan Kiem, Ha Noi", Lat = 21.0285, Lng = 105.8542 },
                new { Name = "Da Nang", Address = "2 Bach Dang, Hai Chau, Da Nang", Lat = 16.0544, Lng = 108.2022 },
                new { Name = "Nha Trang", Address = "46 Tran Phu, Loc Tho, Nha Trang", Lat = 12.2388, Lng = 109.1967 },
                new { Name = "Da Lat", Address = "12 Tran Phu, P.3, Da Lat", Lat = 11.9404, Lng = 108.4583 },
                new { Name = "Can Tho", Address = "91B Nguyen Van Linh, Ninh Kieu, Can Tho", Lat = 10.0452, Lng = 105.7469 },
            };

            var locations = new Dictionary<string, Location>();
            foreach (var ld in locationData)
            {
                var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Name == ld.Name);
                if (loc == null)
                {
                    loc = new Location { Name = ld.Name, Address = ld.Address, Latitude = ld.Lat, Longitude = ld.Lng };
                    _context.Locations.Add(loc);
                    await _context.SaveChangesAsync();
                }
                locations[ld.Name] = loc;
            }

            var brandNames = new[] { "Toyota", "Honda", "Hyundai", "Kia", "Mazda", "Ford", "VinFast", "Mitsubishi", "Mercedes", "BMW" };
            var brands = new Dictionary<string, CarBrand>();
            foreach (var bn in brandNames)
            {
                var b = await _context.CarBrands.FirstOrDefaultAsync(x => x.Name == bn);
                if (b == null)
                {
                    b = new CarBrand { Name = bn };
                    _context.CarBrands.Add(b);
                    await _context.SaveChangesAsync();
                }
                brands[bn] = b;
            }

            var typeNames = new[] { "Sedan", "SUV", "Hatchback", "MPV", "Pickup", "Crossover" };
            var carTypes = new Dictionary<string, CarType>();
            foreach (var tn in typeNames)
            {
                var ct = await _context.CarTypes.FirstOrDefaultAsync(x => x.Name == tn);
                if (ct == null)
                {
                    ct = new CarType { Name = tn };
                    _context.CarTypes.Add(ct);
                    await _context.SaveChangesAsync();
                }
                carTypes[tn] = ct;
            }

            var availableStatus = await _context.CarStatuses.FirstOrDefaultAsync(s => s.Name == "Available");
            if (availableStatus == null)
            {
                availableStatus = new CarStatus { Name = "Available" };
                _context.CarStatuses.Add(availableStatus);
                await _context.SaveChangesAsync();
            }

            var rng = new Random(42);

            var carTemplates = new (string Brand, string Model, string Type, int Seats, decimal PDay, decimal PHour, string Fuel, string Trans, string Cons, string Features, bool Premium)[]
            {
                ("Toyota", "Vios", "Sedan", 5, 640000m, 90000m, "Xang", "Tu dong", "6.5L/100km", "Bluetooth,Camera lui,Cam bien", false),
                ("Toyota", "Camry", "Sedan", 5, 1200000m, 160000m, "Xang", "Tu dong", "8L/100km", "Bluetooth,Camera 360,Ghe da", true),
                ("Toyota", "Innova Cross", "MPV", 7, 1100000m, 150000m, "Xang", "Tu dong", "8.5L/100km", "Bluetooth,Camera lui,Ghe da", false),
                ("Toyota", "Fortuner", "SUV", 7, 1300000m, 170000m, "Dau", "Tu dong", "8.8L/100km", "Bluetooth,Camera 360,4WD,Ghe da", true),
                ("Toyota", "Veloz Cross", "MPV", 7, 850000m, 120000m, "Xang", "Tu dong", "7L/100km", "Bluetooth,Camera lui", false),
                ("Honda", "City", "Sedan", 5, 700000m, 95000m, "Xang", "Tu dong", "6L/100km", "Bluetooth,Camera lui,Honda Sensing", false),
                ("Honda", "Civic", "Sedan", 5, 1050000m, 140000m, "Xang", "Tu dong", "7L/100km", "Bluetooth,Camera lui,Ghe da", true),
                ("Honda", "CR-V", "SUV", 7, 1200000m, 160000m, "Xang", "Tu dong", "8.5L/100km", "Bluetooth,Camera 360,Ghe da", true),
                ("Honda", "HR-V", "Crossover", 5, 900000m, 120000m, "Xang", "Tu dong", "7L/100km", "Bluetooth,Camera lui", false),
                ("Hyundai", "Accent", "Sedan", 5, 600000m, 85000m, "Xang", "Tu dong", "6L/100km", "Bluetooth,Camera lui", false),
                ("Hyundai", "Tucson", "SUV", 5, 1100000m, 150000m, "Xang", "Tu dong", "8L/100km", "Bluetooth,Camera 360,Ghe da", true),
                ("Hyundai", "Santa Fe", "SUV", 7, 1400000m, 180000m, "Dau", "Tu dong", "9L/100km", "Bluetooth,Camera 360,4WD,Ghe da", true),
                ("Hyundai", "Stargazer", "MPV", 7, 750000m, 100000m, "Xang", "Tu dong", "7.5L/100km", "Bluetooth,Camera lui", false),
                ("Kia", "Morning", "Hatchback", 4, 450000m, 65000m, "Xang", "Tu dong", "5L/100km", "Bluetooth", false),
                ("Kia", "Seltos", "Crossover", 5, 850000m, 115000m, "Xang", "Tu dong", "7L/100km", "Bluetooth,Camera lui,Ghe da", false),
                ("Kia", "Sorento", "SUV", 7, 1350000m, 175000m, "Dau", "Tu dong", "8.5L/100km", "Bluetooth,Camera 360,4WD,Ghe da", true),
                ("Kia", "Carnival", "MPV", 7, 1500000m, 200000m, "Dau", "Tu dong", "9L/100km", "Bluetooth,Camera 360,Ghe da", true),
                ("Mazda", "Mazda3", "Sedan", 5, 850000m, 115000m, "Xang", "Tu dong", "6.5L/100km", "Bluetooth,Camera lui,Ghe da,Bose", false),
                ("Mazda", "CX-5", "SUV", 5, 1050000m, 140000m, "Xang", "Tu dong", "7.5L/100km", "Bluetooth,Camera 360,Ghe da,Bose", true),
                ("Mazda", "CX-8", "SUV", 7, 1250000m, 165000m, "Dau", "Tu dong", "8L/100km", "Bluetooth,Camera 360,Ghe da,Bose", true),
                ("Ford", "Ranger", "Pickup", 5, 1000000m, 130000m, "Dau", "Tu dong", "9L/100km", "Bluetooth,Camera lui,4WD", false),
                ("Ford", "Everest", "SUV", 7, 1350000m, 175000m, "Dau", "Tu dong", "9L/100km", "Bluetooth,Camera 360,4WD,Ghe da", true),
                ("Ford", "Territory", "Crossover", 5, 900000m, 120000m, "Xang", "Tu dong", "7.5L/100km", "Bluetooth,Camera 360,Ghe da", false),
                ("VinFast", "VF 5", "Crossover", 5, 650000m, 90000m, "Dien", "Tu dong", "15kWh/100km", "Bluetooth,Camera lui", false),
                ("VinFast", "VF 8", "SUV", 5, 1200000m, 160000m, "Dien", "Tu dong", "18kWh/100km", "Bluetooth,Camera 360,ADAS,Ghe da", true),
                ("VinFast", "VF e34", "Crossover", 5, 800000m, 110000m, "Dien", "Tu dong", "16kWh/100km", "Bluetooth,Camera lui,ADAS", false),
                ("Mitsubishi", "Xpander", "MPV", 7, 750000m, 100000m, "Xang", "Tu dong", "7L/100km", "Bluetooth,Camera lui", false),
                ("Mitsubishi", "Pajero Sport", "SUV", 7, 1300000m, 170000m, "Dau", "Tu dong", "9L/100km", "Bluetooth,Camera 360,4WD,Ghe da", true),
                ("Mercedes", "C200", "Sedan", 5, 2500000m, 320000m, "Xang", "Tu dong", "7L/100km", "Bluetooth,Camera 360,Ghe da,MBUX", true),
                ("BMW", "320i", "Sedan", 5, 2800000m, 350000m, "Xang", "Tu dong", "7.5L/100km", "Bluetooth,Camera 360,Ghe da,iDrive", true),
            };

            var gpsData = new Dictionary<string, (double bLat, double bLng, double sp)>
            {
                { "TP. Ho Chi Minh", (10.7769, 106.6580, 0.04) },
                { "Ha Noi", (21.0285, 105.8342, 0.03) },
                { "Da Nang", (16.0544, 108.2022, 0.025) },
                { "Nha Trang", (12.2388, 109.1967, 0.02) },
                { "Da Lat", (11.9404, 108.4583, 0.015) },
                { "Can Tho", (10.0452, 105.7469, 0.02) },
            };

            var plateAreas = new Dictionary<string, string[]>
            {
                { "TP. Ho Chi Minh", new[]{ "51A", "51B", "51C", "51D", "51F" } },
                { "Ha Noi", new[]{ "29A", "29B", "30A", "30B" } },
                { "Da Nang", new[]{ "43A", "43B", "43C" } },
                { "Nha Trang", new[]{ "79A", "79B" } },
                { "Da Lat", new[]{ "49A", "49B" } },
                { "Can Tho", new[]{ "65A", "65B" } },
            };

            int carIdx = 0;
            int totalSeeded = 0;
            foreach (var loc in locations)
            {
                var gps = gpsData[loc.Key];
                var plates = plateAreas[loc.Key];
                int count = (loc.Key == "TP. Ho Chi Minh" || loc.Key == "Ha Noi") ? 10 : 9;

                for (int i = 0; i < count; i++)
                {
                    var t = carTemplates[(carIdx + i) % carTemplates.Length];
                    string plate = $"{plates[rng.Next(plates.Length)]}-{rng.Next(100, 999)}.{rng.Next(10, 99)}";
                    string vin = $"VN{DateTime.Now.Year % 100}{carIdx + i:D3}{rng.Next(10000, 99999)}";
                    double lat = Math.Round(gps.bLat + (rng.NextDouble() * 2 - 1) * gps.sp, 6);
                    double lng = Math.Round(gps.bLng + (rng.NextDouble() * 2 - 1) * gps.sp, 6);
                    bool isEV = t.Fuel == "Dien";

                    _context.Cars.Add(new Car
                    {
                        CarName = $"{t.Brand} {t.Model}",
                        BrandID = brands[t.Brand].BrandID,
                        CarTypeID = carTypes[t.Type].CarTypeID,
                        Seats = t.Seats,
                        LicensePlate = plate,
                        VIN = vin,
                        PricePerDay = t.PDay,
                        PricePerHour = t.PHour,
                        CarStatusID = availableStatus.CarStatusID,
                        FuelType = t.Fuel,
                        Transmission = t.Trans,
                        FuelConsumption = t.Cons,
                        Features = t.Features,
                        LocationID = loc.Value.LocationID,
                        CarAddress = $"Khu vuc {loc.Key}",
                        Latitude = lat,
                        Longitude = lng,
                        IsPremium = t.Premium,
                        InsuranceTier = t.Premium ? "Premium" : "Basic",
                        IsElectric = isEV,
                        BatteryCapacity = isEV ? 42 + rng.Next(0, 40) : null,
                        RangePerCharge = isEV ? 250 + rng.Next(0, 150) : null,
                        CurrentOdometer = rng.Next(5000, 80000),
                        Description = $"Xe {t.Brand} {t.Model} {t.Seats} cho, {t.Fuel}, {t.Trans}. Khu vuc {loc.Key}.",
                        CreatedAt = DateTime.Now.AddDays(-rng.Next(30, 365)),
                    });
                    totalSeeded++;
                }
                carIdx += count;
            }

            await _context.SaveChangesAsync();
            return Content($"SUCCESS: Seeded {totalSeeded} cars across {locations.Count} locations!");
        }
    }
}
