using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IBookingBusinessRuleService
    {
        Task<int> GetServiceTypeIdAsync(string serviceTypeCode);
        Task<CustomerDrivingLicense?> GetValidCustomerDrivingLicenseAsync(int userId);
        Task ValidateSelfDriveAsync(Booking booking, decimal driverFee = 0m);
        Task ValidateWithDriverTripAsync(Booking booking, TripBooking? trip);
    }

    public class BookingBusinessRuleService : IBookingBusinessRuleService
    {
        private readonly ApplicationDbContext _context;

        public BookingBusinessRuleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetServiceTypeIdAsync(string serviceTypeCode)
        {
            var serviceType = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Code == serviceTypeCode && s.IsActive);
            if (serviceType == null)
                throw new InvalidOperationException($"ServiceType '{serviceTypeCode}' chưa được cấu hình.");

            return serviceType.ServiceTypeID;
        }

        public async Task<CustomerDrivingLicense?> GetValidCustomerDrivingLicenseAsync(int userId)
        {
            var license = await _context.CustomerDrivingLicenses
                .Where(l => l.UserID == userId &&
                            l.IsActive &&
                            l.IsApproved &&
                            l.VerificationStatus == "Verified" &&
                            (!l.ExpiryDate.HasValue || l.ExpiryDate.Value.Date >= DateTime.UtcNow.Date))
                .OrderByDescending(l => l.UpdatedAt ?? l.CreatedAt)
                .FirstOrDefaultAsync();

            if (license != null)
                return license;

            return null;
        }

        public async Task ValidateSelfDriveAsync(Booking booking, decimal driverFee = 0m)
        {
            var selfDriveId = await GetServiceTypeIdAsync(ServiceTypeConstants.SelfDrive);
            booking.ServiceTypeID ??= selfDriveId;

            if (booking.ServiceTypeID != selfDriveId)
                return;

            if (booking.DriverID != null || booking.DriverBookingID != null)
                throw new InvalidOperationException("Đơn thuê xe tự lái không được gán tài xế.");

            if (driverFee > 0)
                throw new InvalidOperationException("Đơn thuê xe tự lái không được có phí tài xế.");

            if (booking.CustomerDrivingLicenseID == null)
                throw new InvalidOperationException("Khách thuê xe tự lái phải cung cấp giấy phép lái xe hợp lệ.");

            booking.ContractTypeCode = "SELF_DRIVE_CONTRACT";
        }

        public async Task ValidateWithDriverTripAsync(Booking booking, TripBooking? trip)
        {
            var withDriverId = await GetServiceTypeIdAsync(ServiceTypeConstants.WithDriver);
            if (booking.ServiceTypeID != withDriverId)
                return;

            if (booking.DriverID != null || booking.DriverBookingID != null)
                throw new InvalidOperationException("Dịch vụ xe có tài xế không còn sử dụng module tài xế riêng.");

            if (trip == null)
                throw new InvalidOperationException("Dịch vụ xe có tài xế phải có thông tin chuyến.");

            if (string.IsNullOrWhiteSpace(trip.PickupLocation))
                throw new InvalidOperationException("Vui lòng nhập điểm đón.");

            var tripType = await _context.TripTypes.FirstOrDefaultAsync(t => t.TripTypeID == trip.TripTypeID);
            if (tripType == null)
                throw new InvalidOperationException("Loại chuyến không hợp lệ.");

            if (tripType.Code != TripTypeConstants.HourlyRental && string.IsNullOrWhiteSpace(trip.DropoffLocation))
                throw new InvalidOperationException("Vui lòng nhập điểm đến.");

            if (tripType.Code == TripTypeConstants.IntercityRoundTrip && trip.ReturnTime == null)
                throw new InvalidOperationException("Chuyến liên tỉnh hai chiều phải có thời gian về.");
        }
    }
}

