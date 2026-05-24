using System.Text;

namespace CarRentalSystem.Helpers
{
    public static class LicensePlateNormalizer
    {
        public static string Normalize(string? licensePlate)
        {
            if (string.IsNullOrWhiteSpace(licensePlate))
                return string.Empty;

            var builder = new StringBuilder(licensePlate.Length);
            foreach (var ch in licensePlate.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(ch);
            }

            return builder.ToString();
        }
    }
}
