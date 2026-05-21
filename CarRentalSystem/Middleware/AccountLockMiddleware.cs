using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, IMemoryCache cache)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                if (context.Request.Path.StartsWithSegments("/Auth/Login", StringComparison.OrdinalIgnoreCase))
                {
                    await _next(context);
                    return;
                }

                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    var state = await cache.GetOrCreateAsync($"UserLockState:{userId}", async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                        return await dbContext.Users
                            .AsNoTracking()
                            .Where(u => u.UserID == userId)
                            .Select(u => new AccountLockState(u.IsLocked, u.IsBlacklisted))
                            .FirstOrDefaultAsync();
                    });

                    if (state != null)
                    {
                        if (state.IsLocked)
                        {
                            // Sign out immediately
                            await context.SignOutAsync("Cookies");
                            context.Session.Clear();

                            var returnUrl = context.Request.Path;
                            context.Response.Redirect($"/Auth/Login?locked=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
                            return;
                        }

                        if (state.IsBlacklisted)
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

        private sealed record AccountLockState(bool IsLocked, bool IsBlacklisted);
    }

    public static class AccountLockMiddlewareExtensions
    {
        public static IApplicationBuilder UseAccountLockChecker(this IApplicationBuilder app)
        {
            return app.UseMiddleware<AccountLockMiddleware>();
        }
    }
}
