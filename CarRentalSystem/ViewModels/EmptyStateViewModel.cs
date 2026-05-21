namespace CarRentalSystem.ViewModels
{
    public class EmptyStateViewModel
    {
        public string Icon { get; set; } = "bi-inbox";
        public string Title { get; set; } = "No data";
        public string Message { get; set; } = "There are no items to show right now.";
        public string? ActionText { get; set; }
        public string? ActionController { get; set; }
        public string? ActionName { get; set; }
        public string? ActionArea { get; set; }
    }
}
