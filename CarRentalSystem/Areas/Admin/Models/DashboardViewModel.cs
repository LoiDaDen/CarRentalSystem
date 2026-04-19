using System.Collections.Generic;
using CarRentalSystem.Models;

namespace CarRentalSystem.Areas.Admin.Models
{
    public class DashboardViewModel
    {
        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int RentedCars { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalBookings { get; set; }
        public List<Booking> RecentBookings { get; set; }
        public List<string> RevenueLabels { get; set; }
        public List<decimal> RevenueData { get; set; }
        public List<TopCarViewModel> TopCars { get; set; }
        public decimal TotalHeldDepositsAmount { get; set; }
        public int TotalHeldDepositsCount { get; set; }
        public Dictionary<string, int> StatusGroups { get; set; }

        // THÊM CHO BIỂU ĐỒ BOOKING & CAR
        public List<string> BookingLabels { get; set; }
        public List<int> BookingData { get; set; }
        public List<string> CarLabels { get; set; }
        public List<int> CarData { get; set; }
    }
}
