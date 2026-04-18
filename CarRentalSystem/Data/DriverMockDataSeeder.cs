using BCrypt.Net;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Data
{
    public static class DriverMockDataSeeder
    {
        private const string SeedBookingPrefix = "[SEED-DRIVER]";
        private const string SeedCustomerDomain = "@seed.customer.local";

        public static void Seed(ApplicationDbContext context)
        {
            if (!context.Cars.Any() || !context.BookingStatuses.Any() || !context.Locations.Any())
            {
                return;
            }

            EnsureSeedCustomers(context);

            var drivers = EnsureDrivers(context);
            EnsureDriverHistory(context, drivers);
        }

        private static void EnsureSeedCustomers(ApplicationDbContext context)
        {
            var customerSpecs = new[]
            {
                new { Name = "Seed Customer Lan", Email = "lan" + SeedCustomerDomain, Phone = "0910001001" },
                new { Name = "Seed Customer Minh", Email = "minh" + SeedCustomerDomain, Phone = "0910001002" },
                new { Name = "Seed Customer Phuc", Email = "phuc" + SeedCustomerDomain, Phone = "0910001003" }
            };

            foreach (var spec in customerSpecs)
            {
                if (context.Users.Any(u => u.Email == spec.Email))
                {
                    continue;
                }

                context.Users.Add(new User
                {
                    Name = spec.Name,
                    Email = spec.Email,
                    Phone = spec.Phone,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                    Role = "Customer",
                    IsEmailVerified = true,
                    IsVerified = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-2)
                });
            }

            context.SaveChanges();
        }

        private static Dictionary<string, Driver> EnsureDrivers(ApplicationDbContext context)
        {
            var locations = context.Locations.OrderBy(l => l.LocationID).ToList();
            if (locations.Count == 0)
            {
                return new Dictionary<string, Driver>();
            }

            var specs = new[]
            {
                new Driver
                {
                    FullName = "Nguyen Van Tai",
                    Phone = "0933000001",
                    Email = "tai.driver@seed.local",
                    LicenseNumber = "B2-000111",
                    LicenseType = "B2",
                    ExperienceYears = 8,
                    DailyRate = 900000m,
                    HourlyRate = 120000m,
                    IsAvailable = true,
                    IsApproved = true,
                    Rating = 4.9m,
                    TotalTrips = 182,
                    Description = "Tai xe premium cho sedan va SUV 5 cho, rating cao va ty le hoan thanh tot.",
                    LocationID = locations[0].LocationID,
                    CreatedAt = DateTime.UtcNow.AddMonths(-18)
                },
                new Driver
                {
                    FullName = "Tran Minh Khang",
                    Phone = "0933000002",
                    Email = "khang.driver@seed.local",
                    LicenseNumber = "B2-000112",
                    LicenseType = "B2",
                    ExperienceYears = 5,
                    DailyRate = 780000m,
                    HourlyRate = 100000m,
                    IsAvailable = false,
                    IsApproved = true,
                    Rating = 4.6m,
                    TotalTrips = 97,
                    Description = "Tai xe dang co chuyen active, phu hop test truong hop khong the match.",
                    LocationID = locations[Math.Min(1, locations.Count - 1)].LocationID,
                    CreatedAt = DateTime.UtcNow.AddMonths(-12)
                },
                new Driver
                {
                    FullName = "Le Quoc Bao",
                    Phone = "0933000003",
                    Email = "bao.driver@seed.local",
                    LicenseNumber = "C-000210",
                    LicenseType = "C",
                    ExperienceYears = 6,
                    DailyRate = 860000m,
                    HourlyRate = 115000m,
                    IsAvailable = true,
                    IsApproved = true,
                    Rating = 4.3m,
                    TotalTrips = 54,
                    Description = "Tai xe cho xe 7 cho va xe cong tac, co lich su completed va cancelled.",
                    LocationID = locations[Math.Min(2, locations.Count - 1)].LocationID,
                    CreatedAt = DateTime.UtcNow.AddMonths(-9)
                },
                new Driver
                {
                    FullName = "Pham Gia Huy",
                    Phone = "0933000004",
                    Email = "huy.driver@seed.local",
                    LicenseNumber = "D-000310",
                    LicenseType = "D",
                    ExperienceYears = 12,
                    DailyRate = 1200000m,
                    HourlyRate = 180000m,
                    IsAvailable = true,
                    IsApproved = true,
                    Rating = 4.8m,
                    TotalTrips = 211,
                    Description = "Tai xe hang cao, kinh nghiem nhieu, phu hop test scoring theo chat luong va gia.",
                    LocationID = locations[Math.Min(3, locations.Count - 1)].LocationID,
                    CreatedAt = DateTime.UtcNow.AddMonths(-24)
                },
                new Driver
                {
                    FullName = "Doan Thanh Nam",
                    Phone = "0933000005",
                    Email = "nam.driver@seed.local",
                    LicenseNumber = "B2-000113",
                    LicenseType = "B2",
                    ExperienceYears = 2,
                    DailyRate = 650000m,
                    HourlyRate = 85000m,
                    IsAvailable = true,
                    IsApproved = false,
                    Rating = 4.0m,
                    TotalTrips = 8,
                    Description = "Tai xe moi onboard, chua duoc duyet, dung de test filter IsApproved.",
                    LocationID = locations[0].LocationID,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                },
                new Driver
                {
                    FullName = "Bui Anh Tuan",
                    Phone = "0933000006",
                    Email = "tuan.driver@seed.local",
                    LicenseNumber = "C-000211",
                    LicenseType = "C",
                    ExperienceYears = 4,
                    DailyRate = 720000m,
                    HourlyRate = 95000m,
                    IsAvailable = false,
                    IsApproved = true,
                    Rating = 3.8m,
                    TotalTrips = 39,
                    Description = "Tai xe tam tat de mo phong time-off hoac tam dung nhan chuyen.",
                    LocationID = locations[Math.Min(1, locations.Count - 1)].LocationID,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6)
                },
                new Driver
                {
                    FullName = "Hoang Thu Duc",
                    Phone = "0933000007",
                    Email = "duc.driver@seed.local",
                    LicenseNumber = "B2-000114",
                    LicenseType = "B2",
                    ExperienceYears = 3,
                    DailyRate = 680000m,
                    HourlyRate = 90000m,
                    IsAvailable = true,
                    IsApproved = true,
                    Rating = 4.1m,
                    TotalTrips = 26,
                    Description = "Tai xe gia tot, rating trung binh, phu hop test trade-off ETA va chi phi.",
                    LocationID = locations[0].LocationID,
                    CreatedAt = DateTime.UtcNow.AddMonths(-5)
                }
            };

            foreach (var driver in specs)
            {
                var existing = context.Drivers.FirstOrDefault(d => d.Email == driver.Email);
                if (existing != null)
                {
                    existing.FullName = driver.FullName;
                    existing.Phone = driver.Phone;
                    existing.LicenseNumber = driver.LicenseNumber;
                    existing.LicenseType = driver.LicenseType;
                    existing.ExperienceYears = driver.ExperienceYears;
                    existing.DailyRate = driver.DailyRate;
                    existing.HourlyRate = driver.HourlyRate;
                    existing.IsAvailable = driver.IsAvailable;
                    existing.IsApproved = driver.IsApproved;
                    existing.Rating = driver.Rating;
                    existing.TotalTrips = driver.TotalTrips;
                    existing.Description = driver.Description;
                    existing.LocationID = driver.LocationID;
                    context.Drivers.Update(existing);
                    continue;
                }

                context.Drivers.Add(driver);
            }

            context.SaveChanges();

            return context.Drivers
                .Where(d => d.Email != null && d.Email.EndsWith("@seed.local"))
                .ToDictionary(d => d.Email!, d => d);
        }

        private static void EnsureDriverHistory(ApplicationDbContext context, Dictionary<string, Driver> drivers)
        {
            if (drivers.Count == 0)
            {
                return;
            }

            var customers = context.Users
                .Where(u => u.Email.EndsWith(SeedCustomerDomain))
                .OrderBy(u => u.UserID)
                .ToList();
            var cars = context.Cars
                .OrderBy(c => c.CarID)
                .ToList();
            var statuses = context.BookingStatuses
                .ToDictionary(s => s.Name, s => s.BookingStatusID, StringComparer.OrdinalIgnoreCase);

            if (customers.Count < 3 || cars.Count < 6)
            {
                return;
            }

            var scenarios = new[]
            {
                new
                {
                    Code = "01",
                    DriverEmail = "tai.driver@seed.local",
                    CustomerIndex = 0,
                    CarIndex = 0,
                    StartAt = DateTime.UtcNow.AddDays(-18).AddHours(8),
                    EndAt = DateTime.UtcNow.AddDays(-18).AddHours(14),
                    BookingStatus = "Completed",
                    DriverBookingStatus = "Completed",
                    DriverFee = 720000m,
                    Note = "completed sedan trip with premium rating"
                },
                new
                {
                    Code = "02",
                    DriverEmail = "tai.driver@seed.local",
                    CustomerIndex = 1,
                    CarIndex = 4,
                    StartAt = DateTime.UtcNow.AddDays(-7).AddHours(7),
                    EndAt = DateTime.UtcNow.AddDays(-5).AddHours(19),
                    BookingStatus = "Completed",
                    DriverBookingStatus = "Completed",
                    DriverFee = 1800000m,
                    Note = "multi day suv trip"
                },
                new
                {
                    Code = "03",
                    DriverEmail = "bao.driver@seed.local",
                    CustomerIndex = 2,
                    CarIndex = 7,
                    StartAt = DateTime.UtcNow.AddDays(-10).AddHours(9),
                    EndAt = DateTime.UtcNow.AddDays(-9).AddHours(18),
                    BookingStatus = "Cancelled",
                    DriverBookingStatus = "Cancelled",
                    DriverFee = 860000m,
                    Note = "cancelled seven seat scenario"
                },
                new
                {
                    Code = "04",
                    DriverEmail = "khang.driver@seed.local",
                    CustomerIndex = 0,
                    CarIndex = 5,
                    StartAt = DateTime.UtcNow.AddHours(-2),
                    EndAt = DateTime.UtcNow.AddHours(6),
                    BookingStatus = "Active",
                    DriverBookingStatus = "Active",
                    DriverFee = 800000m,
                    Note = "active booking keeps driver unavailable"
                },
                new
                {
                    Code = "05",
                    DriverEmail = "duc.driver@seed.local",
                    CustomerIndex = 1,
                    CarIndex = 9,
                    StartAt = DateTime.UtcNow.AddDays(2).Date.AddHours(6),
                    EndAt = DateTime.UtcNow.AddDays(2).Date.AddHours(12),
                    BookingStatus = "Pending",
                    DriverBookingStatus = "Pending",
                    DriverFee = 540000m,
                    Note = "future pending economical driver case"
                },
                new
                {
                    Code = "06",
                    DriverEmail = "huy.driver@seed.local",
                    CustomerIndex = 2,
                    CarIndex = 10,
                    StartAt = DateTime.UtcNow.AddDays(4).Date.AddHours(8),
                    EndAt = DateTime.UtcNow.AddDays(7).Date.AddHours(18),
                    BookingStatus = "Confirmed",
                    DriverBookingStatus = "Confirmed",
                    DriverFee = 3600000m,
                    Note = "future high-end long distance assignment"
                }
            };

            foreach (var scenario in scenarios)
            {
                var adminNote = $"{SeedBookingPrefix}-{scenario.Code}: {scenario.Note}";
                var existingBooking = context.Bookings
                    .Include(b => b.Car)
                    .FirstOrDefault(b => b.AdminNotes == adminNote);

                if (existingBooking == null)
                {
                    var car = cars[scenario.CarIndex];
                    var durationHours = Math.Max(1, (int)Math.Ceiling((scenario.EndAt - scenario.StartAt).TotalHours));
                    var totalPrice = durationHours <= 24
                        ? car.PricePerHour * durationHours
                        : car.PricePerDay * Math.Max(1, (int)Math.Ceiling((scenario.EndAt - scenario.StartAt).TotalDays));

                    existingBooking = new Booking
                    {
                        UserID = customers[scenario.CustomerIndex].UserID,
                        CarID = car.CarID,
                        StartDate = scenario.StartAt,
                        EndDate = scenario.EndAt,
                        PickupLocationID = car.LocationID,
                        ReturnLocationID = car.LocationID,
                        TotalPrice = totalPrice + scenario.DriverFee,
                        TotalHours = durationHours,
                        BookingStatusID = statuses[scenario.BookingStatus],
                        InsuranceTier = car.InsuranceTier,
                        DepositAmount = car.DefaultDeposit,
                        DepositStatus = scenario.BookingStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ? "Refunded" : "Holding",
                        ActualPickupDate = scenario.BookingStatus.Equals("Active", StringComparison.OrdinalIgnoreCase) || scenario.BookingStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                            ? scenario.StartAt.AddMinutes(10)
                            : null,
                        ActualReturnDate = scenario.BookingStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                            ? scenario.EndAt.AddMinutes(20)
                            : null,
                        AdminNotes = adminNote,
                        CreatedAt = scenario.StartAt.AddDays(-2)
                    };

                    context.Bookings.Add(existingBooking);
                    context.SaveChanges();
                }

                var existingDriverBooking = context.DriverBookings
                    .FirstOrDefault(db => db.BookingID == existingBooking.BookingID);

                if (existingDriverBooking != null)
                {
                    continue;
                }

                var driver = drivers[scenario.DriverEmail];
                var driverBooking = new DriverBooking
                {
                    BookingID = existingBooking.BookingID,
                    DriverID = driver.DriverID,
                    DriverFee = scenario.DriverFee,
                    Status = scenario.DriverBookingStatus,
                    CreatedAt = existingBooking.CreatedAt
                };

                context.DriverBookings.Add(driverBooking);
                context.SaveChanges();

                existingBooking.DriverBookingID = driverBooking.DriverBookingID;
                context.Bookings.Update(existingBooking);
                context.SaveChanges();
            }
        }
    }
}
