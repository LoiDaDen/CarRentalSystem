using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class HandoverRecord
    {
        [Key]
        public int HandoverID { get; set; }
        
        [Required]
        public int BookingID { get; set; }
        
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

        [Required, MaxLength(20)]
        public string Type { get; set; } = "CheckIn"; // "CheckIn" or "CheckOut"

        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        public int Odo { get; set; }

        [Required, MaxLength(10)]
        public string FuelLevel { get; set; } = "F"; // E, 1/4, 1/2, 3/4, F

        [MaxLength(255)]
        public string? FrontImgUrl { get; set; }
        
        [MaxLength(255)]
        public string? BackImgUrl { get; set; }
        
        [MaxLength(255)]
        public string? LeftImgUrl { get; set; }
        
        [MaxLength(255)]
        public string? RightImgUrl { get; set; }
        
        [MaxLength(255)]
        public string? InsideImgUrl { get; set; }

        [MaxLength(255)]
        public string? SignatureUrl { get; set; }

        public double? GeoLatitude { get; set; }

        public double? GeoLongitude { get; set; }

        [MaxLength(64)]
        public string? EvidenceHash { get; set; }

        [MaxLength(64)]
        public string? CustomerSignatureHash { get; set; }

        public int? RecordedByUserId { get; set; }
        [ForeignKey("RecordedByUserId")]
        public User? RecordedByUser { get; set; }

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
