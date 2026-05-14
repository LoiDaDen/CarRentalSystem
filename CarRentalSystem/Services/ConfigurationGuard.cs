namespace CarRentalSystem.Services
{
    public static class ConfigurationGuard
    {
        public static void ValidateProductionConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
        {
            if (!environment.IsProduction())
                return;

            var required = new[]
            {
                ("ConnectionStrings:DefaultConnection", configuration.GetConnectionString("DefaultConnection")),
                ("Cors:AllowedOrigins", string.Join(",", configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())),
                ("Tracking:IngestSecret", configuration["Tracking:IngestSecret"]),
                ("DataProtection:KeysPath", configuration["DataProtection:KeysPath"]),
                ("PayOS:ClientId", configuration["PayOS:ClientId"]),
                ("PayOS:ApiKey", configuration["PayOS:ApiKey"]),
                ("PayOS:ChecksumKey", configuration["PayOS:ChecksumKey"]),
                ("SmtpSettings:Host", configuration["SmtpSettings:Host"]),
                ("SmtpSettings:User", configuration["SmtpSettings:User"]),
                ("SmtpSettings:Pass", configuration["SmtpSettings:Pass"]),
                ("Recaptcha:SecretKey", configuration["Recaptcha:SecretKey"]),
                ("FptAi:ApiKey", configuration["FptAi:ApiKey"])
            };

            var missing = required
                .Where(x => string.IsNullOrWhiteSpace(x.Item2) || x.Item2.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Item1)
                .ToList();

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production configuration is missing required secrets: " + string.Join(", ", missing));
            }
        }
    }
}
