using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace CarRentalSystem.Middleware
{
    /// <summary>
    /// 🚦 Rate Limiting Middleware — Chống brute force attacks
    /// Giới hạn số lần request trong khoảng thời gian nhất định.
    /// Áp dụng cho login endpoint và API endpoints.
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        // Track requests per IP: IP -> (count, windowStart)
        private static readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();

        // Cấu hình: tối đa 5 lần login failed / 15 phút
        private const int LoginMaxAttempts = 5;
        private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(15);

        // Cấu hình: tối đa 60 API calls / phút
        private const int ApiMaxAttempts = 60;
        private static readonly TimeSpan ApiWindow = TimeSpan.FromMinutes(1);

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            var method = context.Request.Method;
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // 🔒 Rate limit login POST requests
            if (path.Contains("/auth/login") && method == "POST")
            {
                var key = $"login:{clientIp}";
                if (IsRateLimited(key, LoginMaxAttempts, LoginWindow))
                {
                    _logger.LogWarning("🚫 Rate limit exceeded for login from IP: {IP}", clientIp);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = LoginWindow.TotalSeconds.ToString();
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(
                        "<h2>⚠️ Quá nhiều lần đăng nhập thất bại</h2>" +
                        $"<p>Vui lòng thử lại sau {LoginWindow.TotalMinutes} phút.</p>" +
                        "<p><a href='/'>← Về trang chủ</a></p>");
                    return;
                }
            }

            // 🔒 Rate limit API endpoints
            if (path.StartsWith("/api/"))
            {
                var key = $"api:{clientIp}";
                if (IsRateLimited(key, ApiMaxAttempts, ApiWindow))
                {
                    _logger.LogWarning("🚫 API rate limit exceeded from IP: {IP}, Path: {Path}", clientIp, path);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = ApiWindow.TotalSeconds.ToString();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"Too many requests. Please slow down.\"}");
                    return;
                }
            }

            await _next(context);

            // Sau khi Login thất bại (redirect lại login page), increment counter
            if (path.Contains("/auth/login") && method == "POST" && context.Response.StatusCode == 200)
            {
                // Nếu response là View (không redirect) → login thất bại
                // Khi login thành công, ASP.NET redirect 302
                // Không cần làm gì thêm vì counter đã increment ở IsRateLimited
            }
        }

        private bool IsRateLimited(string key, int maxAttempts, TimeSpan window)
        {
            var now = DateTime.UtcNow;

            var entry = _clients.AddOrUpdate(key,
                // New entry
                _ => new RateLimitEntry { Count = 1, WindowStart = now },
                // Update existing
                (_, existing) =>
                {
                    if (now - existing.WindowStart > window)
                    {
                        // Reset window
                        return new RateLimitEntry { Count = 1, WindowStart = now };
                    }
                    existing.Count++;
                    return existing;
                });

            // Cleanup stale entries periodically
            if (_clients.Count > 10000)
            {
                CleanupStaleEntries(now);
            }

            return entry.Count > maxAttempts;
        }

        private void CleanupStaleEntries(DateTime now)
        {
            var staleKeys = _clients
                .Where(kvp => now - kvp.Value.WindowStart > TimeSpan.FromHours(1))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleKeys)
                _clients.TryRemove(key, out _);
        }

        private class RateLimitEntry
        {
            public int Count { get; set; }
            public DateTime WindowStart { get; set; }
        }
    }

    /// <summary>
    /// Extension method cho pipeline registration
    /// </summary>
    public static class RateLimitingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimitingMiddleware>();
        }
    }
}
