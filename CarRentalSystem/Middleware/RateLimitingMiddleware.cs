using System.Collections.Concurrent;

namespace CarRentalSystem.Middleware
{
    public class RateLimitingMiddleware
    {
        private const int LoginMaxAttempts = 5;
        private const int ApiMaxAttempts = 60;
        private const int ChatMaxAttempts = 20;
        private const int TrackingMaxAttempts = 30;
        private const int PinMaxAttempts = 5;
        private const string LoginFailedKey = "LoginFailedAttempt";

        private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ApiWindow = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan ChatWindow = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan TrackingWindow = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan PinWindow = TimeSpan.FromMinutes(5);

        private static readonly ConcurrentDictionary<string, RateLimitEntry> FailedLoginAttempts = new();
        private static readonly ConcurrentDictionary<string, RateLimitEntry> PinAttempts = new();
        private static readonly ConcurrentDictionary<string, RateLimitEntry> Clients = new();

        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            var method = context.Request.Method;
            var clientIp = GetTrustedClientIpAddress(context);

            if (path.Contains("/auth/login") && method == HttpMethods.Post)
            {
                var key = $"login:{clientIp}";
                if (IsLoginRateLimited(key))
                {
                    _logger.LogWarning("Rate limit exceeded for login from IP: {IP}", clientIp);
                    await RateLimitResponseWriter.WriteHtmlAsync(
                        context,
                        "Qua nhieu lan dang nhap",
                        "Tai khoan hoac dia chi IP da vuot gioi han dang nhap that bai.",
                        LoginWindow);
                    return;
                }

                context.Items[LoginFailedKey] = true;
            }

            if (path.Equals("/api/chat", StringComparison.OrdinalIgnoreCase) && method == HttpMethods.Post)
            {
                var key = $"chat:{clientIp}";
                if (IsRateLimited(key, ChatMaxAttempts, ChatWindow))
                {
                    _logger.LogWarning("Chat rate limit exceeded from IP: {IP}", clientIp);
                    await RateLimitResponseWriter.WriteJsonAsync(
                        context,
                        new { error = "Too many chat requests. Please wait a moment." },
                        ChatWindow);
                    return;
                }
            }

            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                var key = $"api:{clientIp}";
                if (IsRateLimited(key, ApiMaxAttempts, ApiWindow))
                {
                    _logger.LogWarning("API rate limit exceeded from IP: {IP}, Path: {Path}", clientIp, path);
                    await RateLimitResponseWriter.WriteJsonAsync(
                        context,
                        new { error = "Too many requests. Please slow down." },
                        ApiWindow);
                    return;
                }
            }

            if (path.Equals("/tracking/receivedata", StringComparison.OrdinalIgnoreCase) && method == HttpMethods.Post)
            {
                var key = $"tracking:{clientIp}";
                if (IsRateLimited(key, TrackingMaxAttempts, TrackingWindow))
                {
                    _logger.LogWarning("Tracking ingest rate limit exceeded from IP: {IP}", clientIp);
                    await RateLimitResponseWriter.WriteJsonAsync(
                        context,
                        new { error = "Too many tracking requests." },
                        TrackingWindow);
                    return;
                }
            }

            if ((path.Contains("/auth/setpiipin") || path.Contains("/auth/verifypiipin")) && method == HttpMethods.Post)
            {
                var key = $"pin:{clientIp}";
                if (IsRateLimited(key, PinMaxAttempts, PinWindow, PinAttempts))
                {
                    _logger.LogWarning("PIN rate limit exceeded from IP: {IP}", clientIp);
                    await RateLimitResponseWriter.WriteJsonAsync(
                        context,
                        new { success = false, message = "Qua nhieu yeu cau. Vui long thu lai sau." },
                        PinWindow);
                    return;
                }
            }

            await _next(context);

