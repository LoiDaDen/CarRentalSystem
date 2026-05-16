using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Driver
    {
        [Key]
        public int DriverID { get; set; }

        [ForeignKey("User")]
        public int? UserID { get; set; } // Linked user account (nullable for standalone drivers)
        public User? User { get; set; }

        [ForeignKey("Partner")]
        public int? PartnerID { get; set; }
        public User? Partner { get; set; }

        [Required]
        [MaxLength(100)]
        public required string FullName { get; set; }

        [Required]
        [MaxLength(20)]
        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và có 10-11 chữ số")]
        public string? Phone { get; set; }

        [MaxLength(100)]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        [Required]
        [MaxLength(50)]
        public string? LicenseNumber { get; set; }

        [MaxLength(20)]
        public string LicenseType { get; set; } = "B2"; // B1, B2, C, D, E

        public DateTime? DrivingLicenseExpiryDate { get; set; }

        [Required]
        [Range(0, 70, ErrorMessage = "Số năm kinh nghiệm phải từ 0 đến 70 năm")]
        public int ExperienceYears { get; set; } = 1;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Giá thuê ngày phải lớn hơn 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRate { get; set; } = 500000; // 500K/day default

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Giá thuê giờ phải lớn hơn 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; } = 80000; // 80K/hour default

        public bool IsAvailable { get; set; } = true;
        public bool IsApproved { get; set; } = false; // Admin must approve

        [MaxLength(30)]
        public string Status { get; set; } = "Pending";

        [Range(0, 5, ErrorMessage = "Điểm đánh giá phải từ 0 đến 5 sao")]
        [Column(TypeName = "decimal(3,1)")]
        public decimal Rating { get; set; } = 5.0m;

        [Range(0, int.MaxValue)]
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
        [ForeignKey("Location")]
        public int? LocationID { get; set; }
        public Location? Location { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<DriverBooking>? DriverBookings { get; set; } = new List<DriverBooking>();
        public ICollection<DriverReview>? DriverReviews { get; set; } = new List<DriverReview>();

        // Phase 10: Account Control
        public bool IsLocked { get; set; } = false;
        public DateTime? LockedAt { get; set; }
        public bool IsBlacklisted { get; set; } = false;
    }
}
