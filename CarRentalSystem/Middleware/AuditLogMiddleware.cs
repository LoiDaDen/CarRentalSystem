using System.Security.Claims;
using System.Text.RegularExpressions;
using CarRentalSystem.Models;

namespace CarRentalSystem.Middleware
{
    /// <summary>
    /// Enqueues lightweight request audit logs. Persistence happens in AuditLogBackgroundWriter.
    /// </summary>
    public class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLogMiddleware> _logger;
        private readonly IAuditLogQueue _queue;
        private readonly bool _trustForwardedHeaders;

        private static readonly Regex StaticFilePattern = new(
            @"\.(css|js|map|ico|png|jpg|jpeg|gif|svg|woff2?|ttf|eot|webp|avif)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] ExcludedPaths =
        {
            "/health", "/hangfire", "/docs/", "/images/", "/css/", "/js/",
            "/_content/", "/favicon", "/signalr", "/swagger", "/api/chat",
            "/SecureFile/GetSecureImage", "/Cart/GetCartCount",
            "/Admin/Booking/GetRecentNotifications"
        };

        public AuditLogMiddleware(
            RequestDelegate next,
            ILogger<AuditLogMiddleware> logger,
            IConfiguration configuration,
            IAuditLogQueue queue)
        {
            _next = next;
            _logger = logger;
            _queue = queue;
            _trustForwardedHeaders = configuration.GetValue<bool>("Security:TrustForwardedHeaders");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            finally
            {
                if (ShouldLog(context))
                    EnqueueAuditLog(context);
            }
        }

        private static bool ShouldLog(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            if (StaticFilePattern.IsMatch(path))
                return false;

            if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return false;

            return !path.Contains("__browserLink", StringComparison.OrdinalIgnoreCase);
        }

        private void EnqueueAuditLog(HttpContext context)
        {
            try
            {
                var path = context.Request.Path.Value ?? "";
                var method = context.Request.Method;
                var userId = GetUserId(context);
                var userName = context.User.FindFirst(ClaimTypes.Name)?.Value
                    ?? context.User.FindFirst(ClaimTypes.Email)?.Value;
                var (action, entityType) = ParseActionFromPath(method, path);
                var userAgent = context.Request.Headers.UserAgent.ToString();

                var log = new AuditLog
                {
                    Action = action,
                    EntityType = entityType,
                    UserId = userId,
                    UserName = userName,
                    Description = BuildDescription(method, path, context.Response.StatusCode),
                    IpAddress = GetClientIp(context),
                    UserAgent = userAgent.Length > 500 ? userAgent[..500] : userAgent,
                    RequestUrl = TruncateRequestUrl($"{method} {path}{context.Request.QueryString}"),
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = context.TraceIdentifier
                };

                if (!_queue.TryEnqueue(log))
                {
                    _logger.LogWarning("Audit log queue is full; dropped {Action} for {Path}.", action, path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue audit log for {Path}.", context.Request.Path.Value);
            }
        }

        private static int? GetUserId(HttpContext context)
        {
            var claim = context.User.FindFirst("UserID") ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        private static string TruncateRequestUrl(string requestUrl)
        {
            return requestUrl.Length <= AuditLog.RequestUrlMaxLength
                ? requestUrl
                : requestUrl[..AuditLog.RequestUrlMaxLength];
        }

        private string GetClientIp(HttpContext context)
        {
            if (_trustForwardedHeaders)
            {
                foreach (var headerName in new[] { "CF-Connecting-IP", "X-Real-IP", "X-Forwarded-For" })
                {
                    var value = context.Request.Headers[headerName].ToString();
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    var ip = value.Split(',')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(ip))
                        return NormalizeIp(ip);
                }
            }

            return NormalizeIp(context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        private static (string action, string entityType) ParseActionFromPath(string method, string path)
        {
            var segs = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var controller = "System";
            var actionName = "";

            if (segs.Length >= 2)
            {
                if (segs[0].Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                    segs[0].Equals("Partner", StringComparison.OrdinalIgnoreCase) ||
                    segs[0].Equals("Driver", StringComparison.OrdinalIgnoreCase))
                {
                    controller = segs[1];
                    actionName = segs.Length >= 3 ? segs[2] : "";
                }
                else
                {
                    controller = segs[0];
                    actionName = segs[1];
                }
            }
            else if (segs.Length == 1)
            {
                controller = segs[0];
            }

            return (ToFriendlyAction(method, controller, actionName), ToFriendlyModule(controller));
        }

        private static string BuildDescription(string method, string path, int statusCode)
        {
            var (action, module) = ParseActionFromPath(method, path);
            var target = TryGetTargetId(path);
            var result = statusCode >= 400 ? "failed" : "succeeded";
            var targetText = target == null ? "" : $" #{target}";
            return $"{action} in {module}{targetText}. Result: {statusCode} ({result}).";
        }

        private static string? TryGetTargetId(string path)
        {
            var last = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            return int.TryParse(last, out _) ? last : null;
        }

        private static string NormalizeIp(string ip)
        {
            if (string.Equals(ip, "::1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ip, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
                return $"Localhost ({ip})";

            return ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase)
                ? ip["::ffff:".Length..]
                : ip;
        }

        private static string ToFriendlyModule(string controller)
        {
            return controller.ToLowerInvariant() switch
            {
                "auth" => "Account",
                "booking" => "Booking",
                "payment" => "Payment",
                "car" => "Car",
                "cart" => "Cart",
                "ekyc" => "eKYC",
                "user" => "User",
                "driver" => "Driver",
                "handover" => "Handover",
                "digitalcontract" => "Digital contract",
                "contract" => "Contract",
                "auditlog" => "Audit log",
                "area" or "areabooking" or "arealocation" => "Area",
                "dashboard" or "admin" => "Admin",
                _ => string.IsNullOrWhiteSpace(controller) ? "System" : controller
            };
        }

        private static string ToFriendlyAction(string method, string controller, string actionName)
        {
            var action = actionName.ToLowerInvariant();

            if (controller.Equals("Payment", StringComparison.OrdinalIgnoreCase) && action == "createpayment")
                return "Open PayOS checkout";
            if (controller.Equals("Payment", StringComparison.OrdinalIgnoreCase) && action == "webhook")
                return "Confirm PayOS webhook";
            if (controller.Equals("Booking", StringComparison.OrdinalIgnoreCase) && action == "book")
                return "Submit booking request";
            if (controller.Equals("Booking", StringComparison.OrdinalIgnoreCase) && action == "deposit")
                return "Open deposit page";
            if (controller.Equals("Booking", StringComparison.OrdinalIgnoreCase) && action == "confirmmanualpayment")
                return "Confirm bank transfer";

            return action switch
            {
                "login" => "Login",
                "logout" => "Logout",
                "edit" or "update" or "updatestatus" => "Update",
                "delete" or "remove" => "Delete",
                "cancel" => "Cancel",
                "confirm" or "confirmdeposit" or "confirmpickup" => "Confirm",
                "details" or "detail" => "View details",
                "checkout" => "View checkout",
                "create" or "add" => "Create",
                _ => method.ToUpperInvariant() switch
                {
                    "GET" => "View",
                    "POST" => "Submit",
                    "PUT" or "PATCH" => "Update",
                    "DELETE" => "Delete",
                    _ => method
                }
            };
        }
    }

    public static class AuditLogMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuditLog(this IApplicationBuilder app)
        {
            return app.UseMiddleware<AuditLogMiddleware>();
        }
    }
}
