using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class VehicleAreaPresence
    {
        [Key]
        public int VehicleAreaPresenceID { get; set; }

        public int CarID { get; set; }
        public Car? Car { get; set; }

        public int? AreaID { get; set; }
        public Area? Area { get; set; }

        public int? LocationID { get; set; }
        public Location? Location { get; set; }

        public bool IsInside { get; set; }

        [Required, MaxLength(20)]
        public string LastDirection { get; set; } = CameraGateDirectionConstants.Unknown;

        public DateTime LastEventAt { get; set; }

        public int LastCameraGateEventID { get; set; }
        public CameraGateEvent? LastCameraGateEvent { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
    }
}
