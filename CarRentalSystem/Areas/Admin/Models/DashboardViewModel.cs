using CarRentalSystem.Models;

namespace CarRentalSystem.Areas.Admin.Models
{
    public class DashboardViewModel
    {
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime BusinessDate { get; set; }
        public bool HasLoadError { get; set; }
        public string? LoadErrorMessage { get; set; }

        public int TotalUsers { get; set; }
        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int RentedCars { get; set; }
        public int MaintenanceCars { get; set; }
        public int PendingApprovalCars { get; set; }
        public int TotalBookings { get; set; }
        public int TodayBookings { get; set; }
        public int PendingBookings { get; set; }
        public int OpenSupportTickets { get; set; }
        public decimal TodayDepositAmount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalHeldDepositsAmount { get; set; }
        public int TotalHeldDepositsCount { get; set; }
        public decimal CancellationRate30Days { get; set; }

        public int OverdueBookings { get; set; }
        public int MaintenanceDueSoon { get; set; }
        public int StalePendingPayments { get; set; }
        public int PendingEkyc { get; set; }
        public int HighRiskUsers { get; set; }

        public List<Booking> RecentBookings { get; set; } = new();
        public List<Car> PendingCars { get; set; } = new();
        public List<Payment> RecentPayments { get; set; } = new();
        public List<SupportTicket> RecentSupportTickets { get; set; } = new();
        public List<TopCarViewModel> TopCars { get; set; } = new();
        public List<MaintenanceLog> MaintenanceLogs { get; set; } = new();
        public List<TopCustomerViewModel> TopCustomers { get; set; } = new();
        public Dictionary<string, int> StatusGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> DepositLabels { get; set; } = new();
        public List<decimal> DepositData { get; set; } = new();
        public List<string> BookingLabels { get; set; } = new();
        public List<int> BookingData { get; set; } = new();
        public List<string> CarLabels { get; set; } = new();
        public List<int> CarData { get; set; } = new();
    }
}
