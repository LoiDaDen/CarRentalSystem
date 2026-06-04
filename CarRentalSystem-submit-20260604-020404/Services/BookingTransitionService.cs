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
        private readonly IAuditTrailService _auditTrail;

        public BookingTransitionService(ApplicationDbContext context, IPaymentLedgerService ledger, IAuditTrailService auditTrail)
        {
            _context = context;
            _ledger = ledger;
            _auditTrail = auditTrail;
        }

        public async Task<BookingTransitionResult> TransitionAsync(int bookingId, string targetStatus, int? actorUserId, string actorRole, string? reason = null)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var booking = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Payment)
                .Include(b => b.Car)
                .Include(b => b.HandoverRecords)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
                return new BookingTransitionResult(false, "Không tìm thấy đơn đặt xe.");

            var current = booking.BookingStatus?.Name ?? BookingStatusConstants.Draft;
            var validation = ValidateTransition(booking, current, targetStatus);
            if (!validation.IsSuccess)
            {
                await transaction.RollbackAsync();
                return validation;
            }

            var status = await GetRequiredStatusAsync(targetStatus);
            if (status == null)
            {
                await transaction.RollbackAsync();
                return new BookingTransitionResult(false, $"Thiếu lookup trạng thái đặt xe: {targetStatus}.");
            }

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
                booking.DepositStatus = DepositStatusConstants.Refunded;
                await SetCarStatusAsync(booking.Car, CarStatusConstants.Available);
            }
            else if (targetStatus == BookingStatusConstants.Confirmed)
            {
                booking.DepositStatus = DepositStatusConstants.Holding;
            }

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "BookingTransition",
                EntityType = "Booking",
                EntityId = booking.BookingID,
                UserId = actorUserId,
                UserName = actorRole,
                Description = $"{current} -> {targetStatus}. {reason}".Trim()
            });

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
                return new BookingTransitionResult(false, "Không tìm thấy đơn đặt xe.");

            var current = booking.BookingStatus?.Name ?? BookingStatusConstants.Draft;
            if (current == BookingStatusConstants.Completed)
            {
                await transaction.RollbackAsync();
                return new BookingTransitionResult(false, "Đơn đã hoàn tất, không thể hủy.");
            }

            var cancelled = await GetRequiredStatusAsync(BookingStatusConstants.Cancelled);
            if (cancelled == null)
            {
                await transaction.RollbackAsync();
                return new BookingTransitionResult(false, $"Thiếu lookup trạng thái đặt xe: {BookingStatusConstants.Cancelled}.");
            }

            booking.BookingStatusID = cancelled.BookingStatusID;
            booking.BookingStatus = cancelled;
            booking.CancelReason = reason;

            if (current == BookingStatusConstants.Active)
            {
                booking.ActualReturnDate ??= DateTime.UtcNow;
                booking.DepositStatus = DepositStatusConstants.UnderReview;
                if (booking.Payment != null && booking.Payment.PaymentStatus == PaymentStatusConstants.Paid)
                    await _ledger.CreateRefundRequestAsync(booking.BookingID, booking.DepositAmount, reason ?? "Active booking cancelled.");
            }
            else if (booking.Payment != null && booking.Payment.PaymentStatus == PaymentStatusConstants.Paid)
            {
                booking.DepositStatus = DepositStatusConstants.PendingRefund;
                await _ledger.CreateRefundRequestAsync(booking.BookingID, booking.DepositAmount, reason ?? "Booking cancelled.");
            }
            else
            {
                booking.DepositStatus = DepositStatusConstants.Unpaid;
            }

            await SetCarStatusAsync(booking.Car, CarStatusConstants.Available);

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "BookingCancel",
                EntityType = "Booking",
                EntityId = booking.BookingID,
                UserId = actorUserId,
                UserName = actorRole,
                Description = $"{current} -> Cancelled. {reason}".Trim()
            });

            await transaction.CommitAsync();

            return new BookingTransitionResult(true, null);
        }

        private BookingTransitionResult ValidateTransition(Booking booking, string current, string target)
        {
            if (target == current)
                return new BookingTransitionResult(true, null);

            var paymentPaid = booking.Payment?.PaymentStatus == PaymentStatusConstants.Paid;

            if (target == BookingStatusConstants.Confirmed && !paymentPaid)
                return new BookingTransitionResult(false, "Cần thanh toán đặt cọc trước khi xác nhận đơn.");

            if (target == BookingStatusConstants.Active)
            {
                if (current != BookingStatusConstants.Confirmed)
                    return new BookingTransitionResult(false, "Chỉ đơn đã xác nhận mới có thể chuyển sang trạng thái đang thuê.");
                if (booking.HandoverRecords == null || !booking.HandoverRecords.Any(h => h.Type == "CheckIn"))
                    return new BookingTransitionResult(false, "Cần có biên bản nhận xe (CheckIn) trước khi xác nhận khách lấy xe.");
            }

            if (target == BookingStatusConstants.Completed)
            {
                if (current != BookingStatusConstants.Active)
                    return new BookingTransitionResult(false, "Chỉ đơn đang thuê mới có thể chuyển sang hoàn tất.");
                if (booking.HandoverRecords == null || !booking.HandoverRecords.Any(h => h.Type == "CheckOut"))
                    return new BookingTransitionResult(false, "Cần có biên bản trả xe (CheckOut) trước khi hoàn tất đơn.");
            }

            return target switch
            {
                BookingStatusConstants.Pending when current is BookingStatusConstants.Unpaid or BookingStatusConstants.Draft => new BookingTransitionResult(true, null),
                BookingStatusConstants.Confirmed when current is BookingStatusConstants.Pending => new BookingTransitionResult(true, null),
                BookingStatusConstants.Active => new BookingTransitionResult(true, null),
                BookingStatusConstants.Completed => new BookingTransitionResult(true, null),
                BookingStatusConstants.Expired when current is BookingStatusConstants.Unpaid or BookingStatusConstants.Pending => new BookingTransitionResult(true, null),
                _ => new BookingTransitionResult(false, $"Trạng thái không hợp lệ: {current} → {target}.")
            };
        }

        private async Task<BookingStatus?> GetRequiredStatusAsync(string statusName)
        {
            return await _context.BookingStatuses.FirstOrDefaultAsync(s => s.Name == statusName);
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
