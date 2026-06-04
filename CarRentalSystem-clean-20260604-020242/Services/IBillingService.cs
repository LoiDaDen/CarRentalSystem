using System;
using System.Threading.Tasks;

namespace CarRentalSystem.Services
{
    public interface IBillingService
    {
        Task<BillingQuotation> CalculateQuotationAsync(int carId, int userId, DateTime startDate, DateTime endDate, bool withDriver, decimal addOnFees);
    }
    
    public class BillingQuotation
    {
        public decimal BaseRate { get; set; }
        public int TotalHours { get; set; }
        public decimal DurationMultiplier { get; set; } = 1.0m;
        public decimal DriverFee { get; set; }
        public decimal AdminFee { get; set; }
        public decimal MembershipDiscountPercent { get; set; }
        
        public decimal MembershipDiscount => (BaseRate * TotalHours * DurationMultiplier) * (MembershipDiscountPercent / 100m);
        
        public decimal AddOnFees { get; set; }
        
        public decimal TotalAmount => (BaseRate * TotalHours * DurationMultiplier) + DriverFee + AdminFee + AddOnFees - MembershipDiscount;
    }
}
