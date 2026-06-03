using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class OwnerDigitalSignatureProfile
    {
        [Key]
        public int OwnerDigitalSignatureProfileID { get; set; }

        [Required]
        public int UserID { get; set; }

        [ForeignKey(nameof(UserID))]
        public User? User { get; set; }

        [Required]
        [MaxLength(120)]
        public string ProviderName { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string CertificateSerialNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string CertificateSubject { get; set; } = string.Empty;

        public DateTime? CertificateValidUntil { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = OwnerDigitalSignatureProfileStatuses.Pending;

        [MaxLength(500)]
        public string? SignatureImageUrl { get; set; }

        [MaxLength(64)]
        public string? SignatureImageHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }

        public bool IsDeleted { get; set; }

        [NotMapped]
        public bool IsReady =>
            Status == OwnerDigitalSignatureProfileStatuses.Active
            && !string.IsNullOrWhiteSpace(ProviderName)
            && !string.IsNullOrWhiteSpace(CertificateSerialNumber)
            && !string.IsNullOrWhiteSpace(CertificateSubject)
            && !string.IsNullOrWhiteSpace(SignatureImageHash)
            && (!CertificateValidUntil.HasValue || CertificateValidUntil.Value.Date >= DateTime.UtcNow.Date);
    }

    public static class OwnerDigitalSignatureProfileStatuses
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Revoked = "Revoked";
    }
}
