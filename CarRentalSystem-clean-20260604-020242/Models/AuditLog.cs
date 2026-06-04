using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    /// <summary>
    /// Ghi lại toàn bộ thao tác quan trọng trên hệ thống.
    /// Dùng cho truy vết, kiểm toán, và bảo mật.
    /// </summary>
    public class AuditLog
    {
        public const int RequestUrlMaxLength = 2048;

        [Key]
        public long AuditLogID { get; set; }

        /// <summary>Loại hành động: Create, Update, Delete, Login, Logout, Export, v.v.</summary>
        [Required, MaxLength(50)]
        public required string Action { get; set; }

        /// <summary>Entity/module bị tác động: Car, Booking, User, Payment, v.v.</summary>
        [Required, MaxLength(100)]
        public required string EntityType { get; set; }

        /// <summary>ID của entity bị tác động (nullable cho thao tác không liên quan entity cụ thể)</summary>
        public int? EntityId { get; set; }

        /// <summary>User thực hiện thao tác</summary>
        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        /// <summary>Tên user (cache lại phòng user bị xóa)</summary>
        [MaxLength(100)]
        public string? UserName { get; set; }

        /// <summary>Mô tả chi tiết thao tác</summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>JSON snapshot giá trị cũ trước khi thay đổi</summary>
        public string? OldValues { get; set; }

        /// <summary>JSON snapshot giá trị mới sau khi thay đổi</summary>
        public string? NewValues { get; set; }

        /// <summary>Địa chỉ IP của người thực hiện</summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /// <summary>User-Agent của trình duyệt</summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /// <summary>HTTP Method + URL của request</summary>
        [MaxLength(RequestUrlMaxLength)]
        public string? RequestUrl { get; set; }

        [MaxLength(1000)]
        public string? Reason { get; set; }

        [MaxLength(64)]
        public string? CorrelationId { get; set; }

        [MaxLength(64)]
        public string? PreviousHash { get; set; }

        [MaxLength(64)]
        public string? RecordHash { get; set; }

        public int HashVersion { get; set; } = 1;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
