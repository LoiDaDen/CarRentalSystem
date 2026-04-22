// CarRentalSystem/ViewModels/Areas/AreaViewModel.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.ViewModels.Areas
{
    // ═══════════════════════════════════════════════════
    // 📋 AREA CREATE VIEW MODEL
    // ═══════════════════════════════════════════════════
    public class AreaCreateViewModel
    {
        [Required(ErrorMessage = "Tên khu vực là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Tên khu vực không quá 100 ký tự.")]
        public required string Name { get; set; }

        [Required(ErrorMessage = "Mã khu vực là bắt buộc.")]
        [MaxLength(20, ErrorMessage = "Mã khu vực không quá 20 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã khu vực chỉ chứa chữ hoa, số và dấu gạch dưới.")]
        public required string Code { get; set; }

        [Required(ErrorMessage = "Tỉnh/Thành phố là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Tỉnh/Thành phố không quá 100 ký tự.")]
        public required string Province { get; set; }

        [MaxLength(300, ErrorMessage = "Địa chỉ không quá 300 ký tự.")]
        public string? Address { get; set; }

        [Range(-90, 90, ErrorMessage = "Vĩ độ không hợp lệ.")]
        public double? Latitude { get; set; }

        [Range(-180, 180, ErrorMessage = "Kinh độ không hợp lệ.")]
        public double? Longitude { get; set; }

        [Required(ErrorMessage = "Trạng thái là bắt buộc.")]
        public string Status { get; set; } = "active";

        public int? ManagerUserId { get; set; }

        public List<int> LocationIds { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════
    // ✏️ AREA EDIT VIEW MODEL
    // ═══════════════════════════════════════════════════
    public class AreaEditViewModel
    {
        public int AreaID { get; set; }

        [Required(ErrorMessage = "Tên khu vực là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Tên khu vực không quá 100 ký tự.")]
        public required string Name { get; set; }

        [Required(ErrorMessage = "Mã khu vực là bắt buộc.")]
        [MaxLength(20, ErrorMessage = "Mã khu vực không quá 20 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã khu vực chỉ chứa chữ hoa, số và dấu gạch dưới.")]
        public required string Code { get; set; }

        [Required(ErrorMessage = "Tỉnh/Thành phố là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Tỉnh/Thành phố không quá 100 ký tự.")]
        public required string Province { get; set; }

        [MaxLength(300, ErrorMessage = "Địa chỉ không quá 300 ký tự.")]
        public string? Address { get; set; }

        [Range(-90, 90, ErrorMessage = "Vĩ độ không hợp lệ.")]
        public double? Latitude { get; set; }

        [Range(-180, 180, ErrorMessage = "Kinh độ không hợp lệ.")]
        public double? Longitude { get; set; }

        [Required(ErrorMessage = "Trạng thái là bắt buộc.")]
        public string Status { get; set; } = "active";

        public int? ManagerID { get; set; }

        public string Status_ReadOnly { get; set; } = "";

        [NotMapped]
        public List<int> LocationIds { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════
    // 📊 AREA STATS VIEW MODEL
    // ═══════════════════════════════════════════════════
    public class AreaStatsViewModel
    {
        public int AreaID { get; set; }
        public string AreaName { get; set; } = "";

        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int RentedCars => TotalCars - AvailableCars;
        public int TotalStaffs { get; set; }
        public int TotalLocations { get; set; }

        public int MonthlyOrders { get; set; }
        public decimal MonthlyRevenue { get; set; }

        public string MonthlyRevenueFormatted => MonthlyRevenue.ToString("N0") + " ₫";
        public string UtilizationRateFormatted => TotalCars > 0
            ? Math.Round((decimal)RentedCars / TotalCars * 100, 1).ToString("N1") + "%"
            : "0%";
    }

    public class AreaStatsChartPoint
    {
        public string Date { get; set; } = "";
        public int Count { get; set; }
        public decimal Revenue { get; set; }
        public string RevenueFormatted => Revenue.ToString("N0") + " ₫";
    }

    // ═══════════════════════════════════════════════════
    // 👥 AREA STAFF VIEW MODELS
    // ═══════════════════════════════════════════════════
    public class AreaStaffAddViewModel
    {
        public int AreaID { get; set; }

        [Required(ErrorMessage = "User là bắt buộc.")]
        public int UserID { get; set; }

        [Required(ErrorMessage = "Vai trò trong khu vực là bắt buộc.")]
        [RegularExpression(@"^(manager|staff|viewer)$",
            ErrorMessage = "Vai trò chỉ nhận: manager, staff, viewer.")]
        public required string RoleInArea { get; set; }
    }

    public class AreaStaffUpdateRoleViewModel
    {
        public int StaffID { get; set; }

        [Required(ErrorMessage = "Vai trò mới là bắt buộc.")]
        [RegularExpression(@"^(manager|staff|viewer)$",
            ErrorMessage = "Vai trò chỉ nhận: manager, staff, viewer.")]
        public required string NewRole { get; set; }
    }

    // ═══════════════════════════════════════════════════
    // 📍 AREA LOCATION VIEW MODELS
    // ═══════════════════════════════════════════════════
    public class AreaLocationAddViewModel
    {
        public int AreaID { get; set; }

        [Required(ErrorMessage = "Location là bắt buộc.")]
        public int LocationID { get; set; }
    }

    // ═══════════════════════════════════════════════════
    // 🔍 API RESULT VIEW MODELS
    // ═══════════════════════════════════════════════════
    public class UserSelect2Result
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class LocationSelect2Result
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public int CarCount { get; set; }
    }
}
