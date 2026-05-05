using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading;

namespace CarRentalSystem.Middleware
{
    public class AccountLockMiddleware
    {
        private readonly RequestDelegate _next;

        public AccountLockMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    var user = await dbContext.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UserID == userId);

                    if (user != null)
                    {
                        if (user.IsLocked)
                        {
                            // Sign out immediately
                            await context.SignOutAsync("Cookies");
                            context.Session.Clear();

                            var returnUrl = context.Request.Path;
                            context.Response.Redirect($"/Auth/Login?locked=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
                            return;
                        }

                        if (user.IsBlacklisted)
                        {
                            await context.SignOutAsync("Cookies");
                            context.Session.Clear();

                            var returnUrl = context.Request.Path;
                            context.Response.Redirect($"/Auth/Login?blacklisted=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
                            return;
                        }
                    }
                }
            }

            await _next(context);
        }
    }

    public static class AccountLockMiddlewareExtensions
    {
        public static IApplicationBuilder UseAccountLockChecker(this IApplicationBuilder app)
        {
            return app.UseMiddleware<AccountLockMiddleware>();
        }
    }
}
