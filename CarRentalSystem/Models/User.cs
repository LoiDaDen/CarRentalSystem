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
        public string Role { get; set; } = "Customer"; // Admin, Manager, Staff, Customer, Partner

        public static readonly string[] AllRoles = { "Admin", "Manager", "Staff", "Customer", "Partner" };
        public static readonly string[] ManageableRoles = { "Manager", "Staff", "Customer" }; // Roles that can be changed by Admin
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

        // 🔐 SHA-256 hash để check trùng CCCD (vì ciphertext không deterministic)
        [MaxLength(64)]
        public string? CitizenIdHash { get; set; }

        // 🔐 SHA-256 hash để check trùng GPLX
        [MaxLength(64)]
        public string? DrivingLicenseHash { get; set; }

        // 🔐 SHA-256 hash để check trùng SĐT
        [MaxLength(64)]
        public string? PhoneHash { get; set; }
        
        // Email Verification
        public bool IsEmailVerified { get; set; } = false;
        [MaxLength(100)]
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }

        public bool IsVerified { get; set; } = false;
        
        public bool IsBlacklisted { get; set; } = false;
        [MaxLength(500)]
        public string? BlacklistReason { get; set; }

        [MaxLength(6)]
        public string? ReferralCode { get; set; }

        public int? ReferredByUserID { get; set; }
        [ForeignKey("ReferredByUserID")]
        public User? ReferredByUser { get; set; }
        public ICollection<User>? ReferredUsers { get; set; } = new List<User>();

        public DateTime? ReferralDiscountUsedAt { get; set; }

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

        // Partner Approval Status
        public bool IsPartnerApproved { get; set; } = false;
        public DateTime? PartnerApprovedAt { get; set; }

        // Lưu role gốc để Admin/Staff đăng ký partner không mất quyền
        [MaxLength(20)]
        public string? OriginalRole { get; set; }

        // Phase 5: Membership & Loyalty
        public int? MembershipTierID { get; set; }
        [ForeignKey("MembershipTierID")]
        public MembershipTier? MembershipTier { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSpending { get; set; } = 0;
        
        public int LoyaltyPoints { get; set; } = 0;

        // Phase 10: Account Control
        public bool IsLocked { get; set; } = false;
        public DateTime? LockedAt { get; set; }

        // Phase 10: Area Management
        public ICollection<AreaStaff>? AreaStaffs { get; set; } = new List<AreaStaff>();

        public bool HasRole(string role) => Role == role;

        public bool BelongsToArea(int areaId) =>
            AreaStaffs?.Any(asx => asx.AreaID == areaId) ?? false;
    }
}
