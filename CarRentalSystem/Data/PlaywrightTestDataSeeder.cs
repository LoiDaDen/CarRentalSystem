using BCrypt.Net;
using CarRentalSystem.Helpers;
using CarRentalSystem.Models;
using CarRentalSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CarRentalSystem.Data
{
    public static class PlaywrightTestDataSeeder
    {
        private const string DefaultPlaywrightPasswordPrefix = "Playwright";
        private const int DefaultPlaywrightPasswordSuffix = 123;

        public static string TestLoginPassword =>
            Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_LOGIN_PASSWORD")
            ?? $"{DefaultPlaywrightPasswordPrefix}@{DefaultPlaywrightPasswordSuffix}";

        public static async Task<PlaywrightSeedSummary> SeedAsync(ApplicationDbContext db)
        {
            DbSeeder.Seed(db);
            await BusinessReferenceDataSeeder.SeedAsync(db);
            await OwnerCarReferenceDataSeeder.SeedAsync(db);
            await SeedMinimumCatalogAsync(db);

            var users = await SeedUsersAsync(db);
            await SeedLegalDocumentsAsync(db);
            await SeedVerifiedLicenseAsync(db, users.CustomerVerified.UserID);
            var area = await SeedAreaAsync(db, users.Admin, users.Manager, users.Staff, users.Viewer);
            var cars = await SeedCarsAsync(db, users.Owner.UserID);
            await EnsureSandboxAmenityAsync(db, cars.FirstAvailableCar.CarID);
            var booking = await SeedOperationalRecordsAsync(db, users, cars.FirstAvailableCar, area);
            var paymentId = await db.Payments
                .Where(p => p.BookingID == booking.BookingID)
                .Select(p => p.PaymentID)
                .FirstAsync();
            var privacyRequestId = await db.PrivacyRequests
                .Where(r => r.UserID == users.CustomerVerified.UserID)
                .OrderByDescending(r => r.PrivacyRequestID)
                .Select(r => r.PrivacyRequestID)
                .FirstAsync();
            var contractTypeId = await db.ContractTypes
                .Where(c => c.IsActive)
                .OrderBy(c => c.ContractTypeID)
                .Select(c => c.ContractTypeID)
                .FirstAsync();

            return new PlaywrightSeedSummary
            {
                ResetAtUtc = DateTime.UtcNow,
                Users = new Dictionary<string, PlaywrightUserCredential>
                {
                    ["admin"] = Credential(users.Admin),
                    ["manager"] = Credential(users.Manager),
                    ["staff"] = Credential(users.Staff),
                    ["viewer"] = Credential(users.Viewer),
                    ["customer"] = Credential(users.CustomerVerified),
                    ["customerUnverified"] = Credential(users.CustomerUnverified),
                    ["partner"] = Credential(users.Owner),
                    ["owner"] = Credential(users.Owner)
                },
                FirstAvailableCarId = cars.FirstAvailableCar.CarID,
                PendingOwnerCarId = cars.PendingOwnerCar.CarID,
                BookingId = booking.BookingID,
                PaymentId = paymentId,
                PrivacyRequestId = privacyRequestId,
                AreaId = area.AreaID,
                ContractTypeId = contractTypeId
            };
        }

        public static async Task<PlaywrightScenarioSummary> SeedScenarioAsync(ApplicationDbContext db, string? scenarioName)
        {
            if (!await db.Users.AnyAsync(u => u.Email == "customer@playwright.local"))
                await SeedAsync(db);

            var normalizedScenario = string.IsNullOrWhiteSpace(scenarioName)
                ? "delivery-tracking"
                : scenarioName.Trim().ToLowerInvariant();

            var users = await LoadUsersAsync(db);
            var area = await db.Areas.FirstAsync(a => a.Code == "CYP-HCM");
            var car = await db.Cars
                .Where(c => c.IsApproved && !c.IsDeleted)
                .OrderBy(c => c.CarID)
                .FirstAsync();

            if (normalizedScenario is "ops-copilot" or "ai-ops-copilot")
            {
                var opsScenario = await EnsureOpsCopilotScenarioAsync(db, users, area);
                var opsAmenity = await EnsureSandboxAmenityAsync(db, opsScenario.Car.CarID);

                return new PlaywrightScenarioSummary
                {
                    ScenarioName = normalizedScenario,
                    CarId = opsScenario.Car.CarID,
                    PendingOwnerCarId = await db.Cars
                        .Where(c => c.LicensePlate == "CYP-00001")
                        .Select(c => c.CarID)
                        .FirstOrDefaultAsync(),
                    BookingId = opsScenario.Booking.BookingID,
                    PaymentId = opsScenario.Payment.PaymentID,
                    OrderCode = opsScenario.OrderCode,
                    AreaId = area.AreaID,
                    AdminUserId = users.Admin.UserID,
                    StaffUserId = users.Staff.UserID,
                    CustomerUserId = users.CustomerVerified.UserID,
                    OwnerUserId = users.Owner.UserID,
                    AmenityId = opsAmenity.CarAmenityID,
                    AmenityName = opsAmenity.Name,
                    CarLatitude = opsScenario.Car.Latitude,
                    CarLongitude = opsScenario.Car.Longitude,
                    DeliveryLatitude = opsScenario.Booking.DeliveryLatitude,
                    DeliveryLongitude = opsScenario.Booking.DeliveryLongitude,
                    DeliveryFee = opsScenario.Booking.DeliveryFee,
                    DeliveryDistanceKm = opsScenario.Booking.DeliveryDistanceKm,
                    CameraGateEventId = opsScenario.CameraGateEventId,
                    MismatchedCameraGateEventId = opsScenario.MismatchedCameraGateEventId
                };
            }

            if (normalizedScenario is "payos-pending-hold" or "payment-pending-hold")
            {
                await ConfigureDeliveryCarAsync(db, car, users.Owner.UserID);
                var payosHold = await EnsurePendingPayOSHoldAsync(db, users, car);
                var payosAmenity = await EnsureSandboxAmenityAsync(db, car.CarID);

                return new PlaywrightScenarioSummary
                {
                    ScenarioName = normalizedScenario,
                    CarId = car.CarID,
                    PendingOwnerCarId = await db.Cars
                        .Where(c => c.LicensePlate == "CYP-00001")
                        .Select(c => c.CarID)
                        .FirstOrDefaultAsync(),
                    OrderCode = payosHold.OrderCode,
                    AreaId = area.AreaID,
                    AdminUserId = users.Admin.UserID,
                    StaffUserId = users.Staff.UserID,
                    CustomerUserId = users.CustomerVerified.UserID,
                    OwnerUserId = users.Owner.UserID,
                    AmenityId = payosAmenity.CarAmenityID,
                    AmenityName = payosAmenity.Name,
                    CarLatitude = car.Latitude,
                    CarLongitude = car.Longitude,
                    BookingHoldId = payosHold.BookingHoldID,
                    CheckoutUrl = payosHold.CheckoutUrl
                };
            }

            await ConfigureDeliveryCarAsync(db, car, users.Owner.UserID);
            var booking = await EnsureDeliveryTrackingBookingAsync(db, users, car);
            var payment = await db.Payments
                .Where(p => p.BookingID == booking.BookingID)
                .OrderByDescending(p => p.PaymentID)
                .FirstOrDefaultAsync();
            var payosTransaction = await db.PayOSTransactions
                .Where(t => t.BookingID == booking.BookingID || t.PaymentID == payment!.PaymentID)
                .OrderByDescending(t => t.PayOSTransactionID)
                .FirstOrDefaultAsync();
            var amenity = await EnsureSandboxAmenityAsync(db, car.CarID);

            var existingTrackings = await db.VehicleTrackings
                .Where(t => t.CarID == car.CarID)
                .ToListAsync();
            db.VehicleTrackings.RemoveRange(existingTrackings);

            if (normalizedScenario is "tracking-stale" or "stale-tracking")
            {
                db.VehicleTrackings.Add(new VehicleTracking
                {
                    CarID = car.CarID,
                    Latitude = car.Latitude ?? 10.7769,
                    Longitude = car.Longitude ?? 106.7009,
                    Speed = 0,
                    EngineStatus = "Off",
                    Timestamp = DateTime.UtcNow.AddMinutes(-45)
                });
            }
            else
            {
                db.VehicleTrackings.Add(new VehicleTracking
                {
                    CarID = car.CarID,
                    Latitude = car.Latitude ?? 10.7769,
                    Longitude = car.Longitude ?? 106.7009,
                    Speed = 22,
                    EngineStatus = "On",
                    Timestamp = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();

            return new PlaywrightScenarioSummary
            {
                ScenarioName = normalizedScenario,
                CarId = car.CarID,
                PendingOwnerCarId = await db.Cars
                    .Where(c => c.LicensePlate == "CYP-00001")
                    .Select(c => c.CarID)
                    .FirstOrDefaultAsync(),
                BookingId = booking.BookingID,
                PaymentId = payment?.PaymentID,
                OrderCode = payosTransaction?.OrderCode,
                AreaId = area.AreaID,
                AdminUserId = users.Admin.UserID,
                StaffUserId = users.Staff.UserID,
                CustomerUserId = users.CustomerVerified.UserID,
                OwnerUserId = users.Owner.UserID,
                AmenityId = amenity.CarAmenityID,
                AmenityName = amenity.Name,
                CarLatitude = car.Latitude,
                CarLongitude = car.Longitude,
                DeliveryLatitude = booking.DeliveryLatitude,
                DeliveryLongitude = booking.DeliveryLongitude,
                DeliveryFee = booking.DeliveryFee,
                DeliveryDistanceKm = booking.DeliveryDistanceKm
            };
        }

        private static async Task SeedMinimumCatalogAsync(ApplicationDbContext db)
        {
            foreach (var statusName in new[]
            {
                CarStatusConstants.Available,
                CarStatusConstants.Pending,
                "Maintenance"
            })
            {
                if (!await db.CarStatuses.AnyAsync(s => s.Name == statusName))
                    db.CarStatuses.Add(new CarStatus { Name = statusName });
            }

            if (!await db.CarTypes.AnyAsync())
                db.CarTypes.Add(new CarType { Name = "Sedan", Description = "Playwright sedan fixture" });

            if (!await db.ContractTypes.AnyAsync(c => c.IsActive))
            {
                db.ContractTypes.Add(new ContractType
                {
                    Name = "1 Month Playwright",
                    Description = "Playwright long-term rental fixture",
                    MinDurationDays = 1,
                    MaxDurationDays = 365,
                    DiscountPercent = 0,
                    IsActive = true
                });
            }

            var location = await db.Locations.FirstOrDefaultAsync(l => l.Name == "Playwright Ho Chi Minh");
            if (location == null)
            {
                location = new Location { Name = "Playwright Ho Chi Minh", Address = "Playwright Test Depot" };
                db.Locations.Add(location);
            }

            if (!await db.Locations.AnyAsync(l => l.Name == "Playwright Empty Region"))
                db.Locations.Add(new Location { Name = "Playwright Empty Region", Address = "No seeded cars" });

            var brand = await db.CarBrands.FirstOrDefaultAsync(b => b.Name == "Toyota");
            if (brand == null)
            {
                brand = new CarBrand { Name = "Toyota" };
                db.CarBrands.Add(brand);
            }

            await db.SaveChangesAsync();

            if (!await db.HolidayPricings.AnyAsync(h => h.HolidayName == "Playwright Booking Holiday"))
            {
                db.HolidayPricings.Add(new HolidayPricing
                {
                    HolidayName = "Playwright Booking Holiday",
                    StartDate = DateTime.UtcNow.AddDays(20).Date,
                    EndDate = DateTime.UtcNow.AddDays(22).Date,
                    PriceMultiplier = 1.25m,
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

            if (!await db.CarModels.AnyAsync(m => m.BrandID == brand.BrandID && m.Name == "Playwright Vios"))
            {
                db.CarModels.Add(new CarModel
                {
                    BrandID = brand.BrandID,
                    Name = "Playwright Vios",
                    SeatCount = 5,
                    FuelType = "Gasoline",
                    Transmission = "Automatic"
                });
            }

            await db.SaveChangesAsync();

            if (!await db.Cars.AnyAsync(c => c.LicensePlate == "PW-READY"))
            {
                var availableStatus = await db.CarStatuses.FirstAsync(s => s.Name == CarStatusConstants.Available);
                var type = await db.CarTypes.OrderBy(t => t.CarTypeID).FirstAsync();
                var selfDrive = await db.ServiceTypes.FirstAsync(s => s.Code == ServiceTypeConstants.SelfDrive);

                db.Cars.Add(new Car
                {
                    CarName = "Playwright Ready Car",
                    BrandID = brand.BrandID,
                    CarTypeID = type.CarTypeID,
                    CarStatusID = availableStatus.CarStatusID,
                    LocationID = location.LocationID,
                    ServiceTypeID = selfDrive.ServiceTypeID,
                    Seats = 5,
                    LicensePlate = "PW-READY",
                    VIN = "CYPREADYVIN00001",
                    PricePerDay = 800000m,
                    PricePerHour = 100000m,
                    FuelType = "Gasoline",
                    Transmission = "Automatic",
                    Description = "Playwright available booking fixture.",
                    IsApproved = true,
                    ImageURL = "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800",
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                });
                await db.SaveChangesAsync();
            }
        }

        private static PlaywrightUserCredential Credential(User user) =>
            new() { Email = user.Email, Password = TestLoginPassword, Role = user.Role, UserId = user.UserID };

        private static async Task<PlaywrightUsers> SeedUsersAsync(ApplicationDbContext db)
        {
            var admin = await UpsertUserAsync(db, "Admin Playwright", "admin@admin.com", UserRoleConstants.Admin, true);
            var manager = await UpsertUserAsync(db, "Manager Playwright", "manager@playwright.local", UserRoleConstants.Manager, true);
            var staff = await UpsertUserAsync(db, "Staff Playwright", "staff@playwright.local", UserRoleConstants.Staff, true);
            var viewer = await UpsertUserAsync(db, "Viewer Playwright", "viewer@playwright.local", UserRoleConstants.Viewer, true);
            var customer = await UpsertUserAsync(db, "Customer Verified Playwright", "customer@playwright.local", UserRoleConstants.Customer, true);
            var unverified = await UpsertUserAsync(db, "Customer Pending Playwright", "customer-unverified@playwright.local", UserRoleConstants.Customer, false);
            var owner = await UpsertUserAsync(db, "Owner Playwright", "owner@playwright.local", UserRoleConstants.Customer, true);

            customer.CitizenIdHash = "playwright-citizen-hash";
            customer.DrivingLicenseHash = "playwright-license-hash";
            customer.DrivingLicenseUrl = "/secure/playwright/license-front.jpg";
            customer.DrivingLicenseBackUrl = "/secure/playwright/license-back.jpg";
            customer.CitizenId = "encrypted-playwright-citizen";
            customer.DrivingLicenseNumber = "encrypted-playwright-license";
            customer.VerificationStatus = "Verified";
            customer.IsVerified = true;

            owner.IsPartnerApproved = true;
            owner.PartnerApprovedAt = DateTime.UtcNow.AddDays(-5);
            owner.PartnerAddress = "Playwright Owner Depot";

            await db.SaveChangesAsync();
            return new PlaywrightUsers(admin, manager, staff, viewer, customer, unverified, owner);
        }

        private static async Task<PlaywrightUsers> LoadUsersAsync(ApplicationDbContext db)
        {
            return new PlaywrightUsers(
                await db.Users.FirstAsync(u => u.Email == "admin@admin.com"),
                await db.Users.FirstAsync(u => u.Email == "manager@playwright.local"),
                await db.Users.FirstAsync(u => u.Email == "staff@playwright.local"),
                await db.Users.FirstAsync(u => u.Email == "viewer@playwright.local"),
                await db.Users.FirstAsync(u => u.Email == "customer@playwright.local"),
                await db.Users.FirstAsync(u => u.Email == "customer-unverified@playwright.local"),
                await db.Users.FirstAsync(u => u.Email == "owner@playwright.local"));
        }

        private static async Task<User> UpsertUserAsync(ApplicationDbContext db, string name, string email, string role, bool verified)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Name = name,
                    Email = email,
                    Phone = "099" + Math.Abs(email.GetHashCode()).ToString("D7")[..7],
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestLoginPassword),
                    Role = role,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                };
                db.Users.Add(user);
            }

            user.Name = name;
            user.Role = role;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestLoginPassword);
            user.IsEmailVerified = true;
            user.IsVerified = verified;
            user.VerificationStatus = verified ? "Verified" : "Pending";
            user.IsLocked = false;
            user.IsBlacklisted = false;
            user.BlacklistReason = null;
            user.Phone ??= "0990000000";
            return user;
        }

        private static async Task SeedVerifiedLicenseAsync(ApplicationDbContext db, int userId)
        {
            if (await db.CustomerDrivingLicenses.AnyAsync(l => l.UserID == userId && l.LicenseNumberHash == "playwright-license-hash"))
                return;

            db.CustomerDrivingLicenses.Add(new CustomerDrivingLicense
            {
                UserID = userId,
                LicenseNumber = "encrypted-playwright-license",
                LicenseNumberHash = "playwright-license-hash",
                LicenseClass = "B2",
                ExpiryDate = DateTime.UtcNow.AddYears(5),
                FrontImageUrl = "/secure/playwright/license-front.jpg",
                BackImageUrl = "/secure/playwright/license-back.jpg",
                VerificationStatus = "Verified",
                IsApproved = true,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        private static async Task SeedLegalDocumentsAsync(ApplicationDbContext db)
        {
            foreach (var code in new[]
            {
                LegalDocumentCodes.TermsOfUse,
                LegalDocumentCodes.PrivacyNotice,
                LegalDocumentCodes.EkycBiometricConsent,
                LegalDocumentCodes.SelfDriveAgreement,
                LegalDocumentCodes.WalletLedgerNotice
            })
            {
                if (await db.LegalDocumentVersions.AnyAsync(d => d.Code == code && d.IsActive))
                    continue;

                db.LegalDocumentVersions.Add(new LegalDocumentVersion
                {
                    Code = code,
                    Version = "2026.05-playwright",
                    Title = code,
                    ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code))).ToLowerInvariant(),
                    EffectiveAt = DateTime.UtcNow.AddDays(-1),
                    PublishedAt = DateTime.UtcNow.AddDays(-1),
                    IsActive = true,
                    SourceUrl = $"/Home/{(code == LegalDocumentCodes.PrivacyNotice ? "Privacy" : "Terms")}"
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task<Area> SeedAreaAsync(ApplicationDbContext db, User admin, User manager, User staff, User viewer)
        {
            var area = await db.Areas.FirstOrDefaultAsync(a => a.Code == "CYP-HCM");
            if (area == null)
            {
                area = new Area
                {
                    Name = "Playwright Ho Chi Minh",
                    Code = "CYP-HCM",
                    Province = "Ho Chi Minh",
                    Address = "Playwright Test Depot",
                    Latitude = 10.7769,
                    Longitude = 106.7009,
                    ManagerID = manager.UserID,
                    Status = "active"
                };
                db.Areas.Add(area);
                await db.SaveChangesAsync();
            }

            var location = await db.Locations.FirstAsync();
            if (!await db.AreaLocations.AnyAsync(al => al.AreaID == area.AreaID && al.LocationID == location.LocationID))
            {
                db.AreaLocations.Add(new AreaLocation { AreaID = area.AreaID, LocationID = location.LocationID });
            }

            await EnsureAreaStaffAsync(db, area.AreaID, manager.UserID, AreaStaffRoleConstants.Manager, admin.UserID);
            await EnsureAreaStaffAsync(db, area.AreaID, staff.UserID, AreaStaffRoleConstants.Staff, admin.UserID);
            await EnsureAreaStaffAsync(db, area.AreaID, viewer.UserID, AreaStaffRoleConstants.Viewer, admin.UserID);
            await db.SaveChangesAsync();
            return area;
        }

        private static async Task EnsureAreaStaffAsync(ApplicationDbContext db, int areaId, int userId, string role, int assignedBy)
        {
            if (await db.AreaStaffs.AnyAsync(a => a.AreaID == areaId && a.UserID == userId))
                return;

            db.AreaStaffs.Add(new AreaStaff
            {
                AreaID = areaId,
                UserID = userId,
                RoleInArea = role,
                AssignedBy = assignedBy
            });
        }

        private static async Task<PlaywrightCars> SeedCarsAsync(ApplicationDbContext db, int ownerId)
        {
            var availableStatus = await db.CarStatuses.FirstAsync(s => s.Name == CarStatusConstants.Available);
            var pendingStatus = await db.CarStatuses.FirstAsync(s => s.Name == CarStatusConstants.Pending);
            var brand = await db.CarBrands.FirstAsync();
            var type = await db.CarTypes.FirstAsync();
            var location = await db.Locations.FirstAsync();
            var selfDrive = await db.ServiceTypes.FirstAsync(s => s.Code == ServiceTypeConstants.SelfDrive);

            var firstAvailable = await db.Cars
                .Where(c => c.CarStatusID == availableStatus.CarStatusID && c.IsApproved && !c.IsDeleted)
                .OrderBy(c => c.CarID)
                .FirstAsync();
            await ConfigureDeliveryCarAsync(db, firstAvailable, ownerId);

            var pendingOwnerCar = await db.Cars.FirstOrDefaultAsync(c => c.LicensePlate == "PWT-00001");
            if (pendingOwnerCar == null)
            {
                pendingOwnerCar = new Car
                {
                    CarName = "Playwright Owner Pending Car",
                    BrandID = brand.BrandID,
                    CarTypeID = type.CarTypeID,
                    CarStatusID = pendingStatus.CarStatusID,
                    LocationID = location.LocationID,
                    ServiceTypeID = selfDrive.ServiceTypeID,
                    Seats = 5,
                    LicensePlate = "PWT-00001",
                    VIN = "PLAYWRIGHTVIN0001",
                    PricePerDay = 700000m,
                    PricePerHour = 90000m,
                    FuelType = "Gasoline",
                    Transmission = "Automatic",
                    Description = "Playwright owner approval fixture.",
                    NextInspectionDate = DateTime.UtcNow.Date.AddMonths(8),
                    CarAddress = "Playwright Owner Pending Pickup, Quan 1, TP Ho Chi Minh",
                    Latitude = 10.7769,
                    Longitude = 106.7009,
                    OwnerID = ownerId,
                    IsApproved = false,
                    ApprovalStatus = CarApprovalStatusConstants.Pending,
                    ImageURL = "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800"
                };
                db.Cars.Add(pendingOwnerCar);
                await db.SaveChangesAsync();
            }

            return new PlaywrightCars(firstAvailable, pendingOwnerCar);
        }

        private static async Task ConfigureDeliveryCarAsync(ApplicationDbContext db, Car car, int ownerId)
        {
            car.OwnerID = ownerId;
            car.CarAddress = "Playwright Owner Depot, Quan 1, TP Ho Chi Minh";
            car.Latitude = 10.7769;
            car.Longitude = 106.7009;
            car.DeliveryEnabled = true;
            car.MaxDeliveryDistanceKm = 40m;
            car.FreeDeliveryRadiusKm = 3m;
            car.DeliveryFeePerKm = 7000m;
            car.ImageURL ??= "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800";
            await db.SaveChangesAsync();
        }

        private static async Task<Booking> EnsureDeliveryTrackingBookingAsync(ApplicationDbContext db, PlaywrightUsers users, Car car)
        {
            var confirmedStatus = await db.BookingStatuses.FirstAsync(s => s.Name == BookingStatusConstants.Confirmed);
            var location = await db.Locations.FirstAsync();
            var selfDrive = await db.ServiceTypes.FirstAsync(s => s.Code == ServiceTypeConstants.SelfDrive);
            var terms = await db.LegalDocumentVersions.FirstAsync(d => d.Code == LegalDocumentCodes.TermsOfUse && d.IsActive);
            var privacy = await db.LegalDocumentVersions.FirstAsync(d => d.Code == LegalDocumentCodes.PrivacyNotice && d.IsActive);

            var booking = await db.Bookings
                .Where(b => b.UserID == users.CustomerVerified.UserID && b.CarID == car.CarID)
                .OrderByDescending(b => b.BookingID)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                booking = new Booking
                {
                    UserID = users.CustomerVerified.UserID,
                    CarID = car.CarID,
                    ServiceTypeID = selfDrive.ServiceTypeID,
                    StartDate = DateTime.UtcNow.AddDays(30),
                    EndDate = DateTime.UtcNow.AddDays(32),
                    PickupLocationID = location.LocationID,
                    ReturnLocationID = location.LocationID,
                    TotalPrice = 1600000m,
                    TotalHours = 48,
                    DepositAmount = 300000m,
                    ReservationFee = 300000m,
                    DepositStatus = DepositStatusConstants.Unpaid,
                    ContractTypeCode = "SELF_DRIVE_CONTRACT",
                    TermsDocumentVersionID = terms.LegalDocumentVersionID,
                    PrivacyNoticeVersionID = privacy.LegalDocumentVersionID,
                    SelfDriveAgreementAcceptedAt = DateTime.UtcNow.AddDays(-1)
                };
                db.Bookings.Add(booking);
                await db.SaveChangesAsync();
            }

            booking.BookingStatusID = confirmedStatus.BookingStatusID;
            booking.ServiceTypeID = selfDrive.ServiceTypeID;
            booking.PickupLocationID = location.LocationID;
            booking.ReturnLocationID = location.LocationID;
            booking.DeliveryMode = DeliveryModeConstants.OwnerDelivery;
            booking.DeliveryAddress = "Playwright Customer Dropoff, Nguyen Hue, Quan 1, TP Ho Chi Minh";
            booking.DeliveryLatitude = 10.7757;
            booking.DeliveryLongitude = 106.7019;
            booking.DeliveryDistanceKm = 4.6m;
            booking.DeliveryEtaMinutes = 70;
            booking.DeliveryQuotedAt = DateTime.UtcNow;
            booking.DeliveryFee = 14000m;
            booking.TermsDocumentVersionID = terms.LegalDocumentVersionID;
            booking.PrivacyNoticeVersionID = privacy.LegalDocumentVersionID;

            await db.SaveChangesAsync();

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingID == booking.BookingID);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = booking.BookingID,
                    Amount = booking.ReservationFee,
                    PaymentMethod = "PayOS",
                    PaymentStatus = PaymentStatusConstants.Pending,
                    PaymentDate = DateTime.UtcNow
                };
                db.Payments.Add(payment);
                await db.SaveChangesAsync();
            }

            if (!await db.PayOSTransactions.AnyAsync(t => t.BookingID == booking.BookingID))
            {
                db.PayOSTransactions.Add(new PayOSTransaction
                {
                    BookingID = booking.BookingID,
                    UserID = users.CustomerVerified.UserID,
                    PaymentID = payment.PaymentID,
                    OrderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Amount = booking.ReservationFee,
                    Status = PaymentStatusConstants.Pending,
                    CheckoutUrl = "/Payment/Pending",
                    PayOSDescription = "Playwright delivery tracking payment fixture"
                });
                await db.SaveChangesAsync();
            }

            return booking;
        }

        private static async Task<CarAmenity> EnsureSandboxAmenityAsync(ApplicationDbContext db, int carId)
        {
            var amenity = await db.CarAmenities.FirstOrDefaultAsync(a => a.Code == "PLAYWRIGHT_SANDBOX_FEATURE");
            if (amenity == null)
            {
                amenity = new CarAmenity
                {
                    Code = "PLAYWRIGHT_SANDBOX_FEATURE",
                    Name = "Playwright Sandbox Feature",
                    Icon = "bi-broadcast-pin",
                    DisplayOrder = 900,
                    IsActive = true
                };
                db.CarAmenities.Add(amenity);
                await db.SaveChangesAsync();
            }

            var mapping = await db.CarAmenityMappings.FirstOrDefaultAsync(m => m.CarID == carId && m.CarAmenityID == amenity.CarAmenityID);
            if (mapping == null)
            {
                db.CarAmenityMappings.Add(new CarAmenityMapping
                {
                    CarID = carId,
                    CarAmenityID = amenity.CarAmenityID
                });
            }
            else
            {
                mapping.IsDeleted = false;
            }

            await db.SaveChangesAsync();
            return amenity;
        }

        private static async Task<PayOSTransaction> EnsurePendingPayOSHoldAsync(ApplicationDbContext db, PlaywrightUsers users, Car car)
        {
            const string description = "Playwright pending hold payment fixture";
            var existing = await db.PayOSTransactions
                .Include(t => t.BookingHold)
                .Where(t => t.UserID == users.CustomerVerified.UserID && t.PayOSDescription == description)
                .OrderByDescending(t => t.PayOSTransactionID)
                .FirstOrDefaultAsync();

            if (existing?.BookingHold != null
                && existing.Status == PaymentStatusConstants.Pending
                && existing.BookingHold.Status == BookingHoldStatusConstants.Active
                && existing.BookingHold.ExpiresAt > DateTime.UtcNow.AddMinutes(2))
            {
                return existing;
            }

            var start = DateTime.UtcNow.AddDays(12);
            var end = start.AddDays(2);
            var quote = new CheckoutQuoteViewModel
            {
                UserID = users.CustomerVerified.UserID,
                CarID = car.CarID,
                CarName = car.CarName,
                LicensePlate = car.LicensePlate,
                StartDate = start,
                EndDate = end,
                TotalHours = 48,
                RentalDays = 2,
                BaseRentalAmount = car.PricePerDay * 2,
                TotalPrice = car.PricePerDay * 2,
                HoldingFee = 108000m
            };

            var hold = new BookingHold
            {
                UserID = users.CustomerVerified.UserID,
                CarID = car.CarID,
                StartDate = start,
                EndDate = end,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Status = BookingHoldStatusConstants.Active,
                QuoteSnapshotJson = JsonSerializer.Serialize(quote, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            };
            db.BookingHolds.Add(hold);
            await db.SaveChangesAsync();

            var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Random.Shared.Next(1000, 9999);
            var checkoutUrl = $"https://pay.payos.vn/web/checkout/playwright-{orderCode}";
            var accountNumber = "123456789";
            var bin = "970422";
            var transferContent = $"PLAYWRIGHT {orderCode}";

            var transaction = new PayOSTransaction
            {
                BookingHoldID = hold.BookingHoldID,
                UserID = users.CustomerVerified.UserID,
                OrderCode = orderCode,
                Amount = quote.HoldingFee,
                Status = PaymentStatusConstants.Pending,
                CheckoutUrl = checkoutUrl,
                QrCode = $"https://img.vietqr.io/image/{bin}-{accountNumber}-compact.png?amount=108000&addInfo={Uri.EscapeDataString(transferContent)}&accountName=CAR%20RENTAL",
                Bin = bin,
                AccountNumber = accountNumber,
                AccountName = "CAR RENTAL",
                PayOSDescription = description
            };
            db.PayOSTransactions.Add(transaction);
            await db.SaveChangesAsync();

            transaction.BookingHold = hold;
            return transaction;
        }

        private static async Task<OpsCopilotScenario> EnsureOpsCopilotScenarioAsync(ApplicationDbContext db, PlaywrightUsers users, Area area)
        {
            var now = DateTime.UtcNow;
            var location = await db.Locations.FirstAsync();
            var brand = await db.CarBrands.FirstAsync();
            var type = await db.CarTypes.FirstAsync();
            var selfDrive = await db.ServiceTypes.FirstAsync(s => s.Code == ServiceTypeConstants.SelfDrive);
            var availableStatus = await db.CarStatuses.FirstAsync(s => s.Name == CarStatusConstants.Available);
            var rentedStatus = await db.CarStatuses.FirstOrDefaultAsync(s => s.Name == CarStatusConstants.Rented) ?? availableStatus;
            var activeStatus = await db.BookingStatuses.FirstAsync(s => s.Name == BookingStatusConstants.Active);
            var terms = await db.LegalDocumentVersions.FirstAsync(d => d.Code == LegalDocumentCodes.TermsOfUse && d.IsActive);
            var privacy = await db.LegalDocumentVersions.FirstAsync(d => d.Code == LegalDocumentCodes.PrivacyNotice && d.IsActive);

            var car = await db.Cars.FirstOrDefaultAsync(c => c.LicensePlate == "CYP-OPS01");
            if (car == null)
            {
                car = new Car
                {
                    CarName = "Playwright Ops Copilot SUV",
                    BrandID = brand.BrandID,
                    CarTypeID = type.CarTypeID,
                    CarStatusID = rentedStatus.CarStatusID,
                    LocationID = location.LocationID,
                    ServiceTypeID = selfDrive.ServiceTypeID,
                    Seats = 7,
                    LicensePlate = "CYP-OPS01",
                    VIN = "CYPOPSCOPILOT0001",
                    PricePerDay = 1200000m,
                    PricePerHour = 160000m,
                    FuelType = "Gasoline",
                    Transmission = "Automatic",
                    Description = "Playwright fixture for AI Fleet Operations Copilot.",
                    ImageURL = "https://images.unsplash.com/photo-1603584173870-7f23fdae1b7a?w=800",
                    CreatedAt = now.AddDays(-30)
                };
                db.Cars.Add(car);
            }

            car.OwnerID = users.Owner.UserID;
            car.CarStatusID = rentedStatus.CarStatusID;
            car.LocationID = location.LocationID;
            car.ServiceTypeID = selfDrive.ServiceTypeID;
            car.IsApproved = true;
            car.ApprovalStatus = CarApprovalStatusConstants.Approved;
            car.ApprovalRejectionReason = null;
            car.IsDeleted = false;
            car.LicensePlateNormalized = LicensePlateNormalizer.Normalize(car.LicensePlate);
            car.CarAddress = "Playwright Ops Depot, District 1, Ho Chi Minh";
            car.Latitude = 10.7769;
            car.Longitude = 106.7009;
            car.DeliveryEnabled = true;
            car.MaxDeliveryDistanceKm = 40m;
            car.FreeDeliveryRadiusKm = 3m;
            car.DeliveryFeePerKm = 7000m;
            car.NextMaintenanceDate = now.AddDays(3);
            car.ContractSigningMode = CarContractSigningModes.Digital;
            car.UpdatedAt = now;
            await db.SaveChangesAsync();

            var booking = await db.Bookings
                .FirstOrDefaultAsync(b => b.CarID == car.CarID && b.AdminNotes == "CYP-OPS-COPILOT");
            if (booking == null)
            {
                booking = new Booking
                {
                    UserID = users.CustomerVerified.UserID,
                    CarID = car.CarID,
                    CreatedAt = now.AddDays(-2),
                    AdminNotes = "CYP-OPS-COPILOT"
                };
                db.Bookings.Add(booking);
            }

            booking.UserID = users.CustomerVerified.UserID;
            booking.CarID = car.CarID;
            booking.ServiceTypeID = selfDrive.ServiceTypeID;
            booking.StartDate = now.AddDays(-2);
            booking.EndDate = now.AddHours(-2);
            booking.ActualPickupDate = now.AddDays(-2).AddHours(1);
            booking.ActualReturnDate = null;
            booking.PickupLocationID = location.LocationID;
            booking.ReturnLocationID = location.LocationID;
            booking.TotalPrice = 2400000m;
            booking.TotalHours = 48;
            booking.BookingStatusID = activeStatus.BookingStatusID;
            booking.DepositAmount = 5000000m;
            booking.ReservationFee = 300000m;
            booking.DepositStatus = DepositStatusConstants.Holding;
            booking.ContractTypeCode = "SELF_DRIVE_CONTRACT";
            booking.TermsDocumentVersionID = terms.LegalDocumentVersionID;
            booking.PrivacyNoticeVersionID = privacy.LegalDocumentVersionID;
            booking.SelfDriveAgreementAcceptedAt = now.AddDays(-2);
            booking.DeliveryMode = DeliveryModeConstants.OwnerDelivery;
            booking.DeliveryAddress = "Playwright Ops Customer Dropoff";
            booking.DeliveryLatitude = 10.7757;
            booking.DeliveryLongitude = 106.7019;
            booking.DeliveryDistanceKm = 4.6m;
            booking.DeliveryEtaMinutes = 70;
            booking.DeliveryQuotedAt = now.AddDays(-2);
            booking.DeliveryFee = 14000m;
            booking.UpdatedAt = now;
            await db.SaveChangesAsync();

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingID == booking.BookingID);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = booking.BookingID,
                    PaymentMethod = "PayOS"
                };
                db.Payments.Add(payment);
            }

            payment.Amount = booking.ReservationFee;
            payment.PaymentDate = now.AddDays(-2);
            payment.PaymentStatus = PaymentStatusConstants.Paid;
            payment.IsDeleted = false;
            await db.SaveChangesAsync();

            var paidWithoutLedgerOrder = 900000000000L + booking.BookingID;
            var payOS = await db.PayOSTransactions.FirstOrDefaultAsync(t => t.OrderCode == paidWithoutLedgerOrder);
            if (payOS == null)
            {
                payOS = new PayOSTransaction { OrderCode = paidWithoutLedgerOrder };
                db.PayOSTransactions.Add(payOS);
            }

            payOS.BookingID = booking.BookingID;
            payOS.UserID = users.CustomerVerified.UserID;
            payOS.PaymentID = payment.PaymentID;
            payOS.Amount = payment.Amount;
            payOS.Status = PaymentStatusConstants.Paid;
            payOS.CheckoutUrl = "/Payment/Pending";
            payOS.PayOSDescription = "Playwright ops copilot paid without ledger";
            payOS.CreatedAt = now.AddDays(-2);
            payOS.PaidAt = now.AddDays(-2).AddMinutes(5);
            payOS.IsDeleted = false;

            var ledgerMirror = await db.PaymentLedgerEntries
                .Where(l => l.OrderCode == paidWithoutLedgerOrder && l.IdempotencyKey == $"playwright-ops:payos-without-ledger:{booking.BookingID}")
                .ToListAsync();
            db.PaymentLedgerEntries.RemoveRange(ledgerMirror);

            var ledgerOnlyOrder = 900000100000L + booking.BookingID;
            var ledger = await db.PaymentLedgerEntries
                .FirstOrDefaultAsync(l => l.IdempotencyKey == $"playwright-ops:ledger-without-payos:{booking.BookingID}");
            if (ledger == null)
            {
                ledger = new PaymentLedgerEntry
                {
                    IdempotencyKey = $"playwright-ops:ledger-without-payos:{booking.BookingID}"
                };
                db.PaymentLedgerEntries.Add(ledger);
            }

            ledger.BookingID = booking.BookingID;
            ledger.PaymentID = payment.PaymentID;
            ledger.Provider = "PayOS";
            ledger.OrderCode = ledgerOnlyOrder;
            ledger.EntryType = PaymentLedgerEntryTypeConstants.DepositCharge;
            ledger.Amount = 250000m;
            ledger.Currency = "VND";
            ledger.Status = PaymentStatusConstants.Paid;
            ledger.Description = "Playwright ops copilot ledger without PayOS mirror.";
            ledger.CreatedAt = now.AddHours(-6);
            ledger.PostedAt = now.AddHours(-6);

            var refund = await db.RefundRequests
                .FirstOrDefaultAsync(r => r.BookingID == booking.BookingID && r.Reason == "PLAYWRIGHT_OPS_COPILOT_REFUND_PENDING");
            if (refund == null)
            {
                refund = new RefundRequest
                {
                    BookingID = booking.BookingID,
                    Reason = "PLAYWRIGHT_OPS_COPILOT_REFUND_PENDING"
                };
                db.RefundRequests.Add(refund);
            }

            refund.Amount = 300000m;
            refund.Status = RefundRequestStatusConstants.PendingManual;
            refund.CreatedAt = now.AddHours(-5);
            refund.ApprovedByUserID = users.Manager.UserID;
            refund.ApprovedAt = now.AddHours(-4);
            await db.SaveChangesAsync();

            var payout = await db.RefundPayouts
                .FirstOrDefaultAsync(p => p.IdempotencyKey == $"playwright-ops:payout:{refund.RefundRequestID}");
            if (payout == null)
            {
                payout = new RefundPayout
                {
                    RefundRequestID = refund.RefundRequestID,
                    IdempotencyKey = $"playwright-ops:payout:{refund.RefundRequestID}"
                };
                db.RefundPayouts.Add(payout);
            }

            payout.ReferenceId = $"CYP-OPS-PAYOUT-{refund.RefundRequestID}";
            payout.Provider = "PayOS";
            payout.Amount = refund.Amount;
            payout.Currency = "VND";
            payout.ApprovalState = PaymentStatusConstants.PendingManual;
            payout.TransactionState = PaymentStatusConstants.PendingManual;
            payout.AttemptCount = 1;
            payout.LastError = "Sandbox payout permission pending.";
            payout.MaskedRecipientAccount = "****9012";
            payout.CreatedAt = now.AddHours(-4);

            var existingTrackings = await db.VehicleTrackings.Where(t => t.CarID == car.CarID).ToListAsync();
            db.VehicleTrackings.RemoveRange(existingTrackings);
            db.VehicleTrackings.AddRange(
                new VehicleTracking
                {
                    CarID = car.CarID,
                    Latitude = 10.7769,
                    Longitude = 106.7009,
                    Speed = 18,
                    EngineStatus = "On",
                    Source = "MobileGps",
                    BatteryLevel = 61,
                    Timestamp = now.AddMinutes(-12)
                },
                new VehicleTracking
                {
                    CarID = car.CarID,
                    Latitude = 10.7759,
                    Longitude = 106.7012,
                    Speed = 21,
                    EngineStatus = "On",
                    Source = "HardwareGps",
                    BatteryLevel = 73,
                    Timestamp = now.AddMinutes(-40)
                },
                new VehicleTracking
                {
                    CarID = car.CarID,
                    Latitude = 10.7748,
                    Longitude = 106.7020,
                    Speed = 0,
                    EngineStatus = "Off",
                    Source = "HardwareGps",
                    BatteryLevel = 80,
                    Timestamp = now.AddHours(-3)
                });

            var camera = await db.CameraDevices.FirstOrDefaultAsync(c => c.Code == "CYP-OPS-GATE-1");
            if (camera == null)
            {
                camera = new CameraDevice
                {
                    Code = "CYP-OPS-GATE-1"
                };
                db.CameraDevices.Add(camera);
            }

            camera.Name = "Playwright Ops Gate 1";
            camera.Provider = "Playwright";
            camera.AreaID = area.AreaID;
            camera.LocationID = location.LocationID;
            camera.DefaultDirection = CameraGateDirectionConstants.In;
            camera.LiveViewUrl = "/Admin/Camera/Monitor";
            camera.Status = CameraDeviceStatusConstants.Active;
            camera.EncryptedWebhookSecret = "fixture";
            camera.UpdatedAt = now;
            await db.SaveChangesAsync();

            var matchedEvent = await UpsertCameraEventAsync(
                db,
                camera,
                car,
                area.AreaID,
                location.LocationID,
                "playwright-ops-match",
                CameraGateMatchStatusConstants.Matched,
                0.94m,
                null,
                now.AddMinutes(-30));
            var mismatchEvent = await UpsertCameraEventAsync(
                db,
                camera,
                car,
                area.AreaID,
                location.LocationID,
                "playwright-ops-mismatch",
                CameraGateMatchStatusConstants.AreaMismatch,
                0.91m,
                "Car appeared at gate while assigned route still expects customer return.",
                now.AddMinutes(-8));

            var alert = await db.Alerts.FirstOrDefaultAsync(a =>
                a.CarID == car.CarID &&
                a.AlertType == "CameraAreaMismatch" &&
                a.Description != null &&
                a.Description.Contains("PLAYWRIGHT_OPS_COPILOT"));
            if (alert == null)
            {
                alert = new Alert
                {
                    CarID = car.CarID,
                    AlertType = "CameraAreaMismatch"
                };
                db.Alerts.Add(alert);
            }

            alert.Status = "Open";
            alert.AlertTime = now.AddMinutes(-7);
            alert.Description = "PLAYWRIGHT_OPS_COPILOT camera mismatch for demo vehicle.";
            alert.IsDeleted = false;

            var contract = await db.DigitalContracts.FirstOrDefaultAsync(c =>
                c.BookingID == booking.BookingID &&
                c.ContractNo == $"CYP-OPS-{booking.BookingID}");
            if (contract == null)
            {
                contract = new DigitalContract
                {
                    ContractNo = $"CYP-OPS-{booking.BookingID}",
                    BookingID = booking.BookingID,
                    CustomerName = "Playwright Ops Customer",
                    CustomerIdentityCard = "MASKED-PLAYWRIGHT",
                    CustomerPhone = "MASKED-PHONE",
                    CustomerAddress = "Masked Playwright Address",
                    CarLicensePlate = car.LicensePlate,
                    CarModel = car.CarName,
                    TermsAndConditions = "Playwright ops copilot digital contract fixture."
                };
                db.DigitalContracts.Add(contract);
            }

            contract.RentalStartDate = booking.StartDate;
            contract.RentalEndDate = booking.EndDate;
            contract.RentalPricePerDay = car.PricePerDay;
            contract.TotalContractValue = booking.TotalPrice;
            contract.DepositAmount = booking.DepositAmount;
            contract.IsSignedByCustomer = true;
            contract.CustomerSignDate = now.AddDays(-2);
            contract.CustomerSignatureHash = "playwright-customer-signature-hash";
            contract.IsSignedByOwner = false;
            contract.OwnerSignDate = null;
            contract.ContractSigningMode = CarContractSigningModes.Digital;
            contract.RenderedContentHash = "playwright-rendered-contract-hash";
            contract.SignedAt = now.AddDays(-2);
            contract.IsDeleted = false;

            await UpsertHandoverAsync(db, booking.BookingID, users.Staff.UserID, "CheckIn", now.AddDays(-2).AddHours(1), true);
            await UpsertHandoverAsync(db, booking.BookingID, users.Staff.UserID, "CheckOut", now.AddHours(-1), false);

            var support = await db.SupportTickets.FirstOrDefaultAsync(t => t.Subject == "Playwright Ops Copilot incident");
            if (support == null)
            {
                support = new SupportTicket
                {
                    UserID = users.CustomerVerified.UserID,
                    Subject = "Playwright Ops Copilot incident"
                };
                db.SupportTickets.Add(support);
            }

            support.BookingID = booking.BookingID;
            support.AssignedToUserID = users.Staff.UserID;
            support.TicketType = SupportTicketTypeConstants.Emergency;
            support.Priority = SupportTicketPriorityConstants.P1;
            support.Status = SupportTicketStatusConstants.Open;
            support.Description = "Playwright ops copilot seeded support SLA risk.";
            support.CreatedAt = now.AddHours(-2);
            support.FirstResponseDueAt = now.AddMinutes(-30);
            support.UpdatedAt = now.AddMinutes(-20);

            await db.SaveChangesAsync();
            return new OpsCopilotScenario(car, booking, payment, paidWithoutLedgerOrder, matchedEvent.CameraGateEventID, mismatchEvent.CameraGateEventID);
        }

        private static async Task<CameraGateEvent> UpsertCameraEventAsync(
            ApplicationDbContext db,
            CameraDevice camera,
            Car car,
            int areaId,
            int locationId,
            string providerEventId,
            string matchStatus,
            decimal confidence,
            string? failureReason,
            DateTime capturedAt)
        {
            var gateEvent = await db.CameraGateEvents
                .FirstOrDefaultAsync(e => e.CameraDeviceID == camera.CameraDeviceID && e.ProviderEventId == providerEventId);
            if (gateEvent == null)
            {
                gateEvent = new CameraGateEvent
                {
                    CameraDeviceID = camera.CameraDeviceID,
                    ProviderEventId = providerEventId
                };
                db.CameraGateEvents.Add(gateEvent);
            }

            gateEvent.LicensePlateRaw = car.LicensePlate;
            gateEvent.LicensePlateNormalized = LicensePlateNormalizer.Normalize(car.LicensePlate);
            gateEvent.Direction = CameraGateDirectionConstants.In;
            gateEvent.Confidence = confidence;
            gateEvent.CapturedAt = capturedAt;
            gateEvent.ReceivedAt = DateTime.UtcNow;
            gateEvent.SnapshotUrl = "/img/playwright/camera-ops-fixture.jpg";
            gateEvent.CarID = car.CarID;
            gateEvent.AreaID = areaId;
            gateEvent.LocationID = locationId;
            gateEvent.MatchStatus = matchStatus;
            gateEvent.FailureReason = failureReason;
            gateEvent.IsDeleted = false;
            await db.SaveChangesAsync();
            return gateEvent;
        }

        private static async Task UpsertHandoverAsync(ApplicationDbContext db, int bookingId, int staffUserId, string type, DateTime recordDate, bool completeEvidence)
        {
            var handover = await db.HandoverRecords.FirstOrDefaultAsync(h => h.BookingID == bookingId && h.Type == type);
            if (handover == null)
            {
                handover = new HandoverRecord
                {
                    BookingID = bookingId,
                    Type = type
                };
                db.HandoverRecords.Add(handover);
            }

            handover.RecordDate = recordDate;
            handover.Odo = type == "CheckIn" ? 25310 : 25488;
            handover.FuelLevel = "3/4";
            handover.FrontImgUrl = "/img/playwright/handover-front.jpg";
            handover.BackImgUrl = completeEvidence ? "/img/playwright/handover-back.jpg" : null;
            handover.LeftImgUrl = "/img/playwright/handover-left.jpg";
            handover.RightImgUrl = "/img/playwright/handover-right.jpg";
            handover.InsideImgUrl = "/img/playwright/handover-inside.jpg";
            handover.SignatureUrl = completeEvidence ? "/img/playwright/handover-signature.png" : null;
            handover.GeoLatitude = 10.7769;
            handover.GeoLongitude = 106.7009;
            handover.EvidenceHash = completeEvidence ? $"playwright-{type.ToLowerInvariant()}-evidence-hash" : null;
            handover.CustomerSignatureHash = completeEvidence ? $"playwright-{type.ToLowerInvariant()}-customer-signature-hash" : null;
            handover.RecordedByUserId = staffUserId;
            handover.IsDeleted = false;
            await db.SaveChangesAsync();
        }

        private static async Task<Booking> SeedOperationalRecordsAsync(ApplicationDbContext db, PlaywrightUsers users, Car car, Area area)
        {
            var status = await db.BookingStatuses.FirstAsync(s => s.Name == BookingStatusConstants.Confirmed);
            var location = await db.Locations.FirstAsync();
            var selfDrive = await db.ServiceTypes.FirstAsync(s => s.Code == ServiceTypeConstants.SelfDrive);
            var terms = await db.LegalDocumentVersions.FirstAsync(d => d.Code == LegalDocumentCodes.TermsOfUse && d.IsActive);
            var privacy = await db.LegalDocumentVersions.FirstAsync(d => d.Code == LegalDocumentCodes.PrivacyNotice && d.IsActive);

            var booking = new Booking
            {
                UserID = users.CustomerVerified.UserID,
                CarID = car.CarID,
                ServiceTypeID = selfDrive.ServiceTypeID,
                StartDate = DateTime.UtcNow.AddDays(20),
                EndDate = DateTime.UtcNow.AddDays(22),
                PickupLocationID = location.LocationID,
                ReturnLocationID = location.LocationID,
                TotalPrice = 1600000m,
                TotalHours = 48,
                BookingStatusID = status.BookingStatusID,
                DepositAmount = 300000m,
                ReservationFee = 300000m,
                DepositStatus = DepositStatusConstants.Unpaid,
                ContractTypeCode = "SELF_DRIVE_CONTRACT",
                TermsDocumentVersionID = terms.LegalDocumentVersionID,
                PrivacyNoticeVersionID = privacy.LegalDocumentVersionID,
                SelfDriveAgreementAcceptedAt = DateTime.UtcNow.AddDays(-1)
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            var payment = new Payment
            {
                BookingID = booking.BookingID,
                Amount = booking.ReservationFee,
                PaymentMethod = "PayOS",
                PaymentStatus = PaymentStatusConstants.Pending,
                PaymentDate = DateTime.UtcNow
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            db.PayOSTransactions.Add(new PayOSTransaction
            {
                BookingID = booking.BookingID,
                UserID = users.CustomerVerified.UserID,
                PaymentID = payment.PaymentID,
                OrderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Amount = booking.ReservationFee,
                Status = PaymentStatusConstants.Pending,
                CheckoutUrl = "/Payment/Pending",
                PayOSDescription = "Playwright payment fixture"
            });

            db.PrivacyRequests.Add(new PrivacyRequest
            {
                UserID = users.CustomerVerified.UserID,
                RequestType = PrivacyRequestTypeConstants.Export,
                Status = PrivacyRequestStatusConstants.PendingReview,
                Reason = "Playwright export request"
            });

            db.SupportTickets.Add(new SupportTicket
            {
                UserID = users.CustomerVerified.UserID,
                BookingID = booking.BookingID,
                TicketType = SupportTicketTypeConstants.Booking,
                Priority = SupportTicketPriorityConstants.P2,
                Subject = "Playwright support ticket",
                Description = "Seeded ticket for Playwright support tests."
            });

            db.ContactMessages.Add(new ContactMessage
            {
                Name = "Playwright Contact",
                Email = "contact@playwright.local",
                Subject = "Playwright contact",
                Message = "Seeded contact message."
            });

            db.Invoices.Add(new Invoice
            {
                InvoiceNumber = $"CYP-{booking.BookingID:D6}",
                BookingID = booking.BookingID,
                UserID = users.CustomerVerified.UserID,
                RentalAmount = booking.TotalPrice,
                SubTotal = booking.TotalPrice,
                TaxAmount = 160000m,
                TotalAmount = 1760000m,
                AmountDue = 1460000m,
                DepositDeducted = 300000m,
                Status = "Issued",
                CreatedBy = "Playwright"
            });

            db.SettlementReviews.Add(new SettlementReview
            {
                BookingID = booking.BookingID,
                ReviewType = SettlementReviewTypeConstants.Cleaning,
                Amount = 50000m,
                Reason = "Playwright seeded settlement review.",
                RequestedByUserID = users.Staff.UserID
            });

            await db.SaveChangesAsync();
            return booking;
        }

        private sealed record PlaywrightUsers(
            User Admin,
            User Manager,
            User Staff,
            User Viewer,
            User CustomerVerified,
            User CustomerUnverified,
            User Owner);

        private sealed record PlaywrightCars(Car FirstAvailableCar, Car PendingOwnerCar);
        private sealed record OpsCopilotScenario(Car Car, Booking Booking, Payment Payment, long OrderCode, int? CameraGateEventId, int? MismatchedCameraGateEventId);
    }

    public sealed class PlaywrightSeedSummary
    {
        public DateTime ResetAtUtc { get; set; }
        public Dictionary<string, PlaywrightUserCredential> Users { get; set; } = new();
        public int FirstAvailableCarId { get; set; }
        public int PendingOwnerCarId { get; set; }
        public int BookingId { get; set; }
        public int PaymentId { get; set; }
        public int PrivacyRequestId { get; set; }
        public int AreaId { get; set; }
        public int ContractTypeId { get; set; }
    }

    public sealed class PlaywrightScenarioSummary
    {
        public string ScenarioName { get; set; } = string.Empty;
        public int CarId { get; set; }
        public int PendingOwnerCarId { get; set; }
        public int BookingId { get; set; }
        public int? PaymentId { get; set; }
        public long? OrderCode { get; set; }
        public int AreaId { get; set; }
        public int AdminUserId { get; set; }
        public int StaffUserId { get; set; }
        public int CustomerUserId { get; set; }
        public int OwnerUserId { get; set; }
        public int AmenityId { get; set; }
        public string AmenityName { get; set; } = string.Empty;
        public double? CarLatitude { get; set; }
        public double? CarLongitude { get; set; }
        public double? DeliveryLatitude { get; set; }
        public double? DeliveryLongitude { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal DeliveryDistanceKm { get; set; }
        public int? BookingHoldId { get; set; }
        public string? CheckoutUrl { get; set; }
        public int? CameraGateEventId { get; set; }
        public int? MismatchedCameraGateEventId { get; set; }
    }

    public sealed class PlaywrightUserCredential
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
