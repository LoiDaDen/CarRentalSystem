using System;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class ContactMessage
    {
        [Key]
        public int MessageID { get; set; }

        [Required(ErrorMessage = "Tên là bắt buộc")]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100)]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
        [StringLength(200)]
        public string Subject { get; set; } = null!;

        [Required(ErrorMessage = "Nội dung là bắt buộc")]
        public string Message { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;

        public string? AdminResponse { get; set; }
    }
}
