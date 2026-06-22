using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Data;

public static class InternalPortalDemoSeeder
{
    private const string DemoMarker = "DEMO-SEED:";

    public static async Task SeedAsync(ApplicationDbContext context, IPasswordHashService passwordHash)
    {
        var admin = await EnsureUserAsync(context, passwordHash, "Admin Demo", "admin@admin.com", "Admin@123", UserRoleConstants.Admin);
        var manager = await EnsureUserAsync(context, passwordHash, "Manager Demo", "manager@demo.com", "Manager@123", UserRoleConstants.Manager);
        var staff = await EnsureUserAsync(context, passwordHash, "Staff Demo", "staff@demo.com", "Staff@123", UserRoleConstants.Staff);
        var customer1 = await EnsureUserAsync(context, passwordHash, "Customer Demo", "customer@demo.com", "Customer@123", UserRoleConstants.Customer);
        var customer2 = await EnsureUserAsync(context, passwordHash, "Customer Demo 2", "customer2@demo.com", "Customer@123", UserRoleConstants.Customer);
        var partner1 = await EnsureUserAsync(context, passwordHash, "Partner Demo", "partner@demo.com", "Partner@123", UserRoleConstants.Customer, isPartner: true);
        var partner2 = await EnsureUserAsync(context, passwordHash, "Partner Demo 2", "partner2@demo.com", "Partner@123", UserRoleConstants.Customer, isPartner: true);

        await EnsureVerifiedLicenseAsync(context, customer1, "790000000101");
        await EnsureVerifiedLicenseAsync(context, customer2, "790000000102");

        var areaId = await context.Areas.AsNoTracking()
            .Where(area => !area.IsDeleted)
            .OrderBy(area => area.AreaID)
            .Select(area => (int?)area.AreaID)
            .FirstOrDefaultAsync();
        if (areaId.HasValue)
        {
            await EnsureAreaAssignmentAsync(context, areaId.Value, manager.UserID, AreaStaffRoleConstants.Manager, admin.UserID);
            await EnsureAreaAssignmentAsync(context, areaId.Value, staff.UserID, AreaStaffRoleConstants.Staff, admin.UserID);
        }

        var cars = await EnsureCarsAsync(context, partner1.UserID, partner2.UserID);
        await EnsureOperationalDemoDataAsync(context, customer1, customer2, cars);
        await context.SaveChangesAsync();
    }

