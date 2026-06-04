using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IPrivacyRequestService
    {
        Task<PrivacyRequest> CreateAsync(int userId, string requestType, string? reason);
        Task<PrivacyRequestServiceResult> ApproveAsync(int requestId, int reviewedByUserId, string reason);
        Task<PrivacyRequestServiceResult> RejectAsync(int requestId, int reviewedByUserId, string reason);
        Task<PrivacyRequestServiceResult> CompleteAsync(int requestId, int reviewedByUserId, string reason);
        Task<List<PrivacyRetentionCandidate>> PreviewRetentionAsync(DateTime asOfUtc);
    }

    public record PrivacyRequestServiceResult(bool IsSuccess, string Message, PrivacyRequest? Request = null);
    public record PrivacyRetentionCandidate(string DataDomain, int EntityId, DateTime? LastActivityUtc, string RecommendedAction);

    public class PrivacyRequestService : IPrivacyRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditTrailService _auditTrail;

        public PrivacyRequestService(ApplicationDbContext context, IAuditTrailService auditTrail)
        {
            _context = context;
            _auditTrail = auditTrail;
        }

        public async Task<PrivacyRequest> CreateAsync(int userId, string requestType, string? reason)
        {
            if (!PrivacyRequestTypeConstants.All.Contains(requestType))
                throw new InvalidOperationException("Unsupported privacy request type.");

            var request = new PrivacyRequest
            {
                UserID = userId,
                RequestType = requestType,
                Reason = reason,
                Status = PrivacyRequestStatusConstants.PendingReview,
                DueAt = ResolveDueAt(requestType),
                RequestScope = "Account",
                IdentityVerificationStatus = "AuthenticatedSession"
            };

            _context.PrivacyRequests.Add(request);
            await _context.SaveChangesAsync();
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "CreatePrivacyRequest",
                EntityType = nameof(PrivacyRequest),
                EntityId = request.PrivacyRequestID,
                UserId = userId,
                Reason = reason,
                NewValues = JsonSerializer.Serialize(new { request.UserID, request.RequestType, request.Status })
            });

            return request;
        }

        public async Task<PrivacyRequestServiceResult> ApproveAsync(int requestId, int reviewedByUserId, string reason)
        {
            var request = await _context.PrivacyRequests.FirstOrDefaultAsync(r => r.PrivacyRequestID == requestId);
            if (request == null)
                return new PrivacyRequestServiceResult(false, "Privacy request not found.");
            if (request.Status != PrivacyRequestStatusConstants.PendingReview)
                return new PrivacyRequestServiceResult(false, "Privacy request is not pending review.", request);

            var oldValues = JsonSerializer.Serialize(new { request.Status, request.ReviewedByUserID, request.ReviewedAt });
            request.Status = PrivacyRequestStatusConstants.Approved;
            request.ReviewedByUserID = reviewedByUserId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewReason = reason;
            await _context.SaveChangesAsync();

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "ApprovePrivacyRequest",
                EntityType = nameof(PrivacyRequest),
                EntityId = request.PrivacyRequestID,
                UserId = reviewedByUserId,
                Reason = reason,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new { request.Status, request.ReviewedByUserID, request.ReviewedAt })
            });

            return new PrivacyRequestServiceResult(true, "Privacy request approved.", request);
        }

        public async Task<PrivacyRequestServiceResult> RejectAsync(int requestId, int reviewedByUserId, string reason)
        {
            var request = await _context.PrivacyRequests.FirstOrDefaultAsync(r => r.PrivacyRequestID == requestId);
            if (request == null)
                return new PrivacyRequestServiceResult(false, "Privacy request not found.");
            if (request.Status != PrivacyRequestStatusConstants.PendingReview)
                return new PrivacyRequestServiceResult(false, "Privacy request is not pending review.", request);

            var oldValues = JsonSerializer.Serialize(new { request.Status });
            request.Status = PrivacyRequestStatusConstants.Rejected;
            request.ReviewedByUserID = reviewedByUserId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewReason = reason;
            await _context.SaveChangesAsync();

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "RejectPrivacyRequest",
                EntityType = nameof(PrivacyRequest),
                EntityId = request.PrivacyRequestID,
                UserId = reviewedByUserId,
                Reason = reason,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new { request.Status })
            });

            return new PrivacyRequestServiceResult(true, "Privacy request rejected.", request);
        }

        public async Task<PrivacyRequestServiceResult> CompleteAsync(int requestId, int reviewedByUserId, string reason)
        {
            var request = await _context.PrivacyRequests.FirstOrDefaultAsync(r => r.PrivacyRequestID == requestId);
            if (request == null)
                return new PrivacyRequestServiceResult(false, "Privacy request not found.");
            if (request.Status != PrivacyRequestStatusConstants.Approved)
                return new PrivacyRequestServiceResult(false, "Privacy request must be approved before completion.", request);

            var manifest = request.RequestType.Equals(PrivacyRequestTypeConstants.Delete, StringComparison.OrdinalIgnoreCase)
                ? await BuildDeletionManifestAsync(request.UserID)
                : await BuildExportManifestAsync(request.UserID);
            var oldValues = JsonSerializer.Serialize(new { request.Status, request.CompletedAt, request.CompletedByUserID });
            request.Status = PrivacyRequestStatusConstants.Completed;
            request.CompletedAt = DateTime.UtcNow;
            request.CompletedByUserID = reviewedByUserId;
            request.ExportManifestJson = JsonSerializer.Serialize(manifest);
            await _context.SaveChangesAsync();

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "CompletePrivacyRequest",
                EntityType = nameof(PrivacyRequest),
                EntityId = request.PrivacyRequestID,
                UserId = reviewedByUserId,
                Reason = reason,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new
                {
                    request.Status,
                    request.CompletedAt,
                    Manifest = manifest
                })
            });

            return new PrivacyRequestServiceResult(true, "Privacy request completed.", request);
        }

        public async Task<List<PrivacyRetentionCandidate>> PreviewRetentionAsync(DateTime asOfUtc)
        {
            var trackingCutoff = asOfUtc.AddDays(-90);
            var ekycCutoff = asOfUtc.AddYears(-2);
            var candidates = new List<PrivacyRetentionCandidate>();

            var tracking = await _context.VehicleTrackings
                .AsNoTracking()
                .Where(t => t.Timestamp < trackingCutoff)
                .OrderBy(t => t.Timestamp)
                .Take(200)
                .Select(t => new PrivacyRetentionCandidate("Tracking", t.TrackingID, t.Timestamp, "Review for retention purge/anonymization."))
                .ToListAsync();
            candidates.AddRange(tracking);

            var oldUnverifiedUsers = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsEmailVerified && u.CreatedAt < ekycCutoff)
                .OrderBy(u => u.CreatedAt)
                .Take(200)
                .Select(u => new PrivacyRetentionCandidate("UserPII", u.UserID, u.CreatedAt, "Review stale unverified user PII for anonymization."))
                .ToListAsync();
            candidates.AddRange(oldUnverifiedUsers);

            return candidates;
        }

        private async Task<object> BuildExportManifestAsync(int userId)
        {
            var bookings = await _context.Bookings.AsNoTracking().CountAsync(b => b.UserID == userId);
            var payments = await _context.Payments.AsNoTracking().CountAsync(p => p.Booking != null && p.Booking.UserID == userId);
            var handovers = await _context.HandoverRecords.AsNoTracking().CountAsync(h => h.Booking != null && h.Booking.UserID == userId);
            var privacyRequests = await _context.PrivacyRequests.AsNoTracking().CountAsync(r => r.UserID == userId);

            return new
            {
                UserID = userId,
                GeneratedAtUtc = DateTime.UtcNow,
                Domains = new[]
                {
                    new { Name = "Bookings", Count = bookings },
                    new { Name = "Payments", Count = payments },
                    new { Name = "Handovers", Count = handovers },
                    new { Name = "PrivacyRequests", Count = privacyRequests }
                },
                Note = "Manifest only. Raw PII export requires secure delivery approved by Admin/Manager."
            };
        }

        private async Task<object> BuildDeletionManifestAsync(int userId)
        {
            var bookings = await _context.Bookings.AsNoTracking().CountAsync(b => b.UserID == userId);
            var payments = await _context.Payments.AsNoTracking().CountAsync(p => p.Booking != null && p.Booking.UserID == userId);
            var openBookings = await _context.Bookings.AsNoTracking().CountAsync(b =>
                b.UserID == userId &&
                b.BookingStatus != null &&
                b.BookingStatus.Name != BookingStatusConstants.Cancelled &&
                b.BookingStatus.Name != BookingStatusConstants.Completed &&
                b.BookingStatus.Name != BookingStatusConstants.Rejected);

            return new
            {
                UserID = userId,
                GeneratedAtUtc = DateTime.UtcNow,
                Request = "Delete",
                RetentionBlocks = new[]
                {
                    new { Name = "Bookings", Count = bookings },
                    new { Name = "Payments", Count = payments },
                    new { Name = "OpenBookings", Count = openBookings }
                },
                Note = "Deletion requires legal-retention review before anonymization because bookings, payments, tax invoices, disputes, and safety logs may need retention."
            };
        }

        private static DateTime ResolveDueAt(string requestType)
        {
            var now = DateTime.UtcNow;
            return requestType.Equals(PrivacyRequestTypeConstants.RetentionReview, StringComparison.OrdinalIgnoreCase)
                ? now.AddDays(7)
                : now.AddDays(3);
        }
    }
}
