namespace CarRentalSystem.Models.Tutorial;

public sealed class TutorialCatalog
{
    public string Panel { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string TutorialKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-compass";
    public IReadOnlyList<TutorialStep> Steps { get; set; } = Array.Empty<TutorialStep>();
    public IReadOnlyList<TutorialQuickGuide> QuickGuides { get; set; } = Array.Empty<TutorialQuickGuide>();
}

public sealed class TutorialStep
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-lightbulb";
    public string? Route { get; set; }
    public string? Target { get; set; }
}

public sealed class TutorialQuickGuide
{
    public string Area { get; set; } = string.Empty;
    public string Controller { get; set; } = string.Empty;
    public string? Action { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> MainActions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Notes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<TutorialFaq> Faqs { get; set; } = Array.Empty<TutorialFaq>();
}

public sealed class TutorialFaq
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public sealed class TutorialPanelResponse
{
    public string Panel { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string TutorialKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-compass";
    public bool IsCompleted { get; set; }
    public bool DoNotShowAgain { get; set; }
    public IReadOnlyList<TutorialStep> Steps { get; set; } = Array.Empty<TutorialStep>();
}

public sealed class TutorialProgressUpdateRequest
{
    public string Panel { get; set; } = string.Empty;
    public string TutorialKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public static class TutorialProgressActions
{
    public const string Finish = "Finish";
    public const string DoNotShowAgain = "DoNotShowAgain";
}
