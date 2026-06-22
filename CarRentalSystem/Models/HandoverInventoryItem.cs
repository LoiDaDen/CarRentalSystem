using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class HandoverInventoryItem
    {
        [Key]
        public int HandoverInventoryItemID { get; set; }

        public int HandoverID { get; set; }
        public HandoverRecord? HandoverRecord { get; set; }

        public int? CarInventoryItemID { get; set; }
        public CarInventoryItem? CarInventoryItem { get; set; }

        [Required, MaxLength(120)]
        public string ItemName { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int ExpectedQuantity { get; set; }

        [Range(0, int.MaxValue)]
        public int ActualQuantity { get; set; }

        [Range(0, int.MaxValue)]
        public int MissingQuantity { get; set; }

        [Required, MaxLength(20)]
        public string ConditionStatus { get; set; } = HandoverInventoryConditionStatusConstants.Present;

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitReplacementCost { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MissingCharge { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsDeleted { get; set; }
    }
}
