namespace CarRentalSystem.Models
{
    /// <summary>
    /// Centralized constants for user roles — 6-Role RBAC System.
    /// </summary>
    public static class UserRoleConstants
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Staff = "Staff";
        public const string Customer = "Customer";
        public const string Viewer = "Viewer";

        /// <summary>
        /// Roles allowed to access Admin Area (dashboard, reports, management)
        /// </summary>
        public const string AdminOrManager = "Admin,Manager";

        /// <summary>
        /// Roles allowed to manage operational tasks (booking, payment, vehicle status)
        /// </summary>
        public const string InternalStaff = "Admin,Manager,Staff";

        /// <summary>
        /// All roles (for [Authorize(Roles = ...)] when any authenticated user is OK)
        /// </summary>
        public const string AllRoles = "Admin,Manager,Staff,Customer,Viewer";
    }
}
