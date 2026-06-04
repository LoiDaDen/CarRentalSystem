using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class PhysicalContractType
    {
        [Key]
        public int PhysicalContractTypeID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class PhysicalContractTypeCodes
    {
        public const string SelfDriveRental = "SELF_DRIVE_RENTAL";
        public const string CarOwnerDocument = "CAR_OWNER_DOCUMENT";
        public const string HandoverRecord = "HANDOVER_RECORD";
        public const string DepositAgreement = "DEPOSIT_AGREEMENT";
        public const string Other = "OTHER";
    }
}
