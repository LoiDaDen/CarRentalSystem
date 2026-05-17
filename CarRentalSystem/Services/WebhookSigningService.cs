using System.Security.Cryptography;
using System.Text;

namespace CarRentalSystem.Services
{
    public interface IWebhookSigningService
    {
        string Sign(string payload, string secret, long timestampUnixSeconds);
        bool Verify(string payload, string secret, long timestampUnixSeconds, string signature, TimeSpan replayWindow);
    }

    public class WebhookSigningService : IWebhookSigningService
    {
        public string Sign(string payload, string secret, long timestampUnixSeconds)
        {
            var canonical = $"{timestampUnixSeconds}.{payload}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }

        public bool Verify(string payload, string secret, long timestampUnixSeconds, string signature, TimeSpan replayWindow)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - timestampUnixSeconds) > replayWindow.TotalSeconds)
                return false;

            var expected = Sign(payload, secret, timestampUnixSeconds);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
        }
    }
}
