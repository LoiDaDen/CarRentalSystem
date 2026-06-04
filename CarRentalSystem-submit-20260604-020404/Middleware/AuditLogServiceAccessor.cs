namespace CarRentalSystem.Middleware
{
    /// <summary>
    /// Static holder cho IServiceScopeFactory — tránh tạo DbContext trong middleware.
    /// </summary>
    public static class AuditLogServiceAccessor
    {
        public static Microsoft.Extensions.DependencyInjection.IServiceScopeFactory? ScopeFactory { get; set; }
    }
}
