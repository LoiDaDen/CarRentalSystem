using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    /// <summary>
    /// HỢP ĐỒNG ĐỐI TÁC TÀI XẾ (HỆ THỐNG XE TỰ LÁI)
    /// Tự động tạo khi Admin duyệt tài xế. Bên B có 7 ngày để ký.
    /// Sau khi ký, hợp đồng có hiệu lực vĩnh viễn cho đến khi Bên B hủy.
    /// </summary>
    public class DriverContract
    {
        [Key]
        public int DriverContractID { get; set; }

        [Required, MaxLength(50)]
        public string ContractNo { get; set; } = string.Empty; // Số: .../2026/HĐ-TXL

        // ═══ Liên kết tài xế ═══
        public int DriverID { get; set; }
        [ForeignKey("DriverID")]
        public Driver? Driver { get; set; }

        // ═══ BÊN A: ĐƠN VỊ QUẢN LÝ HỆ THỐNG (CHỦ XE/CÔNG TY) ═══
        [Required, MaxLength(200)]
        public string PartyAName { get; set; } = "Công ty TNHH Cho Thuê Xe CarRental";
        
        [MaxLength(100)]
        public string? PartyARepresentative { get; set; } // Đại diện
        
        [MaxLength(100)]
        public string? PartyAPosition { get; set; } // Chức vụ
        
        [MaxLength(300)]
        public string? PartyAAddress { get; set; }

        // ═══ BÊN B: ĐỐI TÁC TÀI XẾ ═══
        [Required, MaxLength(100)]
        public string PartyBFullName { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? PartyBCCCD { get; set; }
        
        [MaxLength(20)]
        public string? PartyBLicenseType { get; set; } // GPLX hạng: B1, B2, C, D, E
        
        [MaxLength(500)]
        public string? PartyBLicenseNumber { get; set; } // Số GPLX
        
        public int PartyBExperienceYears { get; set; } // Kinh nghiệm lái xe (năm)

        // ═══ ĐIỀU 1: NỘI DUNG HỢP TÁC ═══
        [MaxLength(20)]
        public string? AssignedVehiclePlate { get; set; } // BKS xe được giao

        // ═══ ĐIỀU 4: PHÍ DỊCH VỤ VÀ THƯỞNG ═══
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; } = 0; // Lương cố định
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TripFee { get; set; } = 0; // Phí chuyến
        
        [MaxLength(500)]
        public string? BonusPolicy { get; set; } // Thưởng đánh giá khách hàng

        public int PaymentDay { get; set; } = 5; // Ngày thanh toán hàng tháng

        // ═══ NỘI DUNG HỢP ĐỒNG ĐẦY ĐỦ ═══
        public string? TermsContent { get; set; } // Toàn bộ nội dung điều khoản

        // ═══ TRẠNG THÁI HỢP ĐỒNG ═══
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Active, Expired, Cancelled

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } // Hạn ký = CreatedAt + 7 ngày

        // Ký bên A (Admin/Platform)
        public bool SignedByPartyA { get; set; } = true; // Auto-sign khi tạo
        public DateTime? PartyASignDate { get; set; }

        // Ký bên B (Tài xế)
        public bool SignedByPartyB { get; set; } = false;
        public DateTime? PartyBSignDate { get; set; }

        // Hủy hợp đồng
        public DateTime? CancelledAt { get; set; }
        [MaxLength(500)]
        public string? CancelReason { get; set; }
    }
}
