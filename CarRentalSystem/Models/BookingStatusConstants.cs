namespace CarRentalSystem.Models
{
    public static class BookingStatusConstants
    {
        public const string Draft = "Draft";
        public const string Unpaid = "Unpaid";
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string Active = "Active";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Rejected = "Rejected";
        public const string Expired = "Expired";

        public static readonly string[] AvailabilityBlocking =
        {
            Unpaid,
            Pending,
            Confirmed,
            Active
        };
    }

    public static class CarStatusConstants
    {
        public const string Available = "Available";
        public const string Rented = "Rented";
        public const string Maintenance = "Maintenance";
        public const string Pending = "Pending";
    }

    public static class DepositStatusConstants
    {
        public const string Pending = "Pending";
        public const string Unpaid = "Unpaid";
        public const string Holding = "Holding";
        public const string PendingRefund = "PendingRefund";
        public const string UnderReview = "UnderReview";
        public const string Refunded = "Refunded";
        public const string Deducted = "Deducted";
        public const string Failed = "Failed";
    }

    public static class PaymentStatusConstants
    {
        public const string Pending = "Pending";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string RefundPending = "RefundPending";
        public const string Refunded = "Refunded";
    }

    public static class BookingHoldStatusConstants
    {
        public const string Active = "Active";
        public const string Converted = "Converted";
        public const string Expired = "Expired";
        public const string Released = "Released";
    }

    public static class PaymentLedgerEntryTypeConstants
    {
        public const string DepositCharge = "DepositCharge";
        public const string Refund = "Refund";
        public const string Deduction = "Deduction";
        public const string Payout = "Payout";
    }

    public static class RefundRequestStatusConstants
    {
        public const string PendingReview = "PendingReview";
        public const string Approved = "Approved";
        public const string Paid = "Paid";
        public const string Rejected = "Rejected";
        public const string Failed = "Failed";
    }
}
