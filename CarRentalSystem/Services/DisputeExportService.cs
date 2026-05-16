using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IDisputeExportService
    {
        Task<DisputeExportResult?> ExportBookingAsync(int bookingId, int requestedByUserId, bool includeSensitive = false);
    }

    public record DisputeExportResult(byte[] Content, string FileName, string ContentHash);

    public class DisputeExportService : IDisputeExportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditTrailService _audit;
        private readonly IEvidenceHashService _hash;
        private readonly IEnterpriseTelemetryService _telemetry;
        private readonly IProductAnalyticsService _analytics;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        public DisputeExportService(ApplicationDbContext context, IAuditTrailService audit, IEvidenceHashService hash, IEnterpriseTelemetryService telemetry, IProductAnalyticsService analytics)
        {
            _context = context;
            _audit = audit;
            _hash = hash;
            _telemetry = telemetry;
            _analytics = analytics;
        }

        public async Task<DisputeExportResult?> ExportBookingAsync(int bookingId, int requestedByUserId, bool includeSensitive = false)
        {
            using var activity = _telemetry.StartActivity("dispute_export",
                new("booking.id", bookingId),
                new("dispute.include_sensitive", includeSensitive));
            var booking = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.User)
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(b => b.BookingStatus)
                .Include(b => b.DigitalContract)
                .Include(b => b.RentalContract)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
            if (booking == null)
                return null;

            var handovers = await _context.HandoverRecords.AsNoTracking().Where(h => h.BookingID == bookingId).OrderBy(h => h.RecordDate).ToListAsync();
            var ledger = await _context.PaymentLedgerEntries.AsNoTracking().Where(l => l.BookingID == bookingId).OrderBy(l => l.CreatedAt).ToListAsync();
            var payos = await _context.PayOSTransactions.AsNoTracking().Where(t => t.BookingID == bookingId).OrderBy(t => t.CreatedAt).ToListAsync();
            var orderCodes = payos.Select(p => (long?)p.OrderCode).ToList();
            var webhooks = await _context.PaymentWebhookEvents.AsNoTracking().Where(e => e.OrderCode != null && orderCodes.Contains(e.OrderCode)).OrderBy(e => e.ReceivedAt).ToListAsync();
            var refunds = await _context.RefundRequests.AsNoTracking().Where(r => r.BookingID == bookingId).OrderBy(r => r.CreatedAt).ToListAsync();
            var refundIds = refunds.Select(r => r.RefundRequestID).ToList();
            var payouts = await _context.RefundPayouts.AsNoTracking().Where(p => refundIds.Contains(p.RefundRequestID)).OrderBy(p => p.CreatedAt).ToListAsync();
            var audits = await _context.AuditLogs.AsNoTracking()
                .Where(a => (a.EntityType == nameof(Booking) && a.EntityId == bookingId) ||
                            (a.NewValues != null && a.NewValues.Contains($"\"BookingID\":{bookingId}")) ||
                            (a.OldValues != null && a.OldValues.Contains($"\"BookingID\":{bookingId}")))
                .OrderBy(a => a.Timestamp)
                .ToListAsync();

            var summary = new
            {
                Booking = new
                {
                    booking.BookingID,
                    booking.StartDate,
                    booking.EndDate,
                    booking.TotalPrice,
                    booking.DepositAmount,
                    booking.DepositStatus,
                    Status = booking.BookingStatus?.Name,
                    Customer = BuildCustomerSnapshot(booking, includeSensitive),
                    Car = new { booking.CarID, booking.Car?.CarName, Brand = booking.Car?.CarBrand?.Name, booking.Car?.LicensePlate }
                },
                ExportedAt = DateTime.UtcNow,
                IncludeSensitive = includeSensitive
            };

            var manifest = new Dictionary<string, string>();
            await using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddJson(archive, manifest, "summary.json", summary);
                AddJson(archive, manifest, "contract.json", new { booking.DigitalContract, booking.RentalContract });
                AddJson(archive, manifest, "handover-evidence.json", handovers);
                AddJson(archive, manifest, "payment-ledger.json", ledger);
                AddJson(archive, manifest, "payos-transactions.json", BuildPayosSnapshot(payos, includeSensitive));
                AddJson(archive, manifest, "payment-webhooks.json", BuildWebhookSnapshot(webhooks, includeSensitive));
                AddJson(archive, manifest, "refunds.json", new { Refunds = refunds, Payouts = BuildPayoutSnapshot(payouts, includeSensitive) });
                AddJson(archive, manifest, "audit-trail.json", audits);
                AddJson(archive, manifest, "manifest.json", manifest);
            }

            var content = output.ToArray();
            var contentHash = _hash.ComputeTextHash(Convert.ToBase64String(content));
            await _audit.RecordAsync(new AuditLog
            {
                Action = "ExportDisputePackage",
                EntityType = nameof(Booking),
                EntityId = bookingId,
                UserId = requestedByUserId,
                Reason = includeSensitive ? "Dispute export with sensitive data." : "Dispute export redacted.",
                NewValues = JsonSerializer.Serialize(new { bookingId, includeSensitive, contentHash })
            });
            await _analytics.TrackAsync("dispute_export_created", new Dictionary<string, object?>
            {
                ["booking_id"] = bookingId,
                ["include_sensitive"] = includeSensitive,
                ["content_hash_present"] = !string.IsNullOrWhiteSpace(contentHash)
            });

            return new DisputeExportResult(content, $"dispute-booking-{bookingId}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip", contentHash);
        }

        private void AddJson(ZipArchive archive, Dictionary<string, string> manifest, string name, object value)
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(json);
            manifest[name] = _hash.ComputeTextHash(json);
        }

        private static object BuildCustomerSnapshot(Booking booking, bool includeSensitive)
        {
            if (includeSensitive)
                return booking.User is null ? new { booking.UserID } : (object)booking.User;

            return new { booking.UserID, booking.User?.Name, Email = RedactEmail(booking.User?.Email) };
        }

        private static object BuildPayosSnapshot(List<PayOSTransaction> payos, bool includeSensitive)
        {
            if (includeSensitive)
                return payos;

            return payos.Select(p => new { p.PayOSTransactionID, p.BookingID, p.OrderCode, p.Amount, p.Status, p.Currency, p.CreatedAt, p.PaidAt, p.CancelledAt, p.FailedAt }).ToList();
        }

        private static object BuildWebhookSnapshot(List<PaymentWebhookEvent> webhooks, bool includeSensitive)
        {
            if (includeSensitive)
                return webhooks;

            return webhooks.Select(w => new { w.PaymentWebhookEventID, w.Provider, w.EventKey, w.PayloadHash, w.OrderCode, w.Status, w.ReceivedAt, w.ProcessedAt, w.Error }).ToList();
        }

        private static object BuildPayoutSnapshot(List<RefundPayout> payouts, bool includeSensitive)
        {
            if (includeSensitive)
                return payouts;

            return payouts.Select(p => new { p.RefundPayoutID, p.RefundRequestID, p.ReferenceId, p.Provider, p.Amount, p.Currency, p.ApprovalState, p.TransactionState, p.AttemptCount, p.CreatedAt, p.SubmittedAt, p.CompletedAt, p.FailedAt }).ToList();
        }

        private static string? RedactEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return email;
            var parts = email.Split('@', 2);
            return $"{parts[0][0]}***@{parts[1]}";
        }
    }
}
