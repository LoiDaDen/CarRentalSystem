using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Data
{
    public static class BusinessReferenceDataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            await SeedServiceTypesAsync(db);
            await SeedTripTypesAsync(db);
            await SeedSurchargesAsync(db);
            await SeedNeedReviewStatusAsync(db);
            await BackfillServiceTypeAsync(db);
            await BackfillCustomerDrivingLicensesAsync(db);
            await MarkLegacyDriverBookingsForReviewAsync(db);
        }

        private static async Task SeedServiceTypesAsync(ApplicationDbContext db)
        {
            var rows = new[]
            {
                new ServiceType { Code = ServiceTypeConstants.SelfDrive, Name = "Thuê xe tự lái", Description = "Khách thuê xe và tự điều khiển xe." },
                new ServiceType { Code = ServiceTypeConstants.WithDriver, Name = "Xe có tài xế", Description = "Dịch vụ vận chuyển khách theo chuyến/lộ trình, có tài xế phục vụ." }
            };

            foreach (var row in rows)
            {
                if (!await db.ServiceTypes.AnyAsync(x => x.Code == row.Code))
                    db.ServiceTypes.Add(row);
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedTripTypesAsync(ApplicationDbContext db)
        {
            var rows = new[]
            {
                new TripType { Code = TripTypeConstants.CityTransfer, Name = "Đưa đón nội thành" },
                new TripType { Code = TripTypeConstants.AirportTransfer, Name = "Đưa đón sân bay" },
                new TripType { Code = TripTypeConstants.IntercityOneWay, Name = "Liên tỉnh một chiều" },
                new TripType { Code = TripTypeConstants.IntercityRoundTrip, Name = "Liên tỉnh hai chiều" },
                new TripType { Code = TripTypeConstants.HourlyRental, Name = "Thuê xe theo giờ" },
                new TripType { Code = TripTypeConstants.DailyRental, Name = "Thuê xe theo ngày" }
            };

            foreach (var row in rows)
            {
                if (!await db.TripTypes.AnyAsync(x => x.Code == row.Code))
                    db.TripTypes.Add(row);
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedSurchargesAsync(ApplicationDbContext db)
        {
            var rows = new[]
            {
                ("NIGHT_FEE", "Phụ phí ban đêm"),
                ("HOLIDAY_FEE", "Phụ phí ngày lễ"),
                ("WAITING_FEE", "Phí chờ"),
                ("TOLL_FEE", "Phí cầu đường/cao tốc"),
                ("OVERNIGHT_FEE", "Phí lưu đêm tài xế"),
                ("EXTRA_KM_FEE", "Phí vượt km"),
                ("EXTRA_HOUR_FEE", "Phí vượt giờ")
            };

            foreach (var (code, name) in rows)
            {
                if (!await db.Surcharges.AnyAsync(x => x.Code == code))
                    db.Surcharges.Add(new Surcharge { Code = code, Name = name });
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedNeedReviewStatusAsync(ApplicationDbContext db)
        {
            if (!await db.BookingStatuses.AnyAsync(s => s.Name == BookingStatusConstants.NeedReview))
            {
                db.BookingStatuses.Add(new BookingStatus { Name = BookingStatusConstants.NeedReview });
                await db.SaveChangesAsync();
            }
        }

        private static async Task BackfillServiceTypeAsync(ApplicationDbContext db)
        {
            var selfDrive = await db.ServiceTypes.FirstAsync(s => s.Code == ServiceTypeConstants.SelfDrive);

            await db.Cars
                .IgnoreQueryFilters()
                .Where(c => c.ServiceTypeID == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.ServiceTypeID, selfDrive.ServiceTypeID)
                    .SetProperty(c => c.IsSelfDriveAllowed, true)
                    .SetProperty(c => c.IsWithDriverAllowed, false)
                    .SetProperty(c => c.TransportPermitStatus, "NotRequired")
                    .SetProperty(c => c.IsApproved, true)
                    .SetProperty(c => c.ApprovalStatus, CarApprovalStatusConstants.Approved));

            await db.Bookings
                .IgnoreQueryFilters()
                .Where(b => b.ServiceTypeID == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.ServiceTypeID, selfDrive.ServiceTypeID)
                    .SetProperty(b => b.ContractTypeCode, "SELF_DRIVE_CONTRACT"));

            await db.CartItems
                .IgnoreQueryFilters()
                .Where(ci => ci.WithDriver || ci.DriverID != null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ci => ci.WithDriver, false)
                    .SetProperty(ci => ci.DriverID, (int?)null));
        }

        private static async Task BackfillCustomerDrivingLicensesAsync(ApplicationDbContext db)
        {
            var users = await db.Users
                .Where(u => u.DrivingLicenseHash != null && !db.CustomerDrivingLicenses.Any(l => l.UserID == u.UserID && l.LicenseNumberHash == u.DrivingLicenseHash))
                .ToListAsync();

            foreach (var user in users)
            {
                db.CustomerDrivingLicenses.Add(new CustomerDrivingLicense
                {
                    UserID = user.UserID,
                    LicenseNumber = user.DrivingLicenseNumber,
                    LicenseNumberHash = user.DrivingLicenseHash,
                    FrontImageUrl = user.DrivingLicenseUrl,
                    BackImageUrl = user.DrivingLicenseBackUrl,
                    VerificationStatus = "Pending",
                    IsApproved = false,
                    IsActive = true
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task MarkLegacyDriverBookingsForReviewAsync(ApplicationDbContext db)
        {
            var needReview = await db.BookingStatuses.FirstAsync(s => s.Name == BookingStatusConstants.NeedReview);
            var terminalStatuses = new[] { BookingStatusConstants.Completed, BookingStatusConstants.Cancelled, BookingStatusConstants.Rejected };

            var legacyBookings = await db.Bookings
                .IgnoreQueryFilters()
                .Include(b => b.BookingStatus)
                .Where(b => (b.DriverBookingID != null || b.DriverID != null) &&
                            (b.BookingStatus == null || !terminalStatuses.Contains(b.BookingStatus.Name)))
                .ToListAsync();

            foreach (var booking in legacyBookings)
            {
                booking.BookingStatusID = needReview.BookingStatusID;
                booking.AdminNotes = AppendReviewNote(booking.AdminNotes);
                booking.DriverID = null;
            }

            await db.SaveChangesAsync();
        }

        private static string AppendReviewNote(string? current)
        {
            const string note = "NEED_REVIEW: Đơn thuê xe tự lái cũ có dữ liệu tài xế/DriverBooking, cần admin rà soát vì nghiệp vụ mới không cho self-drive kèm tài xế.";
            if (!string.IsNullOrWhiteSpace(current) && current.Contains(note, StringComparison.OrdinalIgnoreCase))
                return current;

            return string.IsNullOrWhiteSpace(current) ? note : current + Environment.NewLine + note;
        }
    }
}
