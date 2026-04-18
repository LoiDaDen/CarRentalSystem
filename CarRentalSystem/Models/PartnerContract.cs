using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    /// <summary>
    /// HỢP ĐỒNG HỢP TÁC KÝ GỬI & VẬN HÀNH XE TỰ LÁI
    /// Tự động tạo khi Admin verify đối tác. Bên B có 7 ngày để ký.
    /// Sau khi ký, hợp đồng có hiệu lực vĩnh viễn cho đến khi Bên B hủy.
    /// </summary>
    public class PartnerContract
    {
        [Key]
        public int PartnerContractID { get; set; }

        [Required, MaxLength(50)]
        public string ContractNo { get; set; } = string.Empty; // Số: .../2024/HĐHT-XT

        // ═══ Liên kết đối tác ═══
        public int PartnerUserID { get; set; }
        [ForeignKey("PartnerUserID")]
        public User? PartnerUser { get; set; }

        // ═══ BÊN A: ĐƠN VỊ VẬN HÀNH ═══
        [Required, MaxLength(200)]
        public string PartyAName { get; set; } = "Công ty TNHH Cho Thuê Xe CarRental";
        
        [MaxLength(300)]
        public string? PartyAAddress { get; set; }

        // ═══ BÊN B: CHỦ XE (ĐỐI TÁC) ═══
        [Required, MaxLength(100)]
        public string PartyBFullName { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string? PartyBCCCD { get; set; }
        
        [MaxLength(200)]
        public string? VehicleInfo { get; set; } // Thông tin xe
        
        [MaxLength(20)]
        public string? VehiclePlate { get; set; } // Biển số
        
        [MaxLength(500)]
        public string? VehicleCondition { get; set; } // Tình trạng xe hiện tại

        // ═══ ĐIỀU 2: PHÂN CHIA DOANH THU (80/20) ═══
        [Column(TypeName = "decimal(5,2)")]
        public decimal PartyBRevenuePercent { get; set; } = 80.00m; // Chủ xe nhận 80%
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal PartyARevenuePercent { get; set; } = 20.00m; // Vận hành nhận 20%

        public int PaymentDay { get; set; } = 5; // Ngày thanh toán
        
        [MaxLength(20)]
        public string PaymentCycle { get; set; } = "Hàng tháng"; // Hàng tuần / Hàng tháng

        // ═══ ĐIỀU 5: THỜI HẠN ═══
        public int ContractDurationMonths { get; set; } = 0; // 0 = vĩnh viễn
        public int NoticePeriodDays { get; set; } = 15; // Báo trước X ngày trước khi chấm dứt

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

        // Ký bên B (Đối tác)
        public bool SignedByPartyB { get; set; } = false;
        public DateTime? PartyBSignDate { get; set; }

        // Hủy hợp đồng
        public DateTime? CancelledAt { get; set; }
        [MaxLength(500)]
        public string? CancelReason { get; set; }
    }
}
