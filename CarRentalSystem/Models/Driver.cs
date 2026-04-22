using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Driver
    {
        [Key]
        public int DriverID { get; set; }
        
        public int? UserID { get; set; } // Linked user account (nullable for standalone drivers)
        [ForeignKey("UserID")]
        public User? User { get; set; }
        
        [Required, MaxLength(100)]
        public required string FullName { get; set; }
        
        [MaxLength(20)]
        public string? Phone { get; set; }
        
        [MaxLength(100)]
        public string? Email { get; set; }
        
        [MaxLength(50)]
        public string? LicenseNumber { get; set; }
        
        [MaxLength(20)]
        public string LicenseType { get; set; } = "B2"; // B1, B2, C, D, E
        
        public int ExperienceYears { get; set; } = 1;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRate { get; set; } = 500000; // 500K/day default
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; } = 80000; // 80K/hour default
        
        public bool IsAvailable { get; set; } = true;
        public bool IsApproved { get; set; } = false; // Admin must approve
        
        [Column(TypeName = "decimal(3,1)")]
        public decimal Rating { get; set; } = 5.0m;
        
        public int TotalTrips { get; set; } = 0;
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [MaxLength(300)]
        public string? AvatarUrl { get; set; }
        
        // Driver documents
        [MaxLength(300)]
        public string? LicenseFrontUrl { get; set; }
        [MaxLength(300)]
        public string? LicenseBackUrl { get; set; }
        [MaxLength(300)]
        public string? IdCardUrl { get; set; }
        
        // Location
        public int? LocationID { get; set; }
        [ForeignKey("LocationID")]
        public Location? Location { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<DriverBooking>? DriverBookings { get; set; } = new List<DriverBooking>();
        public ICollection<DriverReview>? DriverReviews { get; set; } = new List<DriverReview>();

        // Phase 10: Account Control
        public bool IsLocked { get; set; } = false;
        public DateTime? LockedAt { get; set; }
        public bool IsBlacklisted { get; set; } = false;
    }
}
