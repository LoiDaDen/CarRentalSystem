namespace CarRentalSystem.Services;

public static class EmailDeliveryConfiguration
{
    public static bool HasSmtpSettings(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration["SmtpSettings:Host"]) &&
            !string.IsNullOrWhiteSpace(configuration["SmtpSettings:User"]) &&
            !string.IsNullOrWhiteSpace(configuration["SmtpSettings:Pass"]);
    }

    public static bool ShouldUseLocalSink(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        bool isAutomatedTestEnvironment)
    {
        if (isAutomatedTestEnvironment)
            return true;

        var localModeRequested = string.Equals(
            configuration["Email:DeliveryMode"],
            "Local",
            StringComparison.OrdinalIgnoreCase);

        return environment.IsDevelopment() &&
            localModeRequested &&
            !HasSmtpSettings(configuration);
    }
}
