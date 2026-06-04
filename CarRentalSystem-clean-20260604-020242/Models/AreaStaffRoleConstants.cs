namespace CarRentalSystem.Models
{
    public static class AreaStaffRoleConstants
    {
        public const string Manager = "manager";
        public const string Staff = "staff";
        public const string Viewer = "viewer";

        public static readonly string[] All = { Manager, Staff, Viewer };

        public static string Normalize(string? role)
        {
            return (role ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static bool IsManager(string? role)
        {
            return Normalize(role) == Manager;
        }
    }
}
