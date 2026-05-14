using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public enum EligibilityFailureCode
    {
        None,
        UserNotFound,
        LockedOrBlacklisted,
        EmailNotVerified,
        EkycNotVerified,
        MissingIdentityDocuments
    }

    public record EligibilityResult(
        bool IsEligible,
        string? Reason,
        EligibilityFailureCode FailureCode = EligibilityFailureCode.None);

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
                return new EligibilityResult(false, "Không tìm thấy tài khoản.", EligibilityFailureCode.UserNotFound);

            if (user.IsLocked || user.IsBlacklisted)
                return new EligibilityResult(false, user.BlacklistReason ?? "Tài khoản đang bị khóa hoặc nằm trong danh sách hạn chế.", EligibilityFailureCode.LockedOrBlacklisted);

            if (!user.IsEmailVerified)
                return new EligibilityResult(false, "Email chưa được xác minh.", EligibilityFailureCode.EmailNotVerified);

            if (!user.IsVerified || !string.Equals(user.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase))
                return new EligibilityResult(false, "Cần xác thực eKYC trước khi thuê xe.", EligibilityFailureCode.EkycNotVerified);

            if (string.IsNullOrWhiteSpace(user.CitizenIdHash) || string.IsNullOrWhiteSpace(user.DrivingLicenseHash))
                return new EligibilityResult(false, "Vui lòng xác thực CCCD và giấy phép lái xe trước khi thuê xe.", EligibilityFailureCode.MissingIdentityDocuments);

            return new EligibilityResult(true, null);
        }
    }
}
