using CarRentalSystem.Models;

namespace CarRentalSystem.Areas.Admin.Models
{
    public class TopCarViewModel
    {
        public Car Car { get; set; }
        public int RentalCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class TopCustomerViewModel
    {
        public User User { get; set; }
        public int BookingCount { get; set; }
        public decimal TotalSpent { get; set; }
    }

    /// <summary>
    /// Báo cáo công suất sử dụng từng xe trong đội xe.
    /// Utilization % = (Ngày thuê / Tổng ngày trong tháng) × 100
    /// </summary>
    public class FleetUtilizationViewModel
    {
        public int CarID { get; set; }
        public string CarName { get; set; } = "";
        public string LicensePlate { get; set; } = "";
        public string Status { get; set; } = "";
        public int TotalBookings { get; set; }
        public int TotalRentalDays { get; set; }
        public decimal UtilizationPercent { get; set; }
        public decimal Revenue { get; set; }
        public decimal MaintenanceCost { get; set; }
        public decimal Profit { get; set; }
    }
}
