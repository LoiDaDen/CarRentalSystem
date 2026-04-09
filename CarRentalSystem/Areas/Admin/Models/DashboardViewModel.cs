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
    }
}
