using CarRentalSystem.Models.Tutorial;

namespace CarRentalSystem.ViewModels;

public sealed class TutorialPopupViewModel
{
    public string Panel { get; init; } = string.Empty;
    public string DetailUrl { get; init; } = string.Empty;
}

public sealed class QuickGuideViewModel
{
    public string Panel { get; init; } = string.Empty;
    public string DetailUrl { get; init; } = string.Empty;
    public required TutorialQuickGuide Guide { get; init; }
}
