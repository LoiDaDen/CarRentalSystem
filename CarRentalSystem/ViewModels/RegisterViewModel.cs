using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui long nhap ho ten.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui long nhap so dien thoai.")]
        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "So dien thoai phai bat dau bang 0 va co 10-11 chu so.")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui long nhap email.")]
        [EmailAddress(ErrorMessage = "Email khong hop le.")]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui long nhap mat khau.")]
        [MinLength(6, ErrorMessage = "Mat khau phai co it nhat 6 ky tu.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui long xac nhan mat khau.")]
        [Compare(nameof(Password), ErrorMessage = "Mat khau xac nhan khong khop.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [MaxLength(6)]
        public string? ReferralCode { get; set; }
    }
}
