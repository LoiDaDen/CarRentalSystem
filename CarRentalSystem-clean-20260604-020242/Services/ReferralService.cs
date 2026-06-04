using System.Security.Cryptography;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IReferralService
    {
        Task<string?> EnsureReferralCodeAsync(int userId);
        Task<User?> GetVerifiedReferrerAsync(string? referralCode);
        Task<decimal> GetFirstRentalDiscountAsync(int userId);
        Task MarkReferralDiscountUsedAsync(int userId);
    }

    public class ReferralService : IReferralService
    {
        public const decimal FirstRentalDiscount = 100000m;
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Digits = "23456789";
        private readonly ApplicationDbContext _context;

        public ReferralService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string?> EnsureReferralCodeAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null || !user.IsVerified) return user?.ReferralCode;

            if (!string.IsNullOrWhiteSpace(user.ReferralCode))
                return user.ReferralCode;

            for (var attempt = 0; attempt < 40; attempt++)
            {
                var code = GenerateReferralCode();
                var exists = await _context.Users.AnyAsync(u => u.ReferralCode == code);
                if (exists) continue;

                user.ReferralCode = code;
                await _context.SaveChangesAsync();
                return code;
            }

            throw new InvalidOperationException("Could not generate a unique referral code.");
        }

        public async Task<User?> GetVerifiedReferrerAsync(string? referralCode)
        {
            if (string.IsNullOrWhiteSpace(referralCode)) return null;
            var normalized = referralCode.Trim().ToUpperInvariant();
            return await _context.Users
                .FirstOrDefaultAsync(u => u.ReferralCode == normalized && u.IsVerified && !u.IsBlacklisted);
        }

        public async Task<decimal> GetFirstRentalDiscountAsync(int userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user?.ReferredByUserID == null || user.ReferralDiscountUsedAt.HasValue)
                return 0m;

            var hasPaidBooking = await _context.Bookings
                .Include(b => b.Payment)
                .AnyAsync(b => b.UserID == userId &&
                    b.Payment != null &&
                    b.Payment.PaymentStatus == PaymentStatusConstants.Paid);

            return hasPaidBooking ? 0m : FirstRentalDiscount;
        }

        public async Task MarkReferralDiscountUsedAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null || user.ReferredByUserID == null || user.ReferralDiscountUsedAt.HasValue)
                return;

            user.ReferralDiscountUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private static string GenerateReferralCode()
        {
            var chars = new List<char>(6);
            for (var i = 0; i < 3; i++)
                chars.Add(Letters[RandomNumberGenerator.GetInt32(Letters.Length)]);
            for (var i = 0; i < 3; i++)
                chars.Add(Digits[RandomNumberGenerator.GetInt32(Digits.Length)]);

            for (var i = chars.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }
    }
}
