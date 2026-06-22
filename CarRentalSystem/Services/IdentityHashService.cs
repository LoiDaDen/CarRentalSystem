using System.Security.Cryptography;
using System.Text;

namespace CarRentalSystem.Services
{
    public interface IIdentityHashService
    {
        string? HashForStorage(string? value);
        IReadOnlyList<string> HashesForLookup(string? value);
        bool UsesConfiguredHmacKey { get; }
    }

    public sealed class IdentityHashService : IIdentityHashService
    {
        private readonly byte[]? _activeKey;
        private readonly IReadOnlyList<byte[]> _previousKeys;

        public IdentityHashService(IConfiguration configuration)
        {
            var activeKey = configuration["Security:IdentityHash:ActiveKey"]
                ?? Environment.GetEnvironmentVariable("IDENTITY_HASH_ACTIVE_KEY");
            _activeKey = DecodeKey(activeKey);

            var previousKeys = configuration.GetSection("Security:IdentityHash:PreviousKeys").Get<string[]>()
                ?? SplitKeys(Environment.GetEnvironmentVariable("IDENTITY_HASH_PREVIOUS_KEYS"));
            _previousKeys = previousKeys
                .Select(DecodeKey)
                .Where(k => k != null)
                .Cast<byte[]>()
                .ToList();
        }

        public bool UsesConfiguredHmacKey => _activeKey != null;

        public string? HashForStorage(string? value)
        {
            var normalized = Normalize(value);
            if (normalized == null)
                return null;

            return _activeKey == null
                ? Sha256Hex(normalized)
                : HmacSha256Hex(normalized, _activeKey);
        }

        public IReadOnlyList<string> HashesForLookup(string? value)
        {
            var normalized = Normalize(value);
            if (normalized == null)
                return Array.Empty<string>();

            var hashes = new List<string>();
            if (_activeKey != null)
                hashes.Add(HmacSha256Hex(normalized, _activeKey));

            foreach (var key in _previousKeys)
                hashes.Add(HmacSha256Hex(normalized, key));

            hashes.Add(Sha256Hex(normalized));
            return hashes.Distinct(StringComparer.Ordinal).ToList();
        }

        private static string? Normalize(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static string[] SplitKeys(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static byte[]? DecodeKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            try
            {
                return Convert.FromBase64String(trimmed);
            }
            catch (FormatException)
            {
                return Encoding.UTF8.GetBytes(trimmed);
            }
        }

        private static string Sha256Hex(string value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        private static string HmacSha256Hex(string value, byte[] key)
        {
            using var hmac = new HMACSHA256(key);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }
    }
}
