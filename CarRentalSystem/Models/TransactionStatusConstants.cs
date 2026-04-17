namespace CarRentalSystem.Models
{
    /// <summary>
    /// Centralized constants for transaction statuses.
    /// </summary>
    public static class TransactionStatusConstants
    {
        public const string Draft = "Draft";
        public const string Pending = "Pending";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
        public const string Refunded = "Refunded";
    }
}
