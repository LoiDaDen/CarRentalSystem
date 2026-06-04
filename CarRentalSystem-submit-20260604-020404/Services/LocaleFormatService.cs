using System.Globalization;

namespace CarRentalSystem.Services
{
    public interface ILocaleFormatService
    {
        string FormatCurrency(decimal value, string? cultureName = null);
        string FormatDateTime(DateTime value, string? cultureName = null);
    }

    public class LocaleFormatService : ILocaleFormatService
    {
        private readonly IConfiguration _configuration;

        public LocaleFormatService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string FormatCurrency(decimal value, string? cultureName = null)
        {
            var culture = BuildCulture(cultureName);
            return string.Format(culture, "{0:C0}", value);
        }

        public string FormatDateTime(DateTime value, string? cultureName = null)
        {
            var culture = BuildCulture(cultureName);
            return value.ToString("g", culture);
        }

        private CultureInfo BuildCulture(string? cultureName)
        {
            var configured = cultureName ?? _configuration["Localization:DefaultCulture"] ?? "vi-VN";
            return CultureInfo.GetCultureInfo(configured);
        }
    }
}
