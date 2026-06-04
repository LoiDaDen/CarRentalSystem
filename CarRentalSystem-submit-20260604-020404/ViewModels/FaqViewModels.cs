namespace CarRentalSystem.ViewModels
{
    public class FaqIndexViewModel
    {
        public IReadOnlyList<FaqSectionViewModel> Sections { get; init; } = new List<FaqSectionViewModel>();

        public int TotalQuestions => Sections.Sum(section => section.Items.Count);
    }

    public class FaqSectionViewModel
    {
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public IReadOnlyList<FaqItemViewModel> Items { get; init; } = new List<FaqItemViewModel>();
    }

    public class FaqItemViewModel
    {
        public string Question { get; init; } = string.Empty;
        public string Answer { get; init; } = string.Empty;
        public string Badge { get; init; } = string.Empty;
    }
}
