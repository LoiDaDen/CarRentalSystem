using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class AddOn
    {
        [Key]
        public int AddOnID { get; set; }
        [Required, StringLength(100)]
        public string Name { get; set; }
        public string? Description { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerDay { get; set; }
        
        public string? Icon { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
