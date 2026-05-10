using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class ContractType
    {
        [Key]
        public int ContractTypeID { get; set; }

        [Required]
        [MaxLength(50)]
        public required string Name { get; set; } // Theo ngày, Theo tháng, Theo năm, Ngày chẵn, Ngày lẻ, Cuối tuần, Trong tuần

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercent { get; set; } = 0;

        [MaxLength(300)]
        public string? Description { get; set; }

        [Required]
        [Range(1, 365)]
        public int MinDurationDays { get; set; } = 1;

        [Required]
        [Range(1, 365)]
        public int MaxDurationDays { get; set; } = 365;

        public bool IsActive { get; set; } = true;

        // After-sales benefits for yearly contracts
        [Range(0, 100)]
        public int FreeMaintenanceCount { get; set; } = 0; // Free maintenance sessions
        public bool FreeInsurance { get; set; } = false;

        [MaxLength(500)]
        public string? AfterSalesDescription { get; set; }
    }
}
