using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class DigitalContract
    {
        [Key]
        public int DigitalContractID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ContractNo { get; set; }

        public int? BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

        public int? TransactionID { get; set; }
        [ForeignKey("TransactionID")]
        public BookingTransaction? BookingTransaction { get; set; }

        // Party B: Customer Info (Snapshot at the time of signing)
        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; }

        [Required]
        [MaxLength(20)]
        public string CustomerIdentityCard { get; set; }

        [Required]
        [MaxLength(20)]
        public string CustomerPhone { get; set; }

        [Required]
        [MaxLength(255)]
        public string CustomerAddress { get; set; }

        [MaxLength(250)]
        public string? CustomerCompanyName { get; set; }

        [MaxLength(100)]
        public string? CustomerDepartmentCode { get; set; }

        [MaxLength(100)]
        public string? CustomerBankAccount { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        // Car Info (Snapshot)
        [Required]
        [MaxLength(1000)]
        public string CarLicensePlate { get; set; }

        [Required]
        [MaxLength(1000)]
        public string CarModel { get; set; }

        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }

        public decimal RentalPricePerDay { get; set; }
        public decimal TotalContractValue { get; set; }
        public decimal DepositAmount { get; set; }

        [Required]
        public string TermsAndConditions { get; set; }

        public bool IsSignedByCustomer { get; set; }
        public DateTime? CustomerSignDate { get; set; }

        public bool IsSignedByOwner { get; set; }
        public DateTime? OwnerSignDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