            TrackLoginResult(context, path, method, clientIp);
            TrackPinResult(context, path, method, clientIp);
        }

        private static string GetTrustedClientIpAddress(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private bool IsLoginRateLimited(string key)
        {
            var now = DateTime.UtcNow;

            if (!FailedLoginAttempts.TryGetValue(key, out var entry))
                return false;

            if (now - entry.WindowStart > LoginWindow)
            {
                FailedLoginAttempts.TryRemove(key, out _);
                return false;
            }

            return entry.Count >= LoginMaxAttempts;
        }

        private bool IsRateLimited(
            string key,
            int maxAttempts,
            TimeSpan window,
            ConcurrentDictionary<string, RateLimitEntry>? bucket = null)
        {
            var now = DateTime.UtcNow;
            var clients = bucket ?? Clients;

            var entry = clients.AddOrUpdate(
                key,
                _ => new RateLimitEntry { Count = 1, WindowStart = now },
                (_, existing) =>
                {
                    if (now - existing.WindowStart > window)
                        return new RateLimitEntry { Count = 1, WindowStart = now };

                    existing.Count++;
                    return existing;
                });

            if (clients.Count > 5000)
                CleanupStaleEntries(now);

            return entry.Count > maxAttempts;
        }

        private void TrackLoginResult(HttpContext context, string path, string method, string clientIp)
        {
            if (!path.Contains("/auth/login") || method != HttpMethods.Post)
                return;

            var key = $"login:{clientIp}";
            var statusCode = context.Response.StatusCode;
            var location = context.Response.Headers.Location.FirstOrDefault();
            var isSuccess = statusCode == StatusCodes.Status302Found &&
                (string.IsNullOrEmpty(location) ||
                 !location.Contains("/auth/login", StringComparison.OrdinalIgnoreCase));

            if (isSuccess)
            {
                FailedLoginAttempts.TryRemove(key, out _);
                _logger.LogInformation("Login successful, reset failed attempts for IP: {IP}", clientIp);
                return;
            }

            if (context.Items.TryGetValue(LoginFailedKey, out var value) && value is true)
                IncrementFailedLoginAttempt(key, clientIp);
        }

        private static void TrackPinResult(HttpContext context, string path, string method, string clientIp)
        {
            if ((path.Contains("/auth/setpiipin") || path.Contains("/auth/verifypiipin")) &&
                method == HttpMethods.Post &&
                context.Response.StatusCode == StatusCodes.Status200OK)
            {
                PinAttempts.TryRemove($"pin:{clientIp}", out _);
            }
        }

        private void IncrementFailedLoginAttempt(string key, string clientIp)
        {
            var now = DateTime.UtcNow;

            FailedLoginAttempts.AddOrUpdate(
                key,
                _ => new RateLimitEntry { Count = 1, WindowStart = now },
                (_, existing) =>
                {
                    if (now - existing.WindowStart > LoginWindow)
                        return new RateLimitEntry { Count = 1, WindowStart = now };

                    existing.Count++;
                    return existing;
                });

            _logger.LogWarning("Failed login attempt #{Count} from IP: {IP}", GetFailedAttemptCount(key), clientIp);

            if (FailedLoginAttempts.Count > 5000)
                CleanupStaleEntries(now);
        }

        private static int GetFailedAttemptCount(string key)
        {
            return FailedLoginAttempts.TryGetValue(key, out var entry) ? entry.Count : 0;
        }

        private static void CleanupStaleEntries(DateTime now)
        {
            foreach (var key in FailedLoginAttempts
                .Where(kvp => now - kvp.Value.WindowStart > LoginWindow * 2)
                .Select(kvp => kvp.Key)
                .ToList())
            {
                FailedLoginAttempts.TryRemove(key, out _);
            }

            foreach (var key in Clients
                .Where(kvp => now - kvp.Value.WindowStart > TimeSpan.FromHours(1))
                .Select(kvp => kvp.Key)
                .ToList())
            {
                Clients.TryRemove(key, out _);
            }

            foreach (var key in PinAttempts
                .Where(kvp => now - kvp.Value.WindowStart > PinWindow * 2)
                .Select(kvp => kvp.Key)
                .ToList())
            {
                PinAttempts.TryRemove(key, out _);
            }
        }

        private sealed class RateLimitEntry
        {
            public int Count { get; set; }
            public DateTime WindowStart { get; set; }
        }
    }

    public static class RateLimitingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimitingMiddleware>();
        }
    }
}
