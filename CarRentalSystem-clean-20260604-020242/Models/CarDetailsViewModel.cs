using System;
using System.Collections.Generic;

namespace CarRentalSystem.Models
{
    public class CarDetailsViewModel
    {
        public required Car Car { get; set; }
        public List<AmenityItem> Amenities { get; set; } = new List<AmenityItem>();
        public PricingPackages Pricing { get; set; } = new PricingPackages();
        public List<Review> RecentReviews { get; set; } = new List<Review>();
        
        // Mock data for the demonstration if needed
        public string LocationName { get; set; } = "Quận 1, TP. Hồ Chí Minh";
        public string OwnerName { get; set; } = "BonbonCar";
    }

    public class AmenityItem
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty; // Bootstrap Icon class
    }

    public class PricingPackages
    {
        public decimal Price4h { get; set; }
        public decimal Price8h { get; set; }
        public decimal Price12h { get; set; }
        public decimal Price24h { get; set; }
        public decimal DiscountPercent { get; set; } = 17;
        public decimal DepositAmount { get; set; } = 500000;
        public decimal InsuranceFee { get; set; } = 3000000;
    }
}