    private static async Task<User> EnsureUserAsync(
        ApplicationDbContext context,
        IPasswordHashService passwordHash,
        string name,
        string email,
        string password,
        string role,
        bool isPartner = false)
    {
        var user = await context.Users.FirstOrDefaultAsync(item => item.Email == email);
        if (user == null)
        {
            user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = passwordHash.HashPassword(password),
                Role = role,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
        }
        else if (!passwordHash.Verify(password, user.PasswordHash).Success)
        {
            user.PasswordHash = passwordHash.HashPassword(password);
        }

        user.Role = role;
        user.IsEmailVerified = true;
        user.IsVerified = true;
        user.VerificationStatus = "Verified";
        user.IsLocked = false;
        user.LockedAt = null;
        user.IsBlacklisted = false;
        user.BlacklistReason = null;
        if (isPartner)
        {
            user.IsPartnerApproved = true;
            user.PartnerApprovedAt ??= DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return user;
    }

    private static async Task EnsureVerifiedLicenseAsync(ApplicationDbContext context, User user, string licenseNumber)
    {
        if (await context.CustomerDrivingLicenses.AnyAsync(item => item.UserID == user.UserID && item.IsApproved && item.IsActive))
            return;

        context.CustomerDrivingLicenses.Add(new CustomerDrivingLicense
        {
            UserID = user.UserID,
            LicenseNumber = licenseNumber,
            LicenseNumberHash = new string((char)('a' + user.UserID % 20), 64),
            LicenseClass = "B2",
            ExpiryDate = DateTime.UtcNow.AddYears(5),
            VerificationStatus = "Verified",
            IsApproved = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task<List<Car>> EnsureCarsAsync(ApplicationDbContext context, int partner1Id, int partner2Id)
    {
        var available = await context.CarStatuses.FirstAsync(status => status.Name == CarStatusConstants.Available);
        var pending = await context.CarStatuses.FirstAsync(status => status.Name == CarStatusConstants.Pending);
        var maintenance = await context.CarStatuses.FirstAsync(status => status.Name == CarStatusConstants.Maintenance);
        var brand = await context.CarBrands.OrderBy(item => item.BrandID).FirstAsync();
        var carType = await context.CarTypes.OrderBy(item => item.CarTypeID).FirstAsync();
        var location = await context.Locations.OrderBy(item => item.LocationID).FirstAsync();
        var selfDrive = await context.ServiceTypes.FirstAsync(item => item.Code == ServiceTypeConstants.SelfDrive);

        var cars = new List<Car>();
        for (var index = 1; index <= 10; index++)
        {
            var plate = $"DEMO-{index:0000}";
            var car = await context.Cars.FirstOrDefaultAsync(item => item.LicensePlate == plate);
            if (car == null)
            {
                car = new Car
                {
                    CarName = $"Demo Self-drive Car {index:00}",
                    BrandID = brand.BrandID,
                    CarTypeID = carType.CarTypeID,
                    LocationID = location.LocationID,
                    ServiceTypeID = selfDrive.ServiceTypeID,
                    OwnerID = index % 2 == 0 ? partner2Id : partner1Id,
                    Seats = index % 3 == 0 ? 7 : 5,
                    LicensePlate = plate,
                    VIN = $"DEMO{index:0000000000000}",
                    PricePerDay = 600_000m + index * 50_000m,
                    PricePerHour = 80_000m + index * 5_000m,
                    FuelType = index % 4 == 0 ? "Electric" : "Gasoline",
                    Transmission = "Automatic",
                    Description = $"{DemoMarker}CAR-{index:00}",
                    ImageURL = "https://images.unsplash.com/photo-1541899481282-d53bffe3c35d?w=800",
                    IsSelfDriveAllowed = true,
                    IsWithDriverAllowed = false,
                    TransportPermitStatus = "NotRequired",
                    ContractSigningMode = CarContractSigningModes.PhysicalSelfManaged,
                    Latitude = 10.7769 + index * 0.001,
                    Longitude = 106.7009 + index * 0.001,
                    CurrentOdometer = index * 1_000,
                    CreatedAt = DateTime.UtcNow.AddDays(-index)
                };
                context.Cars.Add(car);
            }

            if (index == 9)
            {
                car.CarStatusID = pending.CarStatusID;
                car.IsApproved = false;
                car.ApprovalStatus = CarApprovalStatusConstants.Pending;
                car.ApprovalRejectionReason = null;
            }
            else
            {
                car.CarStatusID = index == 10 ? maintenance.CarStatusID : available.CarStatusID;
                car.IsApproved = true;
                car.ApprovalStatus = CarApprovalStatusConstants.Approved;
                car.ApprovalRejectionReason = null;
            }

            cars.Add(car);
        }

        await context.SaveChangesAsync();
        return cars;
    }

    private static async Task EnsureOperationalDemoDataAsync(
        ApplicationDbContext context,
        User customer1,
        User customer2,
        IReadOnlyList<Car> cars)
    {
        var selfDrive = await context.ServiceTypes.FirstAsync(item => item.Code == ServiceTypeConstants.SelfDrive);
        var customer1LicenseId = await context.CustomerDrivingLicenses
            .Where(item => item.UserID == customer1.UserID && item.IsApproved && item.IsActive)
            .Select(item => item.CustomerDrivingLicenseID)
            .FirstAsync();
        var customer2LicenseId = await context.CustomerDrivingLicenses
            .Where(item => item.UserID == customer2.UserID && item.IsApproved && item.IsActive)
            .Select(item => item.CustomerDrivingLicenseID)
            .FirstAsync();
        var statuses = await context.BookingStatuses.ToDictionaryAsync(item => item.Name);
        var definitions = new[]
        {
            new { Key = "BOOKING-1", User = customer1, LicenseId = customer1LicenseId, Car = cars[0], Status = BookingStatusConstants.Completed, Start = DateTime.UtcNow.AddDays(-40) },
            new { Key = "BOOKING-2", User = customer2, LicenseId = customer2LicenseId, Car = cars[1], Status = BookingStatusConstants.Completed, Start = DateTime.UtcNow.AddDays(-30) },
            new { Key = "BOOKING-3", User = customer1, LicenseId = customer1LicenseId, Car = cars[2], Status = BookingStatusConstants.Completed, Start = DateTime.UtcNow.AddDays(-20) },
            new { Key = "BOOKING-4", User = customer2, LicenseId = customer2LicenseId, Car = cars[3], Status = BookingStatusConstants.Pending, Start = DateTime.UtcNow.AddDays(10) },
            new { Key = "BOOKING-5", User = customer1, LicenseId = customer1LicenseId, Car = cars[4], Status = BookingStatusConstants.Confirmed, Start = DateTime.UtcNow.AddDays(15) }
        };

        var bookings = new List<Booking>();
        foreach (var definition in definitions)
        {
            var marker = DemoMarker + definition.Key;
            var booking = await context.Bookings.FirstOrDefaultAsync(item => item.AdminNotes == marker);
            if (booking == null)
            {
                booking = new Booking
                {
                    UserID = definition.User.UserID,
                    CarID = definition.Car.CarID,
                    ServiceTypeID = selfDrive.ServiceTypeID,
                    CustomerDrivingLicenseID = definition.LicenseId,
                    StartDate = definition.Start,
                    EndDate = definition.Start.AddDays(2),
                    PickupLocationID = definition.Car.LocationID,
                    ReturnLocationID = definition.Car.LocationID,
                    TotalPrice = definition.Car.PricePerDay * 2,
                    TotalHours = 48,
                    BookingStatusID = statuses[definition.Status].BookingStatusID,
                    DepositAmount = definition.Car.DefaultDeposit,
                    DepositStatus = definition.Status == BookingStatusConstants.Completed
                        ? DepositStatusConstants.Refunded
                        : DepositStatusConstants.Pending,
                    ContractTypeCode = "SELF_DRIVE_CONTRACT",
                    AdminNotes = marker,
                    CreatedAt = definition.Start.AddDays(-2)
                };
                context.Bookings.Add(booking);
                await context.SaveChangesAsync();
            }

            bookings.Add(booking);
        }

        foreach (var booking in bookings.Take(3))
        {
            if (!await context.Payments.AnyAsync(item => item.BookingID == booking.BookingID))
            {
                context.Payments.Add(new Payment
                {
                    BookingID = booking.BookingID,
                    Amount = booking.TotalPrice,
                    PaymentMethod = "MockPayment",
                    PaymentStatus = PaymentStatusConstants.Paid,
                    PaymentDate = booking.CreatedAt.AddMinutes(5)
                });
            }

            if (!await context.Reviews.AnyAsync(item => item.UserID == booking.UserID && item.CarID == booking.CarID))
            {
                context.Reviews.Add(new Review
                {
                    UserID = booking.UserID,
                    CarID = booking.CarID,
                    Rating = 5,
                    Comment = $"{DemoMarker} Verified completed-rental review.",
                    IsApproved = true,
                    CreatedAt = booking.EndDate.AddDays(1)
                });
            }
        }

        var maintenanceCar = cars[9];
        for (var index = 1; index <= 2; index++)
        {
            var marker = $"{DemoMarker}MAINTENANCE-{index}";
            if (!await context.MaintenanceLogs.AnyAsync(item => item.Description == marker))
            {
                context.MaintenanceLogs.Add(new MaintenanceLog
                {
                    CarID = maintenanceCar.CarID,
                    ServiceDate = DateTime.UtcNow.AddDays(-index),
                    Description = marker,
                    Cost = 500_000m * index,
                    OdometerAtService = maintenanceCar.CurrentOdometer + index * 100,
                    ServiceCenter = "Demo Service Center",
                    CreatedAt = DateTime.UtcNow.AddDays(-index)
                });
            }
        }

        if (!await context.VehicleTrackings.AnyAsync(item => item.CarID == cars[0].CarID))
        {
            context.VehicleTrackings.Add(new VehicleTracking
            {
                CarID = cars[0].CarID,
                Latitude = cars[0].Latitude ?? 10.7769,
                Longitude = cars[0].Longitude ?? 106.7009,
                Speed = 0,
                EngineStatus = "Off",
                Source = "MockGps",
                DeviceLabel = "Demo GPS fixture",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private static async Task EnsureAreaAssignmentAsync(
        ApplicationDbContext context,
        int areaId,
        int userId,
        string roleInArea,
        int assignedBy)
    {
        if (await context.AreaStaffs.AnyAsync(item => item.AreaID == areaId && item.UserID == userId && !item.IsDeleted))
            return;

        context.AreaStaffs.Add(new AreaStaff
        {
            AreaID = areaId,
            UserID = userId,
            RoleInArea = roleInArea,
            AssignedBy = assignedBy,
            AssignedAt = DateTime.UtcNow
        });
    }
}
