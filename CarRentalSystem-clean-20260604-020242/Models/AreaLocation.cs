// CarRentalSystem/Models/AreaLocation.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    [Table("AreaLocations")]
    public class AreaLocation
    {
        public int AreaID { get; set; }
        [ForeignKey("AreaID")]
        public Area? Area { get; set; }

        public int LocationID { get; set; }
        [ForeignKey("LocationID")]
        public Location? Location { get; set; }

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
