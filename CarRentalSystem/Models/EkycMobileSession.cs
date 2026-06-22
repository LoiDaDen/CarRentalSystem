using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class EkycMobileSession
    {
        [Key]
        public int EkycMobileSessionID { get; set; }

        public int UserID { get; set; }

        [ForeignKey(nameof(UserID))]
        public User? User { get; set; }

        [Required]
        [MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(24)]
        public string Status { get; set; } = EkycMobileSessionStatuses.Waiting;

        [MaxLength(500)]
        public string? ReturnUrl { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public DateTime? LastPolledAtUtc { get; set; }

        public int AttemptCount { get; set; }

        [MaxLength(1000)]
        public string? FailureReason { get; set; }

        public string? WarningsJson { get; set; }

        [MaxLength(500)]
        public string? MobileUserAgent { get; set; }
    }

    public static class EkycMobileSessionStatuses
    {
        public const string Waiting = "Waiting";
        public const string Processing = "Processing";
        public const string Verified = "Verified";
        public const string PendingReview = "PendingReview";
        public const string Rejected = "Rejected";
        public const string Expired = "Expired";
        public const string Failed = "Failed";

        public static bool IsTerminal(string? status) =>
            status is Verified or PendingReview or Rejected or Expired;
    }
}
