using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

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
        private readonly IDataProtector _protector;
        private readonly ILogger<PiiEncryptionService> _logger;

        public PiiEncryptionService(IDataProtectionProvider provider, ILogger<PiiEncryptionService> logger)
        {
            // Purpose string = unique key per app/feature
            _protector = provider.CreateProtector("CarRentalSystem.PII.v1");
            _logger = logger;
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            return _protector.Protect(plainText);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            try
            {
                return _protector.Unprotect(cipherText);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Nếu data chưa được encrypt (legacy) → trả về nguyên bản
                _logger.LogWarning("PII decrypt failed; returning legacy plaintext fallback. Length={Length}", cipherText.Length);
                return cipherText;
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
    }
}
