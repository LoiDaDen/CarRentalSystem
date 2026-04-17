using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    /// <summary>
    /// Hóa đơn xuất cho khách hàng sau hoàn tất booking.
    /// Bao gồm chi tiết tổng tiền, thuế, phí phát sinh.
    /// </summary>
    public class Invoice
    {
        [Key]
        public int InvoiceID { get; set; }

        /// <summary>Mã hóa đơn duy nhất: INV-20260418-0001</summary>
        [Required, MaxLength(50)]
        public required string InvoiceNumber { get; set; }

        /// <summary>Booking liên kết</summary>
        public int BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

        /// <summary>Khách hàng</summary>
        public int UserID { get; set; }
        [ForeignKey("UserID")]
        public User? User { get; set; }

        /// <summary>Ngày xuất hóa đơn</summary>
        public DateTime IssueDate { get; set; } = DateTime.Now;

        /// <summary>Hạn thanh toán</summary>
        public DateTime? DueDate { get; set; }

        // ═══ Chi tiết tài chính ═══

        /// <summary>Tiền thuê xe cơ bản</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal RentalAmount { get; set; }

        /// <summary>Phí tài xế</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DriverFee { get; set; } = 0;

        /// <summary>Phí giao xe</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryFee { get; set; } = 0;

        /// <summary>Phí dịch vụ thêm (add-ons)</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal AddOnFee { get; set; } = 0;

        /// <summary>Phí trả trễ</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LateFee { get; set; } = 0;

        /// <summary>Tổng phụ (chưa thuế, chưa giảm)</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        /// <summary>Giảm giá (voucher, membership)</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        /// <summary>Thuế VAT (%)</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxPercent { get; set; } = 10; // 10% VAT

        /// <summary>Số tiền thuế</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        /// <summary>Tổng thanh toán cuối cùng</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        /// <summary>Số tiền đặt cọc đã trừ</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositDeducted { get; set; } = 0;

        /// <summary>Số tiền còn phải trả</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountDue { get; set; }

        // ═══ Trạng thái ═══

        /// <summary>Draft, Issued, Paid, Overdue, Cancelled</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "Draft";

        /// <summary>Phương thức thanh toán: Cash, BankTransfer, MoMo, VNPay, v.v.</summary>
        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        /// <summary>Ghi chú nội bộ</summary>
        [MaxLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Thông tin người tạo hóa đơn</summary>
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }
    }
}
