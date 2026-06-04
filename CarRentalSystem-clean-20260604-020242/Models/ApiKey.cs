using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class ApiKey
    {
        [Key]
        public int ApiKeyID { get; set; }

        public int OwnerUserID { get; set; }
        public User? OwnerUser { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(32)]
        public string KeyPrefix { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string KeyHash { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Scopes { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Status { get; set; } = ApiKeyStatusConstants.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public int? RevokedByUserID { get; set; }
        public User? RevokedByUser { get; set; }
    }

    public static class ApiKeyStatusConstants
    {
        public const string Active = "Active";
        public const string Revoked = "Revoked";
        public const string Expired = "Expired";
    }
}
