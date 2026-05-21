using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class ConsentRecord
    {
        [Key]
        public int ConsentRecordID { get; set; }

        public int UserID { get; set; }
        public User? User { get; set; }

        public int LegalDocumentVersionID { get; set; }
        public LegalDocumentVersion? LegalDocumentVersion { get; set; }

        [Required]
        [MaxLength(80)]
        public string PurposeCode { get; set; } = string.Empty;

        public bool IsGranted { get; set; } = true;
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public DateTime? WithdrawnAt { get; set; }

        [MaxLength(80)]
        public string Source { get; set; } = string.Empty;

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(64)]
        public string? ConsentTextHash { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? EvidenceJson { get; set; }
    }

    public static class ConsentPurposeCodes
    {
        public const string AccountRegistration = "AccountRegistration";
        public const string PrivacyNoticeAcknowledgement = "PrivacyNoticeAcknowledgement";
        public const string SensitiveDataProcessing = "SensitiveDataProcessing";
        public const string EkycIdentityVerification = "EkycIdentityVerification";
        public const string EkycBiometricProcessing = "EkycBiometricProcessing";
        public const string BookingTerms = "BookingTerms";
        public const string SelfDriveAgreement = "SelfDriveAgreement";
        public const string TrackingOperationalUse = "TrackingOperationalUse";
        public const string WalletLedgerOnly = "WalletLedgerOnly";
    }
}
