using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models.Tutorial;

public sealed class UserTutorialProgress
{
    [Key]
    public int UserTutorialProgressID { get; set; }

    public int UserID { get; set; }

    [ForeignKey(nameof(UserID))]
    public User? User { get; set; }

    [Required, MaxLength(30)]
    public string RoleName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string PanelName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string TutorialKey { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool DoNotShowAgain { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
