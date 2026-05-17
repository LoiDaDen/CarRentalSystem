using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IApiKeyService
    {
        Task<ApiKeyCreateResult> CreateAsync(int ownerUserId, string name, string scopes, DateTime? expiresAt, int actorUserId);
        Task<ApiKey?> VerifyAsync(string rawKey);
        Task<bool> RevokeAsync(int apiKeyId, int actorUserId);
    }

    public record ApiKeyCreateResult(ApiKey ApiKey, string RawKey);

    public class ApiKeyService : IApiKeyService
    {
        private const string Prefix = "crs_live_";
        private readonly ApplicationDbContext _context;
        private readonly IAuditTrailService _auditTrail;

        public ApiKeyService(ApplicationDbContext context, IAuditTrailService auditTrail)
        {
            _context = context;
            _auditTrail = auditTrail;
        }

        public async Task<ApiKeyCreateResult> CreateAsync(int ownerUserId, string name, string scopes, DateTime? expiresAt, int actorUserId)
        {
            var rawKey = Prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var apiKey = new ApiKey
            {
                OwnerUserID = ownerUserId,
                Name = name.Trim(),
                Scopes = scopes.Trim(),
                ExpiresAt = expiresAt,
                KeyPrefix = rawKey[..Math.Min(rawKey.Length, 18)],
                KeyHash = Hash(rawKey)
            };

            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "CreateApiKey",
                EntityType = nameof(ApiKey),
                EntityId = apiKey.ApiKeyID,
                UserId = actorUserId,
                NewValues = JsonSerializer.Serialize(new { apiKey.OwnerUserID, apiKey.Name, apiKey.Scopes, apiKey.ExpiresAt, apiKey.KeyPrefix })
            });

            return new ApiKeyCreateResult(apiKey, rawKey);
        }

        public async Task<ApiKey?> VerifyAsync(string rawKey)
        {
            if (string.IsNullOrWhiteSpace(rawKey)) return null;
            var hash = Hash(rawKey);
            var key = await _context.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.KeyHash == hash);
            if (key == null || key.Status != ApiKeyStatusConstants.Active) return null;
            if (key.ExpiresAt.HasValue && key.ExpiresAt.Value <= DateTime.UtcNow) return null;
            return key;
        }

        public async Task<bool> RevokeAsync(int apiKeyId, int actorUserId)
        {
            var key = await _context.ApiKeys.FirstOrDefaultAsync(k => k.ApiKeyID == apiKeyId);
            if (key == null || key.Status == ApiKeyStatusConstants.Revoked) return false;

            key.Status = ApiKeyStatusConstants.Revoked;
            key.RevokedAt = DateTime.UtcNow;
            key.RevokedByUserID = actorUserId;
            await _context.SaveChangesAsync();
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "RevokeApiKey",
                EntityType = nameof(ApiKey),
                EntityId = key.ApiKeyID,
                UserId = actorUserId,
                NewValues = JsonSerializer.Serialize(new { key.Status, key.RevokedAt, key.KeyPrefix })
            });

            return true;
        }

        private static string Hash(string value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }
    }
}
