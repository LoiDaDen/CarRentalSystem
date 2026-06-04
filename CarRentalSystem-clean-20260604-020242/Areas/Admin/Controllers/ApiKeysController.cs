using CarRentalSystem.Data;
using CarRentalSystem.Filters;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RoleAuthorize("Admin")]
    public class ApiKeysController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IApiKeyService _apiKeys;

        public ApiKeysController(ApplicationDbContext context, IApiKeyService apiKeys)
        {
            _context = context;
            _apiKeys = apiKeys;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var keys = await _context.ApiKeys.AsNoTracking()
                .Include(k => k.OwnerUser)
                .OrderByDescending(k => k.CreatedAt)
                .Take(200)
                .Select(k => new
                {
                    k.ApiKeyID,
                    k.Name,
                    k.KeyPrefix,
                    k.Scopes,
                    k.Status,
                    k.CreatedAt,
                    k.ExpiresAt,
                    Owner = k.OwnerUser != null ? k.OwnerUser.Email : k.OwnerUserID.ToString()
                })
                .ToListAsync();
            return Json(keys);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int ownerUserId, string name, string scopes, DateTime? expiresAt)
        {
            var result = await _apiKeys.CreateAsync(ownerUserId, name, scopes, expiresAt, GetCurrentUserId());
            return Json(new { result.ApiKey.ApiKeyID, result.ApiKey.KeyPrefix, rawKey = result.RawKey });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(int id)
        {
            var revoked = await _apiKeys.RevokeAsync(id, GetCurrentUserId());
            return Json(new { success = revoked });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("UserID");
            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }
    }
}
