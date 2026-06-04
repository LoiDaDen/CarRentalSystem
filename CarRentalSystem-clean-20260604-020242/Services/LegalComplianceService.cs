using System.Security.Cryptography;
using System.Text;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface ILegalComplianceService
    {
        Task<LegalDocumentVersion> GetActiveDocumentAsync(string code);
        Task<ConsentRecord> RecordConsentAsync(
            int userId,
            string documentCode,
            string purposeCode,
            bool isGranted,
            string source,
            HttpContext httpContext,
            string? evidenceJson = null);
        Task<bool> HasActiveConsentAsync(int userId, string documentCode, string purposeCode);
    }

    public class LegalComplianceService : ILegalComplianceService
    {
        private const string DefaultVersion = "2026.05";
        private readonly ApplicationDbContext _context;

        public LegalComplianceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<LegalDocumentVersion> GetActiveDocumentAsync(string code)
        {
            var document = await _context.LegalDocumentVersions
                .Where(d => d.Code == code && d.IsActive && d.RetiredAt == null)
                .OrderByDescending(d => d.EffectiveAt)
                .FirstOrDefaultAsync();

            if (document != null)
                return document;

            document = CreateDefaultDocument(code);
            _context.LegalDocumentVersions.Add(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<ConsentRecord> RecordConsentAsync(
            int userId,
            string documentCode,
            string purposeCode,
            bool isGranted,
            string source,
            HttpContext httpContext,
            string? evidenceJson = null)
        {
            var document = await GetActiveDocumentAsync(documentCode);
            var record = new ConsentRecord
            {
                UserID = userId,
                LegalDocumentVersionID = document.LegalDocumentVersionID,
                PurposeCode = purposeCode,
                IsGranted = isGranted,
                GrantedAt = DateTime.UtcNow,
                Source = source,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Truncate(httpContext.Request.Headers.UserAgent.ToString(), 500),
                EvidenceJson = evidenceJson,
                ConsentTextHash = Sha256Hex($"{document.Code}|{document.Version}|{purposeCode}|{isGranted}|{evidenceJson}")
            };

            _context.ConsentRecords.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<bool> HasActiveConsentAsync(int userId, string documentCode, string purposeCode)
        {
            return await _context.ConsentRecords
                .Include(c => c.LegalDocumentVersion)
                .AnyAsync(c =>
                    c.UserID == userId &&
                    c.PurposeCode == purposeCode &&
                    c.IsGranted &&
                    c.WithdrawnAt == null &&
                    c.LegalDocumentVersion != null &&
                    c.LegalDocumentVersion.Code == documentCode &&
                    c.LegalDocumentVersion.IsActive);
        }

        private static LegalDocumentVersion CreateDefaultDocument(string code)
        {
            var title = code switch
            {
                LegalDocumentCodes.TermsOfUse => "Điều khoản sử dụng",
                LegalDocumentCodes.PrivacyNotice => "Thông báo xử lý dữ liệu cá nhân",
                LegalDocumentCodes.EkycBiometricConsent => "Đồng ý xử lý dữ liệu eKYC và sinh trắc học",
                LegalDocumentCodes.SelfDriveAgreement => "Cam kết thuê xe tự lái",
                LegalDocumentCodes.WalletLedgerNotice => "Thông báo sổ cái nội bộ, không phải ví điện tử",
                _ => code
            };

            return new LegalDocumentVersion
            {
                Code = code,
                Version = DefaultVersion,
                Title = title,
                ContentHash = Sha256Hex($"{code}|{DefaultVersion}|{title}"),
                PublishedAt = DateTime.UtcNow,
                EffectiveAt = DateTime.UtcNow,
                IsActive = true,
                SourceUrl = code switch
                {
                    LegalDocumentCodes.PrivacyNotice => "https://congbao.chinhphu.vn/van-ban/luat-so-91-2025-qh15-45578.htm",
                    _ => null
                },
                Notes = "Default version created by application bootstrap. Replace with counsel-approved text before production launch."
            };
        }

        private static string Sha256Hex(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
