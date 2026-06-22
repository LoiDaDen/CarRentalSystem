namespace CarRentalSystem.ViewModels
{
    public class EkycMobileViewModel
    {
        public string Token { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string StatusText { get; set; } = string.Empty;

        public DateTime? ExpiresAtUtc { get; set; }

        public bool IsAvailable { get; set; }

        public bool IsCompleted { get; set; }

        public bool CanSubmit => IsAvailable && !IsCompleted;

        public string? Message { get; set; }

        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }
}
