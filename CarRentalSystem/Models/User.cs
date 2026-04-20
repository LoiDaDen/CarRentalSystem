using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class User
    {
        [Key]
        public int UserID { get; set; }
        [Required, MaxLength(100)]
        public required string Name { get; set; }
        [Required, EmailAddress, MaxLength(100)]
        public required string Email { get; set; }
        [MaxLength(20)]
        public string? Phone { get; set; }
        [Required]
        public required string PasswordHash { get; set; }
        [MaxLength(20)]
        public string Role { get; set; } = "Customer"; // Admin, Customer, Staff, Partner
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Booking>? Bookings { get; set; } = new List<Booking>();
        public ICollection<Review>? Reviews { get; set; } = new List<Review>();
        
        // Phase 4: P2P Host Engine (Marketplace)
        [InverseProperty("Owner")]
        public ICollection<Car>? OwnedCars { get; set; } = new List<Car>();

        // Phase 2: Risk Management & eKYC
        [MaxLength(255)]
        public string? IdCardFrontUrl { get; set; }
        [MaxLength(255)]
        public string? IdCardBackUrl { get; set; }
        [MaxLength(255)]
        public string? DrivingLicenseUrl { get; set; }
        [MaxLength(255)]
        public string? DrivingLicenseBackUrl { get; set; }
        [MaxLength(255)]
        public string? FaceUrl { get; set; }
        
        // Trạng thái eKYC: "Pending", "Verified", "Rejected"
        [MaxLength(20)]
        public string VerificationStatus { get; set; } = "Pending";
        
        // 🔒 Giới hạn 12 số — validated in Controller, encrypted at rest (MaxLength lớn vì ciphertext dài)
        [MaxLength(500)]
        public string? DrivingLicenseNumber { get; set; }
        
        // Email Verification
        public bool IsEmailVerified { get; set; } = false;
        [MaxLength(100)]
        public string? EmailVerificationToken { get; set; }

        public bool IsVerified { get; set; } = false;
        
        public bool IsBlacklisted { get; set; } = false;
        [MaxLength(500)]
        public string? BlacklistReason { get; set; }

        // 🔒 Mã hóa PII at rest — MaxLength lớn vì ciphertext dài
        [MaxLength(500)]
        public string? CitizenId { get; set; }

        // 🔐 Mã PIN 6 số để xem thông tin PII (BCrypt hash)
        [MaxLength(200)]
        public string? PiiPinHash { get; set; }
        
        [MaxLength(200)]
        public string? Address { get; set; }
        
        [MaxLength(10)]
        public string? Gender { get; set; }
        
        public DateTime? DateOfBirth { get; set; }

        // Phase 4: Partner Location
        [MaxLength(300)]
        public string? PartnerAddress { get; set; }
        public double? PartnerLatitude { get; set; }
        public double? PartnerLongitude { get; set; }

        // Phase 5: Membership & Loyalty
        public int? MembershipTierID { get; set; }
        [ForeignKey("MembershipTierID")]
        public MembershipTier? MembershipTier { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSpending { get; set; } = 0;
        
        public int LoyaltyPoints { get; set; } = 0;
    }
}
