namespace CarRentalSystem.ViewModels;

public sealed class HomeIndexViewModel
{
    public IReadOnlyList<HomeCarCardViewModel> PremiumCars { get; init; } = [];
    public IReadOnlyList<HomeCarCardViewModel> AvailableCars { get; init; } = [];
}

public sealed class HomeCarCardViewModel
{
    public int CarID { get; init; }
    public string CarName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? BrandName { get; init; }
    public string Transmission { get; init; } = string.Empty;
    public int Seats { get; init; }
    public string? LocationName { get; init; }
    public decimal PricePerDay { get; init; }
    public double AverageRating { get; init; } = 5.0;
}
