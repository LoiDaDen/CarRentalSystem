using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public record EligibilityResult(bool IsEligible, string? Reason);

    public interface IEligibilityService
    {
        Task<EligibilityResult> CheckRentalEligibilityAsync(int userId);
    }

    public class EligibilityService : IEligibilityService
    {
        private readonly ApplicationDbContext _context;

        public EligibilityService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<EligibilityResult> CheckRentalEligibilityAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null)
                return new EligibilityResult(false, "User not found.");

            if (user.IsLocked || user.IsBlacklisted)
                return new EligibilityResult(false, user.BlacklistReason ?? "Account is locked or blacklisted.");

            if (!user.IsEmailVerified)
                return new EligibilityResult(false, "Email is not verified.");

            if (!user.IsVerified || !string.Equals(user.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase))
                return new EligibilityResult(false, "Identity verification is required before renting.");

            if (string.IsNullOrWhiteSpace(user.CitizenIdHash) || string.IsNullOrWhiteSpace(user.DrivingLicenseHash))
                return new EligibilityResult(false, "Citizen ID and driving license must be verified.");

            return new EligibilityResult(true, null);
        }
    }
}
