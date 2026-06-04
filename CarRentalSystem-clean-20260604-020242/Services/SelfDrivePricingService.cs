using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public record SelfDrivePriceResult(int TotalHours, int RentalDays, decimal BaseRentalAmount);

    public interface ISelfDrivePricingService
    {
        SelfDrivePriceResult Calculate(Car car, DateTime startDate, DateTime endDate);
    }

    public class SelfDrivePricingService : ISelfDrivePricingService
    {
        public SelfDrivePriceResult Calculate(Car car, DateTime startDate, DateTime endDate)
        {
            if (endDate <= startDate)
                throw new InvalidOperationException("Ngày trả xe phải sau ngày nhận xe.");

            var totalHours = (int)Math.Ceiling((endDate - startDate).TotalHours);
            if (totalHours <= 0) totalHours = 1;

            var rentalDays = Math.Max(1, (int)Math.Ceiling(totalHours / 24.0));
            return new SelfDrivePriceResult(totalHours, rentalDays, CalculateRental(car, totalHours));
        }

        private static decimal CalculateRental(Car car, int hours)
        {
            var pricePerHour = car.PricePerHour > 0 ? car.PricePerHour : Math.Round(car.PricePerDay / 8m);
            var p4 = car.PricePerDay > 0 ? car.PricePerDay * 0.4m : 4 * pricePerHour;
            var p8 = car.PricePerDay > 0 ? car.PricePerDay * 0.7m : 8 * pricePerHour;
            var p12 = car.PricePerDay > 0 ? car.PricePerDay * 0.85m : 12 * pricePerHour;
            var p24 = car.PricePerDay > 0 ? car.PricePerDay : 24 * pricePerHour;

            if (hours <= 0) return 0m;
            if (hours <= 4) return Math.Min(hours * pricePerHour, p4);
            if (hours <= 8) return Math.Min(p4 + (hours - 4) * pricePerHour, p8);
            if (hours <= 12) return Math.Min(p8 + (hours - 8) * pricePerHour, p12);
            if (hours <= 24) return Math.Min(p12 + (hours - 12) * pricePerHour, p24);

            var fullDays = hours / 24;
            var rem = hours % 24;
            var remPrice = rem switch
            {
                <= 0 => 0m,
                <= 4 => Math.Min(rem * pricePerHour, p4),
                <= 8 => Math.Min(p4 + (rem - 4) * pricePerHour, p8),
                <= 12 => Math.Min(p8 + (rem - 8) * pricePerHour, p12),
                _ => Math.Min(p12 + (rem - 12) * pricePerHour, p24)
            };

            return (fullDays * p24) + remPrice;
        }
    }
}
