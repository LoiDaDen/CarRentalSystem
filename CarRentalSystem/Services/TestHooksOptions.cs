namespace CarRentalSystem.Services;

public sealed class TestHooksOptions
{
    public bool Enabled { get; set; }
    public string Secret { get; set; } = string.Empty;
    public string[] AllowedDatabases { get; set; } = [];
}
