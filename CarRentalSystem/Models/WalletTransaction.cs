using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class WalletTransaction
    {
        [Key]
        public int TransactionID { get; set; }
        
        public int WalletID { get; set; }
        [ForeignKey("WalletID")]
        public Wallet? Wallet { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        [StringLength(50)]
        public string Type { get; set; } = "TopUp"; // TopUp, Payment, Refund, Lock, Unlock, Payout
        
        [StringLength(255)]
        public string Description { get; set; } = "";
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
