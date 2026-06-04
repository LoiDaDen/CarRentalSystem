using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace CarRentalSystem.Services
{
    /// <summary>
    /// Service mã hóa dữ liệu nhạy cảm (PII) tại rest.
    /// Sử dụng IDataProtectionProvider (DPAPI) chuẩn ASP.NET Core.
    /// Dữ liệu được mã hóa/giải mã bằng machine key — an toàn khi DB bị leak.
    /// 
    /// Áp dụng cho: CCCD, GPLX, SĐT nhạy cảm, địa chỉ.
    /// </summary>
    public interface IPiiEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
        string? EncryptIfNotEmpty(string? value);
        string? DecryptIfNotEmpty(string? value);
    }

    public class PiiEncryptionService : IPiiEncryptionService
    {
        private static readonly byte[] DataProtectionMagicHeader = [0x09, 0xF0, 0xC9, 0xF0];
        private static readonly ConcurrentDictionary<string, byte> FailedDecryptFingerprints = new();

        private readonly IDataProtector _protector;
        private readonly ILogger<PiiEncryptionService> _logger;
        private readonly bool _isCypressEnvironment;

        public PiiEncryptionService(
            IDataProtectionProvider provider,
            ILogger<PiiEncryptionService> logger,
            IHostEnvironment environment)
        {
            // Purpose string = unique key per app/feature
            _protector = provider.CreateProtector("CarRentalSystem.PII.v1");
            _logger = logger;
            _isCypressEnvironment = environment.IsEnvironment("Cypress");
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            return _protector.Protect(plainText);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            var protectedPayload = cipherText.Trim();
            if (!IsLikelyDataProtectionPayload(protectedPayload))
            {
                if (_isCypressEnvironment)
                {
                    _logger.LogDebug("PII value does not look encrypted; returning legacy plaintext fallback. Length={Length}", cipherText.Length);
                }

                return cipherText;
            }

            try
            {
                return _protector.Unprotect(protectedPayload);
            }
            catch (CryptographicException ex)
            {
                // This looks encrypted, so a decrypt failure points to a key ring or app discriminator mismatch.
                var fingerprint = ComputeFingerprint(protectedPayload);
                var shortFingerprint = fingerprint[..12];

                if (FailedDecryptFingerprints.TryAdd(fingerprint, 0))
                {
                    _logger.LogWarning(
                        ex,
                        "PII encrypted payload could not be decrypted; returning empty value. Check DataProtection:KeysPath/key ring and app discriminator. Length={Length} Fingerprint={Fingerprint}",
                        protectedPayload.Length,
                        shortFingerprint);
                }
                else
                {
                    _logger.LogDebug(
                        "PII encrypted payload could not be decrypted again; returning empty value. Length={Length} Fingerprint={Fingerprint}",
                        protectedPayload.Length,
                        shortFingerprint);
                }

                return string.Empty;
            }
        }

        public string? EncryptIfNotEmpty(string? value)
        {
            return string.IsNullOrEmpty(value) ? value : Encrypt(value);
        }

        public string? DecryptIfNotEmpty(string? value)
        {
            return string.IsNullOrEmpty(value) ? value : Decrypt(value);
        }

        private static bool IsLikelyDataProtectionPayload(string value)
        {
            if (value.Length < 48 || !value.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                var decoded = WebEncoders.Base64UrlDecode(value);
                return decoded.Length >= DataProtectionMagicHeader.Length &&
                       decoded.AsSpan(0, DataProtectionMagicHeader.Length).SequenceEqual(DataProtectionMagicHeader);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string ComputeFingerprint(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
