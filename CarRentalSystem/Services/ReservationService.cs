using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public record ReservationCheckResult(bool IsAvailable, string? Reason);
    public record BookingHoldResult(bool IsSuccess, BookingHold? Hold, string? ErrorMessage);

    public interface IReservationService
    {
        Task<ReservationCheckResult> CheckCarAvailabilityAsync(int carId, DateTime startDate, DateTime endDate, int? ignoreBookingId = null, int? ignoreHoldId = null);
        Task<BookingHoldResult> CreateHoldAsync(int userId, int carId, DateTime startDate, DateTime endDate, object? quoteSnapshot = null, int? transactionId = null);
        Task MarkHoldConvertedAsync(int holdId, int bookingId);
        Task ReleaseHoldAsync(int holdId, string? reason = null);
        Task<int> ExpireHoldsAsync(DateTime? now = null);
        Task<bool> IsDriverAvailableAsync(int driverId, DateTime startDate, DateTime endDate, int? ignoreBookingId = null);
    }

    public class ReservationService : IReservationService
    {
        private static readonly TimeSpan DefaultHoldDuration = TimeSpan.FromMinutes(15);
        private readonly ApplicationDbContext _context;

        public ReservationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReservationCheckResult> CheckCarAvailabilityAsync(int carId, DateTime startDate, DateTime endDate, int? ignoreBookingId = null, int? ignoreHoldId = null)
        {
            if (endDate <= startDate)
                return new ReservationCheckResult(false, "End date must be after start date.");

            var now = DateTime.UtcNow;
            var blockingStatuses = BookingStatusConstants.AvailabilityBlocking;

            var hasBookingOverlap = await _context.Bookings
                .Include(b => b.BookingStatus)
                .AnyAsync(b =>
                    b.CarID == carId &&
                    (!ignoreBookingId.HasValue || b.BookingID != ignoreBookingId.Value) &&
                    b.BookingStatus != null &&
                    blockingStatuses.Contains(b.BookingStatus.Name) &&
                    startDate < b.EndDate &&
                    endDate > b.StartDate);

            if (hasBookingOverlap)
                return new ReservationCheckResult(false, "Car is already reserved for the selected time.");

            var hasHoldOverlap = await _context.BookingHolds
                .AnyAsync(h =>
                    h.CarID == carId &&
                    (!ignoreHoldId.HasValue || h.BookingHoldID != ignoreHoldId.Value) &&
                    h.Status == BookingHoldStatusConstants.Active &&
                    h.ExpiresAt > now &&
                    startDate < h.EndDate &&
                    endDate > h.StartDate);

            if (hasHoldOverlap)
                return new ReservationCheckResult(false, "Car is temporarily held by another checkout.");

            return new ReservationCheckResult(true, null);
        }

        public async Task<BookingHoldResult> CreateHoldAsync(int userId, int carId, DateTime startDate, DateTime endDate, object? quoteSnapshot = null, int? transactionId = null)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var availability = await CheckCarAvailabilityAsync(carId, startDate, endDate);
            if (!availability.IsAvailable)
            {
                await transaction.RollbackAsync();
                return new BookingHoldResult(false, null, availability.Reason);
            }

            var hold = new BookingHold
            {
                UserID = userId,
                CarID = carId,
                StartDate = startDate,
                EndDate = endDate,
                ExpiresAt = DateTime.UtcNow.Add(DefaultHoldDuration),
                Status = BookingHoldStatusConstants.Active,
                TransactionID = transactionId,
                QuoteSnapshotJson = quoteSnapshot == null ? null : JsonSerializer.Serialize(quoteSnapshot)
            };

            _context.BookingHolds.Add(hold);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new BookingHoldResult(true, hold, null);
        }

        public async Task MarkHoldConvertedAsync(int holdId, int bookingId)
        {
            var hold = await _context.BookingHolds.FirstOrDefaultAsync(h => h.BookingHoldID == holdId);
            if (hold == null) return;

            hold.BookingID = bookingId;
            hold.Status = BookingHoldStatusConstants.Converted;
            hold.ConvertedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task ReleaseHoldAsync(int holdId, string? reason = null)
        {
            var hold = await _context.BookingHolds.FirstOrDefaultAsync(h => h.BookingHoldID == holdId);
            if (hold == null || hold.Status != BookingHoldStatusConstants.Active) return;

            hold.Status = BookingHoldStatusConstants.Released;
            hold.ReleasedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<int> ExpireHoldsAsync(DateTime? now = null)
        {
            var cutoff = now ?? DateTime.UtcNow;
            return await _context.BookingHolds
                .Where(h => h.Status == BookingHoldStatusConstants.Active && h.ExpiresAt <= cutoff)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.Status, BookingHoldStatusConstants.Expired)
                    .SetProperty(h => h.ReleasedAt, cutoff));
        }

        public async Task<bool> IsDriverAvailableAsync(int driverId, DateTime startDate, DateTime endDate, int? ignoreBookingId = null)
        {
            if (endDate <= startDate) return false;

            var blockingStatuses = new[]
            {
                BookingStatusConstants.Pending,
                BookingStatusConstants.Confirmed,
                BookingStatusConstants.Active,
                BookingStatusConstants.Unpaid
            };

            return !await _context.DriverBookings
                .Include(db => db.Booking)
                    .ThenInclude(b => b!.BookingStatus)
                .AnyAsync(db =>
                    db.DriverID == driverId &&
                    db.Booking != null &&
                    (!ignoreBookingId.HasValue || db.BookingID != ignoreBookingId.Value) &&
                    db.Status != BookingStatusConstants.Cancelled &&
                    db.Booking.BookingStatus != null &&
                    blockingStatuses.Contains(db.Booking.BookingStatus.Name) &&
                    startDate < db.Booking.EndDate &&
                    endDate > db.Booking.StartDate);
        }
    }
}
