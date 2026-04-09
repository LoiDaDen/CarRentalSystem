using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Promotion
    {
        [Key]
        public int PromotionID { get; set; }
        
        [Required, StringLength(50)]
        public string PromoCode { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountPercent { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; }
        
        public DateTime ValidFrom { get; set; }
        
        [Required]
        public DateTime ValidUntil { get; set; }
        
        public int UsageLimit { get; set; }
        
        public int UsedCount { get; set; } = 0;
        
        public bool IsActive { get; set; } = true;
    }
}
