using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Wallet
    {
        [Key]
        public int WalletID { get; set; }
        
        public int UserID { get; set; }
        [ForeignKey("UserID")]
        public User? User { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0m;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal LockedBalance { get; set; } = 0m; // Holds deposits
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
