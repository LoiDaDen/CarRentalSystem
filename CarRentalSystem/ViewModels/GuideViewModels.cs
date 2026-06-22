using CarRentalSystem.Models.Tutorial;

namespace CarRentalSystem.ViewModels;

public sealed class GuideViewModel
{
    public string Role { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string RoleIcon { get; init; } = "bi-book";
    public string RoleColor { get; init; } = "#2563eb";
    public string MarkdownContent { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
    public TutorialPanelResponse? Tutorial { get; init; }
    public IReadOnlyList<TutorialQuickGuide> QuickGuides { get; init; } = Array.Empty<TutorialQuickGuide>();
}
