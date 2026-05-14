using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRentalSystem.Services
{
    public static class PaymentUrlResolver
    {
        public static string ResolvePublicBaseUrl(HttpContext httpContext)
        {
            var configuration = httpContext.RequestServices.GetService<IConfiguration>();
            var configuredUrl = configuration?["App:PublicBaseUrl"]
                ?? configuration?["Payment:PublicBaseUrl"];

            if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                return configuredUrl!.TrimEnd('/');
            }

            return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}".TrimEnd('/');
        }
    }
}
