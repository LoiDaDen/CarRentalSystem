using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class CarImage
    {
        [Key]
        public int ImageID { get; set; }
        public int CarID { get; set; }
        [Required, MaxLength(500)]
        public required string ImageUrl { get; set; }

        [ForeignKey("CarID")]
        public Car? Car { get; set; }
    }
}
