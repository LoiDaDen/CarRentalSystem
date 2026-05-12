using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class BookingHold
    {
        [Key]
        public int BookingHoldID { get; set; }

        public int UserID { get; set; }
        public User? User { get; set; }

        public int CarID { get; set; }
        public Car? Car { get; set; }

        public int? BookingID { get; set; }
        public Booking? Booking { get; set; }

        public int? TransactionID { get; set; }
        public BookingTransaction? BookingTransaction { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime ExpiresAt { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = BookingHoldStatusConstants.Active;

        [Column(TypeName = "nvarchar(max)")]
        public string? QuoteSnapshotJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReleasedAt { get; set; }
        public DateTime? ConvertedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
