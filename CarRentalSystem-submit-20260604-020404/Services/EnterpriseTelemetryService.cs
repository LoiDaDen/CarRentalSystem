using System.Diagnostics;

namespace CarRentalSystem.Services
{
    public interface IEnterpriseTelemetryService
    {
        Activity? StartActivity(string name, params KeyValuePair<string, object?>[] tags);
        bool ContainsSensitiveTelemetryKey(string key);
    }

    public class EnterpriseTelemetryService : IEnterpriseTelemetryService
    {
        public const string ActivitySourceName = "CarRentalSystem.Enterprise";
        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        private static readonly string[] SensitiveKeyFragments =
        {
            "email", "phone", "citizen", "license", "password", "token", "secret", "payload", "file", "path", "address"
        };

        public Activity? StartActivity(string name, params KeyValuePair<string, object?>[] tags)
        {
            var activity = ActivitySource.StartActivity(name);
            if (activity == null)
                return null;

            foreach (var tag in tags)
            {
                if (ContainsSensitiveTelemetryKey(tag.Key))
                    continue;

                activity.SetTag(tag.Key, tag.Value);
            }

            return activity;
        }

        public bool ContainsSensitiveTelemetryKey(string key)
        {
            return SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}
