using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class HolidayPricing
    {
        [Key]
        public int HolidayPricingID { get; set; }
        
        public int? CarID { get; set; } // Null = applies to all cars
        [ForeignKey("CarID")]
        public Car? Car { get; set; }
        
        [Required, MaxLength(100)]
        public required string HolidayName { get; set; } // "Tết Nguyên Đán 2026", "30/4 - 1/5"
        
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        [Column(TypeName = "decimal(4,2)")]
        public decimal PriceMultiplier { get; set; } = 1.50m; // 1.5 = tăng 50%
        
        public bool IsActive { get; set; } = true;
    }
}
