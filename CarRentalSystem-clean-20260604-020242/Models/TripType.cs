using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class TripType
    {
        [Key]
        public int TripTypeID { get; set; }

        [Required, MaxLength(40)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public static class TripTypeConstants
    {
        public const string CityTransfer = "CITY_TRANSFER";
        public const string AirportTransfer = "AIRPORT_TRANSFER";
        public const string IntercityOneWay = "INTERCITY_ONE_WAY";
        public const string IntercityRoundTrip = "INTERCITY_ROUND_TRIP";
        public const string HourlyRental = "HOURLY_RENTAL";
        public const string DailyRental = "DAILY_RENTAL";
    }
}
