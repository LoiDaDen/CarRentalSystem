using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class PrivacyRequest
    {
        [Key]
        public int PrivacyRequestID { get; set; }

        public int UserID { get; set; }
        public User? User { get; set; }

        [MaxLength(30)]
        public string RequestType { get; set; } = PrivacyRequestTypeConstants.Export;

        [MaxLength(30)]
        public string Status { get; set; } = PrivacyRequestStatusConstants.PendingReview;

        [MaxLength(1000)]
        public string? Reason { get; set; }

        [MaxLength(1000)]
        public string? ReviewReason { get; set; }

        public int? ReviewedByUserID { get; set; }
        public User? ReviewedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? ExportManifestJson { get; set; }
    }

    public static class PrivacyRequestTypeConstants
    {
        public const string Export = "Export";
        public const string Delete = "Delete";
        public const string RetentionReview = "RetentionReview";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Export,
            Delete,
            RetentionReview
        };
    }

    public static class PrivacyRequestStatusConstants
    {
        public const string PendingReview = "PendingReview";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Completed = "Completed";
    }
}
