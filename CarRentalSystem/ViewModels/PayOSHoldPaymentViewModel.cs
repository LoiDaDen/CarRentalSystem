namespace CarRentalSystem.ViewModels
{
    public static class PayOSPaymentFlow
    {
        public const string Hold = "Hold";
        public const string Cart = "Cart";
    }

    public class PayOSHoldPaymentViewModel
    {
        public long OrderCode { get; set; }
        public string PaymentFlow { get; set; } = PayOSPaymentFlow.Hold;
        public string Status { get; set; } = "Pending";
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public string? Bin { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public string? Description { get; set; }
        public DateTime ExpiresAt { get; set; }
        public CheckoutQuoteViewModel Quote { get; set; } = new();
    }
}
