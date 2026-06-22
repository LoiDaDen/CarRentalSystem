using System.Globalization;
using System.Security.Claims;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Http.Features;

namespace CarRentalSystem.Services;

public interface IPiiAccessService
{
    bool HasActiveGrant(HttpContext context);
    Task GrantAsync(HttpContext context, int userId, string reason);
    Task RecordDeniedAsync(HttpContext context, int? targetUserId, string reason);
    string? Mask(string? value);
}

public sealed class PiiAccessService : IPiiAccessService
{
    private const string GrantExpiresAtSessionKey = "PiiAccessVerifiedUntilUtc";
    private const string MaskedPlaceholder = "●●●●●●●●";
    private readonly IAuditTrailService _auditTrail;
    private readonly TimeSpan _ttl;

    public PiiAccessService(IAuditTrailService auditTrail, IConfiguration configuration)
    {
        _auditTrail = auditTrail;
        _ttl = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue("PiiAccess:TtlMinutes", 5), 1, 30));
    }

    public bool HasActiveGrant(HttpContext context)
    {
        var session = context.Features.Get<ISessionFeature>()?.Session;
        var rawExpiry = session?.GetString(GrantExpiresAtSessionKey);
        return DateTime.TryParse(rawExpiry, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry) &&
               expiry > DateTime.UtcNow;
    }

    public async Task GrantAsync(HttpContext context, int userId, string reason)
    {
        var session = context.Features.Get<ISessionFeature>()?.Session
            ?? throw new InvalidOperationException("PII access requires an active session.");
        var expiresAt = DateTime.UtcNow.Add(_ttl);
        session.SetString(GrantExpiresAtSessionKey, expiresAt.ToString("O", CultureInfo.InvariantCulture));
        await WriteAuditAsync(context, "PiiAccessGranted", userId, $"{reason}; expiresAtUtc={expiresAt:O}");
    }

    public Task RecordDeniedAsync(HttpContext context, int? targetUserId, string reason) =>
        WriteAuditAsync(context, "PiiAccessDenied", targetUserId, reason);

    public string? Mask(string? value) => string.IsNullOrWhiteSpace(value) ? null : MaskedPlaceholder;

    private Task WriteAuditAsync(HttpContext context, string action, int? targetUserId, string reason)
    {
        var currentUserId = int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : (int?)null;
        return _auditTrail.RecordAsync(new AuditLog
        {
            Action = action,
            EntityType = "UserPII",
            EntityId = targetUserId,
            UserId = currentUserId,
            UserName = context.User.Identity?.Name,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        });
    }
}
