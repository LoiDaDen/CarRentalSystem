namespace CarRentalSystem.Models
{
    /// <summary>
    /// Centralized constants for booking status names.
    /// Prevents typos and makes refactoring easier.
    /// </summary>
    public static class BookingStatusConstants
    {
        public const string Draft = "Draft";
        public const string Unpaid = "Unpaid";
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string Active = "Active";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled"; // Chuẩn hóa: dùng "Cancelled" (tiếng Anh/Anh)
    }

    public static class CarStatusConstants
    {
        public const string Available = "Available";
        public const string Rented = "Rented";
        public const string Maintenance = "Maintenance";
    }

    public static class DepositStatusConstants
    {
        public const string Pending = "Pending";
        public const string Holding = "Holding";
        public const string Refunded = "Refunded";
        public const string Deducted = "Deducted";
    }
}
