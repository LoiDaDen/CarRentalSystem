using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public record BookingTransitionResult(bool IsSuccess, string? ErrorMessage);

    public interface IBookingTransitionService
    {
        Task<BookingTransitionResult> TransitionAsync(int bookingId, string targetStatus, int? actorUserId, string actorRole, string? reason = null);
        Task<BookingTransitionResult> CancelAsync(int bookingId, int? actorUserId, string actorRole, string? reason);
    }

    public class BookingTransitionService : IBookingTransitionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentLedgerService _ledger;

        public BookingTransitionService(ApplicationDbContext context, IPaymentLedgerService ledger)
        {
            _context = context;
            _ledger = ledger;
        }

        public async Task<BookingTransitionResult> TransitionAsync(int bookingId, string targetStatus, int? actorUserId, string actorRole, string? reason = null)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var booking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Payment)
                .Include(b => b.Car)
                .Include(b => b.DigitalContract)
                .Include(b => b.HandoverRecords)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
                return new BookingTransitionResult(false, "Booking not found.");

            var current = booking.BookingStatus?.Name ?? BookingStatusConstants.Draft;
            var validation = ValidateTransition(booking, current, targetStatus);
            if (!validation.IsSuccess)
            {
                await transaction.RollbackAsync();
                return validation;
            }

            var status = await GetOrCreateStatusAsync(targetStatus);
            booking.BookingStatusID = status.BookingStatusID;
            booking.BookingStatus = status;

            if (targetStatus == BookingStatusConstants.Active)
            {
                booking.ActualPickupDate ??= DateTime.UtcNow;
                await SetCarStatusAsync(booking.Car, CarStatusConstants.Rented);
            }
            else if (targetStatus == BookingStatusConstants.Completed)
            {
                booking.ActualReturnDate ??= DateTime.UtcNow;
                booking.DepositStatus = DepositStatusConstants.PendingRefund;
                await SetCarStatusAsync(booking.Car, CarStatusConstants.Available);
            }
            else if (targetStatus == BookingStatusConstants.Confirmed)
            {
                booking.DepositStatus = DepositStatusConstants.Holding;
            }

            _context.AuditLogs.Add(new AuditLog
            {
                Action = "BookingTransition",
                EntityType = "Booking",
                EntityId = booking.BookingID,
                UserId = actorUserId,
                UserName = actorRole,
                Description = $"{current} -> {targetStatus}. {reason}".Trim()
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new BookingTransitionResult(true, null);
        }

        public async Task<BookingTransitionResult> CancelAsync(int bookingId, int? actorUserId, string actorRole, string? reason)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var booking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Payment)
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
                return new BookingTransitionResult(false, "Booking not found.");

            var current = booking.BookingStatus?.Name ?? BookingStatusConstants.Draft;
            if (current == BookingStatusConstants.Completed)
            {
                await transaction.RollbackAsync();
                return new BookingTransitionResult(false, "Completed bookings cannot be cancelled.");
            }

            var cancelled = await GetOrCreateStatusAsync(BookingStatusConstants.Cancelled);
            booking.BookingStatusID = cancelled.BookingStatusID;
            booking.BookingStatus = cancelled;
            booking.CancelReason = reason;

            if (booking.Payment != null && booking.Payment.PaymentStatus == PaymentStatusConstants.Paid)
            {
                booking.DepositStatus = current == BookingStatusConstants.Active
                    ? DepositStatusConstants.UnderReview
                    : DepositStatusConstants.PendingRefund;

                await _ledger.CreateRefundRequestAsync(booking.BookingID, booking.DepositAmount, reason ?? "Booking cancelled.");
            }
            else
            {
                booking.DepositStatus = DepositStatusConstants.Unpaid;
            }

            await SetCarStatusAsync(booking.Car, CarStatusConstants.Available);

            _context.AuditLogs.Add(new AuditLog
            {
                Action = "BookingCancel",
                EntityType = "Booking",
                EntityId = booking.BookingID,
                UserId = actorUserId,
                UserName = actorRole,
                Description = $"{current} -> Cancelled. {reason}".Trim()
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new BookingTransitionResult(true, null);
        }

        private BookingTransitionResult ValidateTransition(Booking booking, string current, string target)
        {
            if (target == current)
                return new BookingTransitionResult(true, null);

            var paymentPaid = booking.Payment?.PaymentStatus == PaymentStatusConstants.Paid;

            if (target == BookingStatusConstants.Confirmed && !paymentPaid)
                return new BookingTransitionResult(false, "Deposit payment must be paid before confirmation.");

            if (target == BookingStatusConstants.Active)
            {
                if (current != BookingStatusConstants.Confirmed)
                    return new BookingTransitionResult(false, "Only confirmed bookings can become active.");
                if (booking.DigitalContract == null)
                    return new BookingTransitionResult(false, "Digital contract must exist before pickup.");
                if (booking.HandoverRecords == null || !booking.HandoverRecords.Any(h => h.Type == "CheckIn"))
                    return new BookingTransitionResult(false, "Check-in handover record is required before pickup.");
            }

            if (target == BookingStatusConstants.Completed)
            {
                if (current != BookingStatusConstants.Active)
                    return new BookingTransitionResult(false, "Only active bookings can be completed.");
                if (booking.HandoverRecords == null || !booking.HandoverRecords.Any(h => h.Type == "CheckOut"))
                    return new BookingTransitionResult(false, "Check-out handover record is required before completion.");
            }

            return target switch
            {
                BookingStatusConstants.Pending when current is BookingStatusConstants.Unpaid or BookingStatusConstants.Draft => new BookingTransitionResult(true, null),
                BookingStatusConstants.Confirmed when current is BookingStatusConstants.Pending => new BookingTransitionResult(true, null),
                BookingStatusConstants.Active => new BookingTransitionResult(true, null),
                BookingStatusConstants.Completed => new BookingTransitionResult(true, null),
                BookingStatusConstants.Expired when current is BookingStatusConstants.Unpaid or BookingStatusConstants.Pending => new BookingTransitionResult(true, null),
                _ => new BookingTransitionResult(false, $"Invalid transition {current} -> {target}.")
            };
        }

        private async Task<BookingStatus> GetOrCreateStatusAsync(string statusName)
        {
            var status = await _context.BookingStatuses.FirstOrDefaultAsync(s => s.Name == statusName);
            if (status != null) return status;

            status = new BookingStatus { Name = statusName };
            _context.BookingStatuses.Add(status);
            await _context.SaveChangesAsync();
            return status;
        }

        private async Task SetCarStatusAsync(Car? car, string statusName)
        {
            if (car == null) return;

            var status = await _context.CarStatuses.FirstOrDefaultAsync(s => s.Name == statusName);
            if (status != null)
                car.CarStatusID = status.CarStatusID;
        }
    }
}
