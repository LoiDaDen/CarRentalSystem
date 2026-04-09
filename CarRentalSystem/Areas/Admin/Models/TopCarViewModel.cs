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
}
