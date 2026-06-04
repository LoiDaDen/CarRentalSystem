using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.BackgroundJobs
{
    public class ExpireBookingHoldsJob
    {
        private readonly ApplicationDbContext _context;
        private readonly IReservationService _reservationService;
        private readonly ILogger<ExpireBookingHoldsJob> _logger;

        public ExpireBookingHoldsJob(
            ApplicationDbContext context,
            IReservationService reservationService,
            ILogger<ExpireBookingHoldsJob> logger)
        {
            _context = context;
            _reservationService = reservationService;
            _logger = logger;
        }

        public async Task Execute()
        {
            var now = DateTime.UtcNow;
            var expiredHolds = await _reservationService.ExpireHoldsAsync(now);

            var expiredStatus = await _context.BookingStatuses
                .FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Expired);
            if (expiredStatus == null)
            {
                expiredStatus = new BookingStatus { Name = BookingStatusConstants.Expired };
                _context.BookingStatuses.Add(expiredStatus);
                await _context.SaveChangesAsync();
            }

            var expiredBookings = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Where(b =>
                    b.BookingStatus != null &&
                    b.BookingStatus.Name == BookingStatusConstants.Unpaid &&
                    b.CreatedAt <= now.AddMinutes(-15) &&
                    !_context.BookingHolds.Any(h =>
                        h.BookingID == b.BookingID &&
                        h.Status == BookingHoldStatusConstants.Active &&
                        h.ExpiresAt > now))
                .ToListAsync();

            foreach (var booking in expiredBookings)
            {
                booking.BookingStatusID = expiredStatus.BookingStatusID;
                booking.DepositStatus = DepositStatusConstants.Unpaid;
                booking.AdminNotes = AppendNote(booking.AdminNotes, "System expired unpaid booking after hold timeout.");
            }

            if (expiredBookings.Count > 0)
                await _context.SaveChangesAsync();

            if (expiredHolds > 0 || expiredBookings.Count > 0)
                _logger.LogInformation("Expired {HoldCount} holds and {BookingCount} unpaid bookings.", expiredHolds, expiredBookings.Count);
        }

        private static string AppendNote(string? existing, string note)
        {
            return string.IsNullOrWhiteSpace(existing)
                ? note
                : existing + Environment.NewLine + note;
        }
    }
}
