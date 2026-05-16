using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IAuditTrailService
    {
        Task<AuditLog> RecordAsync(AuditLog log, CancellationToken cancellationToken = default);
        Task<AuditChainVerificationResult> VerifyAsync(DateTime? from = null, DateTime? to = null, string? entityType = null, CancellationToken cancellationToken = default);
        string ComputeRecordHash(AuditLog log);
    }

    public record AuditChainVerificationResult(bool IsValid, int CheckedCount, long? FailedAuditLogID, string? Message);

    public class AuditTrailService : IAuditTrailService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public AuditTrailService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public async Task<AuditLog> RecordAsync(AuditLog log, CancellationToken cancellationToken = default)
        {
            PopulateHttpContext(log);

            if (_configuration.GetValue("Audit:HashChainEnabled", true))
            {
                var previous = await _context.AuditLogs
                    .AsNoTracking()
                    .OrderByDescending(a => a.AuditLogID)
                    .FirstOrDefaultAsync(cancellationToken);

                log.PreviousHash = previous?.RecordHash;
                log.HashVersion = 1;
                log.RecordHash = ComputeRecordHash(log);
            }

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
            return log;
        }

        public async Task<AuditChainVerificationResult> VerifyAsync(DateTime? from = null, DateTime? to = null, string? entityType = null, CancellationToken cancellationToken = default)
        {
            var query = _context.AuditLogs.AsNoTracking().OrderBy(a => a.AuditLogID).AsQueryable();
            if (from.HasValue)
                query = query.Where(a => a.Timestamp >= from.Value);
            if (to.HasValue)
                query = query.Where(a => a.Timestamp <= to.Value);
            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(a => a.EntityType == entityType);

            var logs = await query.ToListAsync(cancellationToken);
            var checkedCount = 0;
            foreach (var log in logs.Where(l => !string.IsNullOrWhiteSpace(l.RecordHash)))
            {
                var expected = ComputeRecordHash(log);
                checkedCount++;
                if (!string.Equals(expected, log.RecordHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new AuditChainVerificationResult(false, checkedCount, log.AuditLogID, "Audit record hash mismatch.");
                }
            }

            return new AuditChainVerificationResult(true, checkedCount, null, "Audit hash chain is valid for the selected range.");
        }

        public string ComputeRecordHash(AuditLog log)
        {
            var canonical = string.Join("|", new[]
            {
                log.HashVersion.ToString(),
                log.Timestamp.ToUniversalTime().ToString("O"),
                log.Action ?? string.Empty,
                log.EntityType ?? string.Empty,
                log.EntityId?.ToString() ?? string.Empty,
                log.UserId?.ToString() ?? string.Empty,
                log.UserName ?? string.Empty,
                log.Description ?? string.Empty,
                log.OldValues ?? string.Empty,
                log.NewValues ?? string.Empty,
                log.Reason ?? string.Empty,
                log.CorrelationId ?? string.Empty,
                log.PreviousHash ?? string.Empty
            });

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }

        private void PopulateHttpContext(AuditLog log)
        {
            var http = _httpContextAccessor.HttpContext;
            if (http == null)
                return;

            log.UserId ??= int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
            log.UserName ??= http.User.Identity?.Name ?? http.User.FindFirstValue(ClaimTypes.Email);
            log.IpAddress ??= http.Connection.RemoteIpAddress?.ToString();
            log.UserAgent ??= http.Request.Headers.UserAgent.ToString();
            log.RequestUrl ??= $"{http.Request.Method} {http.Request.Path}{http.Request.QueryString}";
            log.CorrelationId ??= http.TraceIdentifier;
        }
    }
}
