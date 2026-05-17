namespace CarRentalSystem.Services
{
    public static class ConfigurationGuard
    {
        public static void ValidateProductionConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
        {
            if (!environment.IsProduction())
                return;

            var allowedCorsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            var required = new[]
            {
                ("ConnectionStrings:DefaultConnection", configuration.GetConnectionString("DefaultConnection")),
                ("Cors:AllowedOrigins", string.Join(",", allowedCorsOrigins)),
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

            var unsafeSettings = new List<string>();
            if (allowedCorsOrigins.Length == 0 ||
                allowedCorsOrigins.Any(o => string.IsNullOrWhiteSpace(o) || o.Trim() == "*" || o.Contains("*", StringComparison.Ordinal)))
            {
                unsafeSettings.Add("Cors:AllowedOrigins must be explicit production/staging HTTPS origins and cannot contain wildcards.");
            }

            if (configuration.GetValue("Snapshot:AllowUnsafeDownload", false))
                unsafeSettings.Add("Snapshot:AllowUnsafeDownload must be false in Production.");

            if (configuration.GetValue("Snapshot:AllowUnsafeRestore", false))
                unsafeSettings.Add("Snapshot:AllowUnsafeRestore must be false in Production.");

            if (configuration.GetValue("Snapshot:AllowUnsafeDetails", false))
                unsafeSettings.Add("Snapshot:AllowUnsafeDetails must be false in Production.");

            if (!configuration.GetValue("Security:RequirePrivilegedMfa", false))
                unsafeSettings.Add("Security:RequirePrivilegedMfa must be true in Production.");

            if (!configuration.GetValue("Security:RequireRecaptcha", false))
                unsafeSettings.Add("Security:RequireRecaptcha must be true in Production.");

            if (!configuration.GetValue("Audit:HashChainEnabled", true))
                unsafeSettings.Add("Audit:HashChainEnabled must be true in Production.");

            if (configuration.GetValue("Security:RequirePrivilegedSso", false))
            {
                var hasGoogle = HasConfiguredProvider(configuration["Authentication:Google:ClientId"], configuration["Authentication:Google:ClientSecret"]);
                var hasMicrosoft = HasConfiguredProvider(configuration["Authentication:Microsoft:ClientId"], configuration["Authentication:Microsoft:ClientSecret"]);
                if (!hasGoogle && !hasMicrosoft)
                    unsafeSettings.Add("Security:RequirePrivilegedSso requires configured Google or Microsoft authentication provider.");
            }

            var availabilityTarget = configuration.GetValue("Sre:SloAvailabilityTarget", 0m);
            var rtoHours = configuration.GetValue("Sre:RtoHours", 0m);
            var rpoHours = configuration.GetValue("Sre:RpoHours", 0m);
            if (availabilityTarget < 99.9m)
                unsafeSettings.Add("Sre:SloAvailabilityTarget must be at least 99.9 in Production.");
            if (rtoHours <= 0 || rtoHours > 4)
                unsafeSettings.Add("Sre:RtoHours must be <= 4 in Production.");
            if (rpoHours <= 0 || rpoHours > 1)
                unsafeSettings.Add("Sre:RpoHours must be <= 1 in Production.");

            if (configuration.GetValue("Observability:OpenTelemetry:Enabled", false) &&
                string.IsNullOrWhiteSpace(configuration["Observability:OpenTelemetry:OtlpEndpoint"]))
            {
                unsafeSettings.Add("Observability:OpenTelemetry:OtlpEndpoint is required when OpenTelemetry is enabled in Production.");
            }

            if (configuration.GetValue("ApiDocs:Enabled", false) &&
                !configuration.GetValue("ApiDocs:RequireAuth", false))
            {
                unsafeSettings.Add("ApiDocs:RequireAuth must be true when ApiDocs:Enabled is true in Production.");
            }

            if (configuration.GetValue("PublicApi:Enabled", false))
            {
                if (!configuration.GetValue("PublicApi:RequireApiKey", true))
                    unsafeSettings.Add("PublicApi:RequireApiKey must be true when PublicApi:Enabled is true in Production.");
                if (!configuration.GetValue("PublicApi:RequireRateLimit", true))
                    unsafeSettings.Add("PublicApi:RequireRateLimit must be true when PublicApi:Enabled is true in Production.");
                if (!configuration.GetValue("PublicApi:RequireAudit", true))
                    unsafeSettings.Add("PublicApi:RequireAudit must be true when PublicApi:Enabled is true in Production.");
                if (!configuration.GetValue("PublicApi:RequireWebhookSigning", true))
                    unsafeSettings.Add("PublicApi:RequireWebhookSigning must be true when PublicApi:Enabled is true in Production.");
            }

            if (missing.Count > 0 || unsafeSettings.Count > 0)
            {
                var parts = new List<string>();
                if (missing.Count > 0)
                    parts.Add("missing required secrets/config: " + string.Join(", ", missing));
                if (unsafeSettings.Count > 0)
                    parts.Add("unsafe production settings: " + string.Join(" ", unsafeSettings));

                throw new InvalidOperationException("Production configuration invalid: " + string.Join(" | ", parts));
            }
        }

        private static bool HasConfiguredProvider(string? clientId, string? clientSecret)
        {
            return !string.IsNullOrWhiteSpace(clientId) &&
                   !string.IsNullOrWhiteSpace(clientSecret) &&
                   !clientId.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) &&
                   !clientSecret.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
