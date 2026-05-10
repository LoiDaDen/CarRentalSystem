using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Services
{
    public interface IBookingService
    {
        Task<IEnumerable<Booking>> GetAllBookingsAsync();
        Task<IEnumerable<Booking>> GetUserBookingsAsync(int userId);
        Task<Booking> GetBookingByIdAsync(int id);
        Task CreateBookingAsync(Booking booking);
        void UpdateBooking(Booking booking);
        Task UpdateBookingStatusAsync(int id, string status);
        Task StartTripAsync(int bookingId);
        Task EndTripAsync(int bookingId);
        Task ConfirmPickupAsync(int bookingId, string? notes);
        Task CancelBookingAsync(int bookingId, string reason);
        Task ProcessPaymentAsync(Payment payment);
        Task CreateContractAsync(RentalContract contract);
        Task CompleteBookingAsync(int bookingId);
    }

    public class BookingService : IBookingService
    {
        private readonly IRepository<BookingStatus> _statusRepo;
        private readonly IRepository<Car> _carRepo;
        private readonly IRepository<CarStatus> _carStatusRepo;
        private readonly IRepository<Booking> _bookingRepo;
        private readonly IRepository<Payment> _paymentRepo;
        private readonly IRepository<RentalContract> _contractRepo;

        public BookingService(IRepository<Booking> bookingRepo, IRepository<Payment> paymentRepo, 
            IRepository<RentalContract> contractRepo, IRepository<BookingStatus> statusRepo,
            IRepository<Car> carRepo, IRepository<CarStatus> carStatusRepo)
        {
            _bookingRepo = bookingRepo;
            _paymentRepo = paymentRepo;
            _contractRepo = contractRepo;
            _statusRepo = statusRepo;
            _carRepo = carRepo;
            _carStatusRepo = carStatusRepo;
        }

        public async Task<IEnumerable<Booking>> GetAllBookingsAsync()
        {
            var bookings = await _bookingRepo.Query()
                .Include(b => b.Car)
                    .ThenInclude(c => c!.CarBrand)
                .Include(b => b.User)
                .Include(b => b.BookingStatus)
                .Include(b => b.DigitalContract)
                .Include(b => b.ContractType)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return await AutoCancelExpiredBookings(bookings);
        }

        public async Task<IEnumerable<Booking>> GetUserBookingsAsync(int userId)
        {
            var bookings = await _bookingRepo.Query()
                .Include(b => b.Car).ThenInclude(c => c.CarBrand)
                .Include(b => b.Car).ThenInclude(c => c.Location)
                .Include(b => b.BookingStatus)
                .Include(b => b.DigitalContract)
                .Where(b => b.UserID == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
                
            return await AutoCancelExpiredBookings(bookings);
        }
        
        private async Task<IEnumerable<Booking>> AutoCancelExpiredBookings(List<Booking> bookings)
        {
            bool hasChanges = false;
            var cancelStatus = await _statusRepo.Query()
                .FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Cancelled);
            if (cancelStatus == null) return bookings;

            foreach (var b in bookings)
            {
                if ((b.BookingStatus?.Name == BookingStatusConstants.Draft || b.BookingStatus?.Name == BookingStatusConstants.Pending) &&
                    b.StartDate.AddHours(1) < DateTime.UtcNow)
                {
                    b.BookingStatusID = cancelStatus.BookingStatusID;
                    b.BookingStatus = cancelStatus;
                    b.AdminNotes = (string.IsNullOrEmpty(b.AdminNotes) ? "" : b.AdminNotes + "\n") +
                                   $"Hệ thống tự động hủy đơn lúc {DateTime.UtcNow:dd/MM/yyyy HH:mm} do quá giờ nhận xe 1 tiếng mà chưa được xác nhận.";
                    _bookingRepo.Update(b);
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await _bookingRepo.SaveChangesAsync();
            }

            return bookings;
        }

        public async Task<Booking> GetBookingByIdAsync(int id)
        {
            return await _bookingRepo.Query()
                .Include(b => b.Car).ThenInclude(c => c.CarBrand)
                .Include(b => b.Car).ThenInclude(c => c.Location)
                .Include(b => b.User)
                .Include(b => b.Payment)
                .Include(b => b.RentalContract)
                .Include(b => b.BookingStatus)
                .FirstOrDefaultAsync(b => b.BookingID == id);
        }

        public async Task CreateBookingAsync(Booking booking)
        {
            var status = await _statusRepo.Query().FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Draft);
            if (status == null) status = await _statusRepo.Query().FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Pending);

            if (status != null) booking.BookingStatusID = status.BookingStatusID;

            await _bookingRepo.AddAsync(booking);
            await _bookingRepo.SaveChangesAsync();
        }

        public void UpdateBooking(Booking booking)
        {
            _bookingRepo.Update(booking);
        }

        public async Task CompleteBookingAsync(int bookingId)
        {
            var booking = await _bookingRepo.Query()
                .Include(b => b.Car)
                .Include(b => b.RentalContract)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null) return;

            if (booking.RentalContract != null)
            {
                var contract = booking.RentalContract;
                var totalDays = (int)(booking.EndDate - booking.StartDate).TotalDays;
                if (totalDays <= 0) totalDays = 1;
                contract.CalculateFees(totalDays);
            }
            
            var status = await _statusRepo.Query().FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Completed);
            if (status != null) booking.BookingStatusID = status.BookingStatusID;

            // Make car available again
            if (booking.Car != null)
            {
                var availableStatus = await _carStatusRepo.Query().FirstOrDefaultAsync(s => s.Name == CarStatusConstants.Available);
                if (availableStatus != null)
                {
                    booking.Car.CarStatusID = availableStatus.CarStatusID;
                    _carRepo.Update(booking.Car);
                    await _carRepo.SaveChangesAsync();
                }
            }

            _bookingRepo.Update(booking);
            await _bookingRepo.SaveChangesAsync();
        }

        public async Task ConfirmPickupAsync(int bookingId, string? notes)
        {
            var booking = await _bookingRepo.Query()
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null) return;

            var activeStatus = await _statusRepo.Query().FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Active);
            if (activeStatus != null) booking.BookingStatusID = activeStatus.BookingStatusID;

            if (!string.IsNullOrEmpty(notes)) booking.AdminNotes = notes;

            // Mark car as Rented
            if (booking.Car != null)
            {
                var rentedStatus = await _carStatusRepo.Query().FirstOrDefaultAsync(s => s.Name == CarStatusConstants.Rented);
                if (rentedStatus != null)
                {
                    booking.Car.CarStatusID = rentedStatus.CarStatusID;
                    _carRepo.Update(booking.Car);
                    await _carRepo.SaveChangesAsync();
                }
            }

            _bookingRepo.Update(booking);
            await _bookingRepo.SaveChangesAsync();
        }

        public async Task CancelBookingAsync(int bookingId, string reason)
        {
            var booking = await _bookingRepo.Query()
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
            if (booking == null) return;

            var cancelledStatus = await _statusRepo.Query().FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Cancelled);
            if (cancelledStatus != null) booking.BookingStatusID = cancelledStatus.BookingStatusID;

            booking.CancelReason = reason;
            booking.DepositStatus = DepositStatusConstants.Refunded;

            // Reset car status back to Available when booking is cancelled
            if (booking.Car != null)
            {
                var availableStatus = await _carStatusRepo.Query().FirstOrDefaultAsync(s => s.Name == CarStatusConstants.Available);
                if (availableStatus != null)
                {
                    booking.Car.CarStatusID = availableStatus.CarStatusID;
                    _carRepo.Update(booking.Car);
                    await _carRepo.SaveChangesAsync();
                }
            }

            _bookingRepo.Update(booking);
            await _bookingRepo.SaveChangesAsync();
        }

        public async Task UpdateBookingStatusAsync(int id, string statusName)
        {
            var status = await _statusRepo.Query().FirstOrDefaultAsync(s => s.Name == statusName);
            if (status == null) return;

            // Use ExecuteUpdateAsync for atomic update to prevent race conditions
            await _bookingRepo.Query()
                .Where(b => b.BookingID == id)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.BookingStatusID, status.BookingStatusID));
        }

        public async Task StartTripAsync(int bookingId)
        {
            var booking = await _bookingRepo.GetByIdAsync(bookingId);
            if (booking != null)
            {
                booking.ActualPickupDate = DateTime.Now;
                var status = await _statusRepo.Query().FirstOrDefaultAsync(s => s.Name == "Active");
                if (status != null)
                {
                    booking.BookingStatusID = status.BookingStatusID;
                }
                _bookingRepo.Update(booking);
                await _bookingRepo.SaveChangesAsync();
            }
        }

        public async Task EndTripAsync(int bookingId)
        {
            var booking = await _bookingRepo.GetByIdAsync(bookingId);
            if (booking != null)
            {
                booking.ActualReturnDate = DateTime.Now;
                var status = await _statusRepo.Query().FirstOrDefaultAsync(s => s.Name == "Completed");
                if (status != null)
                {
                    booking.BookingStatusID = status.BookingStatusID;
                }
                _bookingRepo.Update(booking);
                await _bookingRepo.SaveChangesAsync();
            }
        }

        public async Task ProcessPaymentAsync(Payment payment)
        {
            await _paymentRepo.AddAsync(payment);
            await _paymentRepo.SaveChangesAsync();
        }

        public async Task CreateContractAsync(RentalContract contract)
        {
            await _contractRepo.AddAsync(contract);
            await _contractRepo.SaveChangesAsync();
        }
    }
}
