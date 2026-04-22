// CarRentalSystem/Filters/RoleAuthorizeAttribute.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.Filters;

/// <summary>
/// Action filter kiểm tra User.Role có nằm trong danh sách allowed roles không.
/// Trả về 403 Forbidden nếu không đủ quyền.
///
/// Cách dùng:
///   [RoleAuthorize("Manager", "Staff")]
///   public async Task<IActionResult> SomeAction() { ... }
///
/// Luôn yêu cầu user đã đăng nhập (authenticated).
/// Nếu không có role nào được chỉ định → chỉ yêu cầu đăng nhập.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RoleAuthorizeAttribute : ActionFilterAttribute
{
    private readonly string[] _allowedRoles;

    public RoleAuthorizeAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;

        // Bước 1: Kiểm tra authentication
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        // Bước 2: Nếu không chỉ định role → chỉ yêu cầu đăng nhập
        if (_allowedRoles.Length == 0)
        {
            base.OnActionExecuting(context);
            return;
        }

        // Bước 3: Kiểm tra role
        var userRole = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                    ?? user.FindFirst("role")?.Value
                    ?? "";

        if (!_allowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase))
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<RoleAuthorizeAttribute>>();
            logger?.LogWarning(
                "Access denied to {Controller}.{Action} for user {User} with role {Role}. Required: [{RequiredRoles}]",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"],
                user.Identity?.Name,
                userRole,
                string.Join(", ", _allowedRoles));

            context.Result = new ObjectResult(new
            {
                Error = "Forbidden",
                Message = "Bạn không có quyền truy cập tài nguyên này.",
                RequiredRoles = _allowedRoles
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        base.OnActionExecuting(context);
    }
}
