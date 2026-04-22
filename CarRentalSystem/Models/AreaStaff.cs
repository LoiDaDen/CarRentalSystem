// CarRentalSystem/Models/AreaStaff.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class AreaStaff
    {
        [Key]
        public int ID { get; set; }

        public int AreaID { get; set; }
        [ForeignKey("AreaID")]
        public Area? Area { get; set; }

        public int UserID { get; set; }
        [ForeignKey("UserID")]
        public User? User { get; set; }

        [Required, MaxLength(20)]
        public required string RoleInArea { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public int? AssignedBy { get; set; }
        [ForeignKey("AssignedBy")]
        public User? AssignedByUser { get; set; }

        // Phase 10: Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
