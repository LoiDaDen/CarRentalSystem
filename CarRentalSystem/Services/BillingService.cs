using System;
using System.Linq;
using System.Threading.Tasks;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public class BillingService : IBillingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHolidayPricingService _holidayService;
        
        public BillingService(ApplicationDbContext context, IHolidayPricingService holidayService)
        {
            _context = context;
            _holidayService = holidayService;
        }

        public async Task<BillingQuotation> CalculateQuotationAsync(int carId, int userId, DateTime startDate, DateTime endDate, bool withDriver, decimal addOnFees)
        {
            var car = await _context.Cars.FindAsync(carId);
            if (car == null) throw new Exception("Car not found");

            var user = await _context.Users
                .Include(u => u.MembershipTier)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            int totalHours = (int)Math.Ceiling((endDate - startDate).TotalHours);
            if (totalHours <= 0) totalHours = 1;

            decimal baseRate = car.PricePerHour;
            
            // ═══ TÍNH MULTIPLIER TỪ SERVICE ═══
            // Ưu tiên: Custom DB pricing > Holiday Service pricing
            decimal durationMultiplier = 1.0m;
            
            var holidayPricings = await _context.HolidayPricings
                .Where(h => h.IsActive && (h.CarID == null || h.CarID == carId))
                .Where(h => (h.StartDate <= startDate && h.EndDate >= startDate) || (h.StartDate <= endDate && h.EndDate >= endDate))
                .ToListAsync();

            if (holidayPricings.Any())
            {
                durationMultiplier = holidayPricings.Max(h => h.PriceMultiplier);
            }
            else
            {
                // Không có custom pricing -> dùng Holiday Pricing Service
                durationMultiplier = _holidayService.GetMultiplier(startDate, endDate);
            }

            decimal driverFee = 0;
            if (withDriver)
            {
                driverFee = 80000m * totalHours; 
            }

            decimal membershipDiscountPercent = user?.MembershipTier?.DiscountPercent ?? 0m;

            return new BillingQuotation
            {
                BaseRate = baseRate,
                TotalHours = totalHours,
                DurationMultiplier = durationMultiplier,
                DriverFee = driverFee,
                AdminFee = 50000m,
                MembershipDiscountPercent = membershipDiscountPercent,
                AddOnFees = addOnFees
            };
        }
    }
}
