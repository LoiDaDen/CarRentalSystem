using CarRentalSystem.Services;

namespace CarRentalSystem.Areas.Admin.Models;

public sealed record StartupGrowthPlanRow(
    int Month,
    int NewUsers,
    int ApprovedCars,
    int Bookings,
    int CompletedBookings,
    decimal Gmv,
    decimal PlatformCommission);

public sealed class StartupKpiViewModel
{
    public required StartupKpiSnapshot Snapshot { get; init; }
    public required StartupKpiOptions Targets { get; init; }

    public IReadOnlyList<StartupGrowthPlanRow> GrowthPlan { get; init; } =
    [
        new(1, 15, 5, 5, 3, 10_000_000m, 0m),
        new(2, 15, 5, 8, 6, 20_000_000m, 0m),
        new(3, 15, 5, 10, 7, 30_000_000m, 0m),
        new(4, 20, 5, 14, 10, 45_000_000m, 0m),
        new(5, 17, 5, 18, 12, 60_000_000m, 0m),
        new(6, 18, 5, 25, 18, 85_000_000m, 0m)
    ];
}
