using System.Security.Claims;
using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Models.Tutorial;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CarRentalSystem.Services;

public interface ITutorialService
{
    Task<bool> CanAccessPanelAsync(ClaimsPrincipal user, string panel, CancellationToken cancellationToken = default);
    Task<TutorialPanelResponse?> GetTutorialAsync(ClaimsPrincipal user, string panel, bool manual, CancellationToken cancellationToken = default);
    Task<TutorialQuickGuide?> GetQuickGuideAsync(
        ClaimsPrincipal user,
        string panel,
        string? area,
        string? controller,
        string? action,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TutorialQuickGuide>> GetAllQuickGuidesAsync(ClaimsPrincipal user, string panel, CancellationToken cancellationToken = default);
    Task<bool> UpdateProgressAsync(ClaimsPrincipal user, TutorialProgressUpdateRequest request, CancellationToken cancellationToken = default);
}

public sealed class TutorialService : ITutorialService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] SupportedPanels = { "Admin", "Partner", "Staff", "Manager" };

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IMemoryCache _cache;

    public TutorialService(ApplicationDbContext context, IWebHostEnvironment environment, IMemoryCache cache)
    {
        _context = context;
        _environment = environment;
        _cache = cache;
    }

    public async Task<bool> CanAccessPanelAsync(ClaimsPrincipal user, string panel, CancellationToken cancellationToken = default)
    {
        var normalizedPanel = NormalizePanel(panel);
        var userId = GetUserId(user);
        if (normalizedPanel == null || userId <= 0 || user.Identity?.IsAuthenticated != true)
            return false;

        if (!string.Equals(normalizedPanel, "Partner", StringComparison.Ordinal))
        {
            var role = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role");
            return string.Equals(role, normalizedPanel, StringComparison.OrdinalIgnoreCase);
        }

        return await _context.Users
            .AsNoTracking()
            .AnyAsync(item => item.UserID == userId && item.IsPartnerApproved, cancellationToken);
    }

    public async Task<TutorialPanelResponse?> GetTutorialAsync(
        ClaimsPrincipal user,
        string panel,
        bool manual,
        CancellationToken cancellationToken = default)
    {
        var normalizedPanel = NormalizePanel(panel);
        if (normalizedPanel == null || !await CanAccessPanelAsync(user, normalizedPanel, cancellationToken))
            return null;

        var catalog = await LoadCatalogAsync(normalizedPanel, cancellationToken);
        var userId = GetUserId(user);
        var progress = await _context.UserTutorialProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.UserID == userId &&
                        item.PanelName == normalizedPanel &&
                        item.TutorialKey == catalog.TutorialKey,
                cancellationToken);

        if (!manual && progress is { IsCompleted: true } or { DoNotShowAgain: true })
            return null;

        return new TutorialPanelResponse
        {
            Panel = catalog.Panel,
            RoleName = catalog.RoleName,
            TutorialKey = catalog.TutorialKey,
            Title = catalog.Title,
            Description = catalog.Description,
            Icon = catalog.Icon,
            IsCompleted = progress?.IsCompleted == true,
            DoNotShowAgain = progress?.DoNotShowAgain == true,
            Steps = catalog.Steps
        };
    }

    public async Task<TutorialQuickGuide?> GetQuickGuideAsync(
        ClaimsPrincipal user,
        string panel,
        string? area,
        string? controller,
        string? action,
        CancellationToken cancellationToken = default)
    {
        var normalizedPanel = NormalizePanel(panel);
        if (normalizedPanel == null || !await CanAccessPanelAsync(user, normalizedPanel, cancellationToken))
            return null;

        var catalog = await LoadCatalogAsync(normalizedPanel, cancellationToken);
        var exact = catalog.QuickGuides.FirstOrDefault(item =>
            Matches(item.Area, area) &&
            Matches(item.Controller, controller) &&
            Matches(item.Action, action));
        if (exact != null)
            return exact;

        var controllerFallback = catalog.QuickGuides.FirstOrDefault(item =>
            Matches(item.Area, area) &&
            Matches(item.Controller, controller) &&
            string.IsNullOrWhiteSpace(item.Action));
        if (controllerFallback != null)
            return controllerFallback;

        return catalog.QuickGuides.FirstOrDefault(item => item.Controller == "*");
    }

    public async Task<IReadOnlyList<TutorialQuickGuide>> GetAllQuickGuidesAsync(
        ClaimsPrincipal user,
        string panel,
        CancellationToken cancellationToken = default)
    {
        var normalizedPanel = NormalizePanel(panel);
        if (normalizedPanel == null || !await CanAccessPanelAsync(user, normalizedPanel, cancellationToken))
            return Array.Empty<TutorialQuickGuide>();

        return (await LoadCatalogAsync(normalizedPanel, cancellationToken)).QuickGuides;
    }

    public async Task<bool> UpdateProgressAsync(
        ClaimsPrincipal user,
        TutorialProgressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedPanel = NormalizePanel(request.Panel);
        if (normalizedPanel == null ||
            !await CanAccessPanelAsync(user, normalizedPanel, cancellationToken) ||
            !IsSupportedAction(request.Action))
            return false;

        var catalog = await LoadCatalogAsync(normalizedPanel, cancellationToken);
        if (!string.Equals(catalog.TutorialKey, request.TutorialKey, StringComparison.Ordinal))
            return false;

        var userId = GetUserId(user);
        var progress = await _context.UserTutorialProgresses.FirstOrDefaultAsync(
            item => item.UserID == userId &&
                    item.PanelName == normalizedPanel &&
                    item.TutorialKey == catalog.TutorialKey,
            cancellationToken);

        var now = DateTime.UtcNow;
        if (progress == null)
        {
            progress = new UserTutorialProgress
            {
                UserID = userId,
                RoleName = catalog.RoleName,
                PanelName = normalizedPanel,
                TutorialKey = catalog.TutorialKey,
                CreatedAt = now
            };
            _context.UserTutorialProgresses.Add(progress);
        }

        if (string.Equals(request.Action, TutorialProgressActions.Finish, StringComparison.OrdinalIgnoreCase))
        {
            progress.IsCompleted = true;
            progress.CompletedAt ??= now;
        }
        else
        {
            progress.DoNotShowAgain = true;
        }

        progress.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<TutorialCatalog> LoadCatalogAsync(string panel, CancellationToken cancellationToken)
    {
        var cacheKey = $"tutorial-catalog:{panel}";
        if (_cache.TryGetValue(cacheKey, out TutorialCatalog? cached) && cached != null)
            return cached;

        var filePath = Path.Combine(_environment.ContentRootPath, "Data", "Tutorials", $"{panel}.json");
        await using var stream = File.OpenRead(filePath);
        var catalog = await JsonSerializer.DeserializeAsync<TutorialCatalog>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Tutorial catalog '{panel}' is invalid.");

        ValidateCatalog(catalog, panel);
        _cache.Set(cacheKey, catalog, _environment.IsDevelopment() ? TimeSpan.FromMinutes(1) : TimeSpan.FromHours(12));
        return catalog;
    }

    private static void ValidateCatalog(TutorialCatalog catalog, string expectedPanel)
    {
        if (!string.Equals(catalog.Panel, expectedPanel, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(catalog.TutorialKey) ||
            catalog.Steps.Count == 0 ||
            catalog.Steps.Any(step => string.IsNullOrWhiteSpace(step.Key) ||
                                      string.IsNullOrWhiteSpace(step.Title) ||
                                      string.IsNullOrWhiteSpace(step.Description)))
        {
            throw new InvalidOperationException($"Tutorial catalog '{expectedPanel}' is incomplete.");
        }
    }

    private static bool Matches(string? configured, string? current) =>
        string.IsNullOrWhiteSpace(configured)
            ? string.IsNullOrWhiteSpace(current)
            : string.Equals(configured, current, StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedAction(string? action) =>
        string.Equals(action, TutorialProgressActions.Finish, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(action, TutorialProgressActions.DoNotShowAgain, StringComparison.OrdinalIgnoreCase);

    private static int GetUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;

    private static string? NormalizePanel(string? panel) =>
        SupportedPanels.FirstOrDefault(item => string.Equals(item, panel, StringComparison.OrdinalIgnoreCase));
}
