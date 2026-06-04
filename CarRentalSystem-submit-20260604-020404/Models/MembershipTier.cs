using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class MembershipTier
    {
        [Key]
        public int MembershipTierID { get; set; }
        
        [Required, MaxLength(50)]
        public required string Name { get; set; } // Bronze, Silver, Gold, Platinum, Diamond
        
        public decimal MinSpending { get; set; } = 0; // Minimum total spending to qualify
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercent { get; set; } = 0; // Auto discount %
        
        public int FreeDeliveryKm { get; set; } = 0; // Free delivery within X km
        
        public bool PrioritySupport { get; set; } = false;
        
        public int ExtraFreeHours { get; set; } = 0; // Free extra hours on late return
        
        [MaxLength(50)]
        public string Icon { get; set; } = "bi-award"; // Bootstrap icon
        
        [MaxLength(20)]
        public string Color { get; set; } = "#cd7f32"; // Badge color
        
        public int SortOrder { get; set; } = 0;
        
        public ICollection<User>? Users { get; set; }
    }
}
