using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class PhysicalContract
    {
        [Key]
        public int PhysicalContractID { get; set; }

        public int? BookingID { get; set; }
        public Booking? Booking { get; set; }

        public int? CarID { get; set; }
        public Car? Car { get; set; }

        public int OwnerID { get; set; }
        public User? Owner { get; set; }

        public int? PhysicalContractTypeID { get; set; }
        public PhysicalContractType? PhysicalContractType { get; set; }

        [MaxLength(260)]
        public string? Title { get; set; }

        [Required]
        [MaxLength(500)]
        public string FileUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string FileKind { get; set; } = "Image";

        [MaxLength(50)]
        public string? SuggestedContractTypeCode { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? AiConfidence { get; set; }

        [MaxLength(1000)]
        public string? AiReason { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = "PendingClassification";

        [MaxLength(1000)]
        public string? Note { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }

        public int ContractVersion { get; set; } = 1;

        [MaxLength(64)]
        public string? FileContentHash { get; set; }

        [MaxLength(45)]
        public string? UploadedIpAddress { get; set; }

        [MaxLength(500)]
        public string? UploadedUserAgent { get; set; }

        public bool IsDeleted { get; set; }
    }
}
