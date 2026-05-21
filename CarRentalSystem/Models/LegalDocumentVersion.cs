using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class LegalDocumentVersion
    {
        [Key]
        public int LegalDocumentVersionID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string Version { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string ContentHash { get; set; } = string.Empty;

        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
        public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
        public DateTime? RetiredAt { get; set; }
        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? SourceUrl { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public ICollection<ConsentRecord> ConsentRecords { get; set; } = new List<ConsentRecord>();
    }

    public static class LegalDocumentCodes
    {
        public const string TermsOfUse = "TermsOfUse";
        public const string PrivacyNotice = "PrivacyNotice";
        public const string EkycBiometricConsent = "EkycBiometricConsent";
        public const string SelfDriveAgreement = "SelfDriveAgreement";
        public const string WalletLedgerNotice = "WalletLedgerNotice";
    }
}
