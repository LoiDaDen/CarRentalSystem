using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class DynamicPricingRule
    {
        [Key]
        public int RuleID { get; set; }
        
        [StringLength(100)]
        public string RuleName { get; set; } = "Surge";
        
        public int? LocationID { get; set; }
        [ForeignKey("LocationID")]
        public Location? Location { get; set; }
        
        // Multiplier to base rate (e.g., 1.5 for 50% surge)
        [Column(TypeName = "decimal(5,2)")]
        public decimal Multiplier { get; set; } = 1.0m;
        
        public DateTime ValidFrom { get; set; }
        public DateTime ValidUntil { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
}
