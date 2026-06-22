using BCrypt.Net;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Data;

public static class QaAdminSeeder
{
    public static async Task SeedFromEnvironmentAsync(
        ApplicationDbContext db,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Playwright"))
            return;

        var email = Environment.GetEnvironmentVariable("QA_ADMIN_EMAIL")?.Trim().ToLowerInvariant();
        var password = Environment.GetEnvironmentVariable("QA_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("QA_ADMIN_EMAIL and QA_ADMIN_PASSWORD must be configured together.");

        var connectionString = db.Database.GetDbConnection().ConnectionString;
        if (!connectionString.Contains("CarRentalSystem_Local", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.Contains("CarRentalSystem_Playwright", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("QA admin seeding is restricted to Local/Playwright databases.");

        var user = await db.Users.FirstOrDefaultAsync(
            item => item.Email == email,
            cancellationToken);
        if (user == null)
        {
            user = new User
            {
                Name = "QA Admin",
                Email = email,
                Phone = "0900000099",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRoleConstants.Admin,
                IsEmailVerified = true,
                IsVerified = true,
                VerificationStatus = "Verified",
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
        }
        else
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.Role = UserRoleConstants.Admin;
            user.IsEmailVerified = true;
            user.IsVerified = true;
            user.VerificationStatus = "Verified";
            user.IsLocked = false;
            user.LockedAt = null;
            user.IsBlacklisted = false;
            user.BlacklistReason = null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
