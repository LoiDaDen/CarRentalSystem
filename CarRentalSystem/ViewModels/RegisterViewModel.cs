using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và có 10-11 chữ số.")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập ngày sinh.")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập CCCD.")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD phải đúng 12 chữ số.")]
        [MaxLength(12)]
        public string CitizenId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số giấy phép lái xe.")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "Số giấy phép lái xe phải đúng 12 chữ số.")]
        [MaxLength(12)]
        public string DrivingLicenseNumber { get; set; } = string.Empty;

        [MaxLength(6)]
        public string? ReferralCode { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý Điều khoản sử dụng.")]
        public bool AcceptTerms { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải xác nhận đã đọc Thông báo xử lý dữ liệu cá nhân.")]
        public bool AcceptPrivacyNotice { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý xử lý CCCD/GPLX và dữ liệu nhạy cảm phục vụ xác minh thuê xe.")]
        public bool AcceptSensitiveDataProcessing { get; set; }
    }
}
