using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class CustomerDrivingLicense
    {
        [Key]
        public int CustomerDrivingLicenseID { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserID { get; set; }
        public User? User { get; set; }

        [MaxLength(500)]
        public string? LicenseNumber { get; set; }

        [MaxLength(64)]
        public string? LicenseNumberHash { get; set; }

        [MaxLength(20)]
        public string LicenseClass { get; set; } = "B2";

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(300)]
        public string? FrontImageUrl { get; set; }

        [MaxLength(300)]
        public string? BackImageUrl { get; set; }

        [MaxLength(30)]
        public string VerificationStatus { get; set; } = "Pending";

        public bool IsApproved { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
