using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class CustomerRiskAssessment
    {
        [Key]
        public int CustomerRiskAssessmentID { get; set; }

        public int UserID { get; set; }
        public User? User { get; set; }

        public int Score { get; set; }

        [MaxLength(30)]
        public string RiskLevel { get; set; } = CustomerRiskLevels.Low;

        [MaxLength(80)]
        public string ModelVersion { get; set; } = CustomerRiskModelVersions.RuleBasedV1;

        [MaxLength(2000)]
        public string ReasonCodesJson { get; set; } = "[]";

        [MaxLength(4000)]
        public string FeatureSnapshotJson { get; set; } = "{}";

        public DateTime AssessedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public static class CustomerRiskLevels
    {
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
        public const string Critical = "Critical";
    }

    public static class CustomerRiskModelVersions
    {
        public const string RuleBasedV1 = "rule-based-v1";
    }
}
