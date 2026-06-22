using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class LostFoundItem
    {
        [Key]
        public int LostFoundItemID { get; set; }

        public int BookingID { get; set; }
        public Booking? Booking { get; set; }

        public int? HandoverID { get; set; }
        public HandoverRecord? HandoverRecord { get; set; }

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? LocationDescription { get; set; }

        [MaxLength(255)]
        public string? PhotoUrl { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = LostFoundStatusConstants.Found;

        public int? ReportedByUserID { get; set; }
        public User? ReportedByUser { get; set; }

        public int? ResolvedByUserID { get; set; }
        public User? ResolvedByUser { get; set; }

        public DateTime FoundAt { get; set; } = DateTime.UtcNow;
        public DateTime? CustomerNotifiedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        [MaxLength(500)]
        public string? ResolutionNote { get; set; }

        public bool IsDeleted { get; set; }
    }

    public static class LostFoundStatusConstants
    {
        public const string Found = "Found";
        public const string CustomerNotified = "CustomerNotified";
        public const string Returned = "Returned";
        public const string Disposed = "Disposed";

        public static readonly string[] All =
        {
            Found,
            CustomerNotified,
            Returned,
            Disposed
        };
    }
}
