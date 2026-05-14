using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace CarRentalSystem.Middleware
{
    /// <summary>
    /// Rate Limiting Middleware — Chống brute force attacks
    /// Giới hạn số lần request trong khoảng thời gian nhất định.
    /// Áp dụng cho login endpoint và API endpoints.
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        // Track failed login attempts per IP: IP -> (count, windowStart)
        private static readonly ConcurrentDictionary<string, RateLimitEntry> _failedLoginAttempts = new();

        // Track PIN verification attempts per IP
        private static readonly ConcurrentDictionary<string, RateLimitEntry> _pinAttempts = new();

        // Cấu hình: tối đa 5 lần login failed / 15 phút
        private const int LoginMaxAttempts = 5;
        private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(15);

        // Cấu hình: tối đa 60 API calls / phút
        private const int ApiMaxAttempts = 60;
        private static readonly TimeSpan ApiWindow = TimeSpan.FromMinutes(1);

        private const int TrackingMaxAttempts = 30;
        private static readonly TimeSpan TrackingWindow = TimeSpan.FromMinutes(1);

        // Cấu hình: tối đa 5 PIN attempts / 5 phút
        private const int PinMaxAttempts = 5;
        private static readonly TimeSpan PinWindow = TimeSpan.FromMinutes(5);

        // Key để lưu trạng thái login trong HttpContext.Items
        private const string LoginFailedKey = "LoginFailedAttempt";

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            var method = context.Request.Method;

            // Lấy IP từ X-Forwarded-For header (khi deploy sau proxy/load balancer)
            var clientIp = GetClientIpAddress(context);

            // Rate limit login POST requests - kiểm tra TRƯỚC khi xử lý
            if (path.Contains("/auth/login") && method == "POST")
            {
                var key = $"login:{clientIp}";
                if (IsLoginRateLimited(key))
                {
                    _logger.LogWarning("Rate limit exceeded for login from IP: {IP}", clientIp);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = LoginWindow.TotalSeconds.ToString();
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(
                        "<h2>Qua nhieu lan dang nhap that bai</h2>" +
                        $"<p>Vui long thu lai sau {LoginWindow.TotalMinutes} phut.</p>" +
                        "<p><a href='/'>Ve trang chu</a></p>");
                    return;
                }

                // Đánh dấu để theo dõi sau khi xử lý
                context.Items[LoginFailedKey] = true; // Mặc định là failed cho đến khi chứng minh ngược lại
            }

            // Rate limit API endpoints
            if (path.StartsWith("/api/"))
            {
                var key = $"api:{clientIp}";
                if (IsRateLimited(key, ApiMaxAttempts, ApiWindow))
                {
                    _logger.LogWarning("API rate limit exceeded from IP: {IP}, Path: {Path}", clientIp, path);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = ApiWindow.TotalSeconds.ToString();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"Too many requests. Please slow down.\"}");
                    return;
                }
            }

            if (path.Equals("/tracking/receivedata", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                var key = $"tracking:{clientIp}";
                if (IsRateLimited(key, TrackingMaxAttempts, TrackingWindow))
                {
                    _logger.LogWarning("Tracking ingest rate limit exceeded from IP: {IP}", clientIp);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = TrackingWindow.TotalSeconds.ToString();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"Too many tracking requests.\"}");
                    return;
                }
            }

            // =========================================
            // Rate limit PIN verification endpoints
            // =========================================
            if ((path.Contains("/auth/setpiipin") || path.Contains("/auth/verifypiipin")) && method == "POST")
            {
                var key = $"pin:{clientIp}";
                if (IsRateLimited(key, PinMaxAttempts, PinWindow))
                {
                    _logger.LogWarning("PIN rate limit exceeded from IP: {IP}", clientIp);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = PinWindow.TotalSeconds.ToString();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"success\":false,\"message\":\"Qua nhieu yeu cau. Vui long thu lai sau.\"}");
                    return;
                }
            }

            await _next(context);

            // Xử lý sau khi login - chỉ đếm FAILED attempts
            if (path.Contains("/auth/login") && method == "POST")
            {
                var key = $"login:{clientIp}";

                // Kiểm tra nếu là response thành công (redirect về trang chính hoặc dashboard)
                var statusCode = context.Response.StatusCode;
                var location = context.Response.Headers.Location.FirstOrDefault();

                bool isSuccess = statusCode == 302 &&
                                 (string.IsNullOrEmpty(location) ||
                                  location.Contains("/home", StringComparison.OrdinalIgnoreCase) ||
                                  location.Contains("/booking", StringComparison.OrdinalIgnoreCase));

                if (isSuccess)
                {
                    // Login thành công - reset counter cho IP này
                    _failedLoginAttempts.TryRemove(key, out _);
                    _logger.LogInformation("Login successful, reset failed attempts for IP: {IP}", clientIp);
                }
                else if (context.Items.ContainsKey(LoginFailedKey) && (bool)context.Items[LoginFailedKey]!)
                {
                    // Login that bai - tang counter
                    IncrementFailedLoginAttempt(key, clientIp);
                }
            }

            // Track PIN verification attempts - reset on success
            if ((path.Contains("/auth/setpiipin") || path.Contains("/auth/verifypiipin")) && method == "POST")
            {
                var key = $"pin:{clientIp}";
                if (context.Response.StatusCode == 200)
                {
                    _pinAttempts.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Kiểm tra xem IP có bị rate limit cho login không (chỉ dựa trên failed attempts)
        /// </summary>
        private bool IsLoginRateLimited(string key)
        {
            var now = DateTime.UtcNow;

            if (_failedLoginAttempts.TryGetValue(key, out var entry))
            {
                if (now - entry.WindowStart > LoginWindow)
                {
                    // Window đã hết, xóa entry cũ
                    _failedLoginAttempts.TryRemove(key, out _);
                    return false;
                }
                return entry.Count >= LoginMaxAttempts;
            }

            return false;
        }

        /// <summary>
        /// Tăng số lần failed login attempt
        /// </summary>
        private void IncrementFailedLoginAttempt(string key, string clientIp)
        {
            var now = DateTime.UtcNow;

            _failedLoginAttempts.AddOrUpdate(key,
                // New entry
                _ => new RateLimitEntry { Count = 1, WindowStart = now },
                // Update existing
                (_, existing) =>
                {
                    if (now - existing.WindowStart > LoginWindow)
                    {
                        // Reset window nếu đã hết hạn
                        return new RateLimitEntry { Count = 1, WindowStart = now };
                    }
                    existing.Count++;
                    return existing;
                });

            _logger.LogWarning("Failed login attempt #{Count} from IP: {IP}", GetFailedAttemptCount(key), clientIp);

            // Cleanup stale entries định kỳ
            if (_failedLoginAttempts.Count > 5000)
            {
                CleanupStaleEntries(now);
            }
        }

        private int GetFailedAttemptCount(string key)
        {
            if (_failedLoginAttempts.TryGetValue(key, out var entry))
            {
                return entry.Count;
            }
            return 0;
        }

        /// <summary>
        /// Lấy IP thực của client, hỗ trợ X-Forwarded-For header từ proxy
        /// </summary>
        private static string GetClientIpAddress(HttpContext context)
        {
            // Kiểm tra X-Forwarded-For header (nhiều proxy có thể gửi nhiều IPs)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (ips.Length > 0)
                {
                    var clientIp = ips[0].Trim();
                    if (!string.IsNullOrEmpty(clientIp))
                        return clientIp;
                }
            }

            // Kiểm tra X-Real-IP header (nginx)
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp.Trim();
            }

            // Fallback về RemoteIpAddress
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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

            // Cleanup stale entries
            if (_clients.Count > 5000)
            {
                CleanupStaleEntries(now);
            }

            return entry.Count > maxAttempts;
        }

        private void CleanupStaleEntries(DateTime now)
        {
            // Cleanup cho failed login attempts
            var staleLoginKeys = _failedLoginAttempts
                .Where(kvp => now - kvp.Value.WindowStart > LoginWindow * 2)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleLoginKeys)
                _failedLoginAttempts.TryRemove(key, out _);

            // Cleanup cho API rate limit
            var staleApiKeys = _clients
                .Where(kvp => now - kvp.Value.WindowStart > TimeSpan.FromHours(1))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleApiKeys)
                _clients.TryRemove(key, out _);
        }

        // ConcurrentDictionary cho API rate limiting
        private static readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();

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
