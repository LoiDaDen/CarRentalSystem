using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class BookingAddOn
    {
        // Composite Key: BookingID + AddOnID
        public int BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

        public int AddOnID { get; set; }
        [ForeignKey("AddOnID")]
        public AddOn? AddOn { get; set; }

        public int Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
    }
}
