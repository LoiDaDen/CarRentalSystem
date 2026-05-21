using BCrypt.Net;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Data
{
    public static class CypressTestDataSeeder
    {
        public const string Password = "Cypress@123";

        public static async Task<CypressSeedSummary> SeedAsync(ApplicationDbContext db)
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

            return new CypressSeedSummary
            {
                ResetAtUtc = DateTime.UtcNow,
                Users = new Dictionary<string, CypressUserCredential>
                {
                    ["admin"] = Credential(users.Admin),
                    ["manager"] = Credential(users.Manager),
                    ["staff"] = Credential(users.Staff),
                    ["viewer"] = Credential(users.Viewer),
                    ["customer"] = Credential(users.CustomerVerified),
                    ["customerUnverified"] = Credential(users.CustomerUnverified),
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

        public static async Task<CypressScenarioSummary> SeedScenarioAsync(ApplicationDbContext db, string? scenarioName)
        {
            if (!await db.Users.AnyAsync(u => u.Email == "customer@cypress.local"))
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

            return new CypressScenarioSummary
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
                db.CarTypes.Add(new CarType { Name = "Sedan", Description = "Cypress sedan fixture" });

            if (!await db.ContractTypes.AnyAsync(c => c.IsActive))
            {
                db.ContractTypes.Add(new ContractType
                {
                    Name = "1 Month Cypress",
                    Description = "Cypress long-term rental fixture",
                    MinDurationDays = 1,
                    MaxDurationDays = 365,
                    DiscountPercent = 0,
                    IsActive = true
                });
            }

            var location = await db.Locations.FirstOrDefaultAsync(l => l.Name == "Cypress Ho Chi Minh");
            if (location == null)
            {
                location = new Location { Name = "Cypress Ho Chi Minh", Address = "Cypress Test Depot" };
                db.Locations.Add(location);
            }

            var brand = await db.CarBrands.FirstOrDefaultAsync(b => b.Name == "Toyota");
            if (brand == null)
            {
                brand = new CarBrand { Name = "Toyota" };
                db.CarBrands.Add(brand);
            }

            await db.SaveChangesAsync();

            if (!await db.CarModels.AnyAsync(m => m.BrandID == brand.BrandID && m.Name == "Cypress Vios"))
            {
                db.CarModels.Add(new CarModel
                {
                    BrandID = brand.BrandID,
                    Name = "Cypress Vios",
                    SeatCount = 5,
                    FuelType = "Gasoline",
                    Transmission = "Automatic"
                });
            }

            await db.SaveChangesAsync();

            if (!await db.Cars.AnyAsync(c => c.LicensePlate == "CYP-READY"))
            {
                var availableStatus = await db.CarStatuses.FirstAsync(s => s.Name == CarStatusConstants.Available);
                var type = await db.CarTypes.OrderBy(t => t.CarTypeID).FirstAsync();
                var selfDrive = await db.ServiceTypes.FirstAsync(s => s.Code == ServiceTypeConstants.SelfDrive);

                db.Cars.Add(new Car
                {
                    CarName = "Cypress Ready Car",
                    BrandID = brand.BrandID,
                    CarTypeID = type.CarTypeID,
                    CarStatusID = availableStatus.CarStatusID,
                    LocationID = location.LocationID,
                    ServiceTypeID = selfDrive.ServiceTypeID,
                    Seats = 5,
                    LicensePlate = "CYP-READY",
                    VIN = "CYPREADYVIN00001",
                    PricePerDay = 800000m,
                    PricePerHour = 100000m,
                    FuelType = "Gasoline",
                    Transmission = "Automatic",
                    Description = "Cypress available booking fixture.",
                    IsApproved = true,
                    ImageURL = "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800",
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                });
                await db.SaveChangesAsync();
            }
        }

        private static CypressUserCredential Credential(User user) =>
            new() { Email = user.Email, Password = Password, Role = user.Role, UserId = user.UserID };

        private static async Task<CypressUsers> SeedUsersAsync(ApplicationDbContext db)
        {
            var admin = await UpsertUserAsync(db, "Admin Cypress", "admin@admin.com", UserRoleConstants.Admin, true);
            var manager = await UpsertUserAsync(db, "Manager Cypress", "manager@cypress.local", UserRoleConstants.Manager, true);
            var staff = await UpsertUserAsync(db, "Staff Cypress", "staff@cypress.local", UserRoleConstants.Staff, true);
            var viewer = await UpsertUserAsync(db, "Viewer Cypress", "viewer@cypress.local", UserRoleConstants.Viewer, true);
            var customer = await UpsertUserAsync(db, "Customer Verified Cypress", "customer@cypress.local", UserRoleConstants.Customer, true);
            var unverified = await UpsertUserAsync(db, "Customer Pending Cypress", "customer-unverified@cypress.local", UserRoleConstants.Customer, false);
            var owner = await UpsertUserAsync(db, "Owner Cypress", "owner@cypress.local", UserRoleConstants.Customer, true);

            customer.CitizenIdHash = "cypress-citizen-hash";
            customer.DrivingLicenseHash = "cypress-license-hash";
            customer.DrivingLicenseUrl = "/secure/cypress/license-front.jpg";
            customer.DrivingLicenseBackUrl = "/secure/cypress/license-back.jpg";
            customer.CitizenId = "encrypted-cypress-citizen";
            customer.DrivingLicenseNumber = "encrypted-cypress-license";
            customer.VerificationStatus = "Verified";
            customer.IsVerified = true;

            owner.IsPartnerApproved = true;
            owner.PartnerApprovedAt = DateTime.UtcNow.AddDays(-5);
            owner.PartnerAddress = "Cypress Owner Depot";

            await db.SaveChangesAsync();
            return new CypressUsers(admin, manager, staff, viewer, customer, unverified, owner);
        }

        private static async Task<CypressUsers> LoadUsersAsync(ApplicationDbContext db)
        {
            return new CypressUsers(
                await db.Users.FirstAsync(u => u.Email == "admin@admin.com"),
                await db.Users.FirstAsync(u => u.Email == "manager@cypress.local"),
                await db.Users.FirstAsync(u => u.Email == "staff@cypress.local"),
                await db.Users.FirstAsync(u => u.Email == "viewer@cypress.local"),
                await db.Users.FirstAsync(u => u.Email == "customer@cypress.local"),
                await db.Users.FirstAsync(u => u.Email == "customer-unverified@cypress.local"),
                await db.Users.FirstAsync(u => u.Email == "owner@cypress.local"));
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
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                    Role = role,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                };
                db.Users.Add(user);
            }

            user.Name = name;
            user.Role = role;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
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
            if (await db.CustomerDrivingLicenses.AnyAsync(l => l.UserID == userId && l.LicenseNumberHash == "cypress-license-hash"))
                return;

            db.CustomerDrivingLicenses.Add(new CustomerDrivingLicense
            {
                UserID = userId,
                LicenseNumber = "encrypted-cypress-license",
                LicenseNumberHash = "cypress-license-hash",
                LicenseClass = "B2",
                ExpiryDate = DateTime.UtcNow.AddYears(5),
                FrontImageUrl = "/secure/cypress/license-front.jpg",
                BackImageUrl = "/secure/cypress/license-back.jpg",
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
                    Version = "2026.05-cypress",
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
                    Name = "Cypress Ho Chi Minh",
                    Code = "CYP-HCM",
                    Province = "Ho Chi Minh",
                    Address = "Cypress Test Depot",
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

        private static async Task<CypressCars> SeedCarsAsync(ApplicationDbContext db, int ownerId)
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

            var pendingOwnerCar = await db.Cars.FirstOrDefaultAsync(c => c.LicensePlate == "CYP-00001");
            if (pendingOwnerCar == null)
            {
                pendingOwnerCar = new Car
                {
                    CarName = "Cypress Owner Pending Car",
                    BrandID = brand.BrandID,
                    CarTypeID = type.CarTypeID,
                    CarStatusID = pendingStatus.CarStatusID,
                    LocationID = location.LocationID,
                    ServiceTypeID = selfDrive.ServiceTypeID,
                    Seats = 5,
                    LicensePlate = "CYP-00001",
                    VIN = "CYPRESSVIN0000001",
                    PricePerDay = 700000m,
                    PricePerHour = 90000m,
                    FuelType = "Gasoline",
                    Transmission = "Automatic",
                    Description = "Cypress owner approval fixture.",
                    OwnerID = ownerId,
                    IsApproved = false,
                    ImageURL = "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800"
                };
                db.Cars.Add(pendingOwnerCar);
                await db.SaveChangesAsync();
            }

            return new CypressCars(firstAvailable, pendingOwnerCar);
        }

        private static async Task ConfigureDeliveryCarAsync(ApplicationDbContext db, Car car, int ownerId)
        {
            car.OwnerID = ownerId;
            car.CarAddress = "Cypress Owner Depot, Quan 1, TP Ho Chi Minh";
            car.Latitude = 10.7769;
            car.Longitude = 106.7009;
            car.DeliveryEnabled = true;
            car.MaxDeliveryDistanceKm = 40m;
            car.FreeDeliveryRadiusKm = 3m;
            car.DeliveryFeePerKm = 7000m;
            car.ImageURL ??= "https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=800";
            await db.SaveChangesAsync();
        }

        private static async Task<Booking> EnsureDeliveryTrackingBookingAsync(ApplicationDbContext db, CypressUsers users, Car car)
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
            booking.DeliveryAddress = "Cypress Customer Dropoff, Nguyen Hue, Quan 1, TP Ho Chi Minh";
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
                    PayOSDescription = "Cypress delivery tracking payment fixture"
                });
                await db.SaveChangesAsync();
            }

            return booking;
        }

        private static async Task<CarAmenity> EnsureSandboxAmenityAsync(ApplicationDbContext db, int carId)
        {
            var amenity = await db.CarAmenities.FirstOrDefaultAsync(a => a.Code == "CYPRESS_SANDBOX_FEATURE");
            if (amenity == null)
            {
                amenity = new CarAmenity
                {
                    Code = "CYPRESS_SANDBOX_FEATURE",
                    Name = "Cypress Sandbox Feature",
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

        private static async Task<Booking> SeedOperationalRecordsAsync(ApplicationDbContext db, CypressUsers users, Car car, Area area)
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
                PayOSDescription = "Cypress payment fixture"
            });

            db.PrivacyRequests.Add(new PrivacyRequest
            {
                UserID = users.CustomerVerified.UserID,
                RequestType = PrivacyRequestTypeConstants.Export,
                Status = PrivacyRequestStatusConstants.PendingReview,
                Reason = "Cypress export request"
            });

            db.SupportTickets.Add(new SupportTicket
            {
                UserID = users.CustomerVerified.UserID,
                BookingID = booking.BookingID,
                TicketType = SupportTicketTypeConstants.Booking,
                Priority = SupportTicketPriorityConstants.P2,
                Subject = "Cypress support ticket",
                Description = "Seeded ticket for Cypress support tests."
            });

            db.ContactMessages.Add(new ContactMessage
            {
                Name = "Cypress Contact",
                Email = "contact@cypress.local",
                Subject = "Cypress contact",
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
                CreatedBy = "Cypress"
            });

            db.SettlementReviews.Add(new SettlementReview
            {
                BookingID = booking.BookingID,
                ReviewType = SettlementReviewTypeConstants.Cleaning,
                Amount = 50000m,
                Reason = "Cypress seeded settlement review.",
                RequestedByUserID = users.Staff.UserID
            });

            await db.SaveChangesAsync();
            return booking;
        }

        private sealed record CypressUsers(
            User Admin,
            User Manager,
            User Staff,
            User Viewer,
            User CustomerVerified,
            User CustomerUnverified,
            User Owner);

        private sealed record CypressCars(Car FirstAvailableCar, Car PendingOwnerCar);
    }

    public sealed class CypressSeedSummary
    {
        public DateTime ResetAtUtc { get; set; }
        public Dictionary<string, CypressUserCredential> Users { get; set; } = new();
        public int FirstAvailableCarId { get; set; }
        public int PendingOwnerCarId { get; set; }
        public int BookingId { get; set; }
        public int PaymentId { get; set; }
        public int PrivacyRequestId { get; set; }
        public int AreaId { get; set; }
        public int ContractTypeId { get; set; }
    }

    public sealed class CypressScenarioSummary
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
    }

    public sealed class CypressUserCredential
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
