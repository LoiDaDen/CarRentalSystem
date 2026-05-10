using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CarRentalSystem.Services
{
    public interface IAuthService
    {
        Task<User> AuthenticateAsync(string email, string password);
        Task<User> RegisterAsync(User user);
        Task<User> GetUserByEmailAsync(string email);
        Task<User> GetUserByIdAsync(int id);
        Task<User> UpdateUserAsync(User user);
        Task<bool> IsCitizenIdTakenAsync(string citizenId, int excludeUserId);
        Task<bool> IsDrivingLicenseTakenAsync(string licenseNumber, int excludeUserId);
        Task<bool> IsPhoneTakenAsync(string phone, int excludeUserId);
    }

    public class AuthService : IAuthService
    {
        private readonly IRepository<User> _userRepository;
        private readonly IPiiEncryptionService _piiEncryption;

        public AuthService(IRepository<User> userRepository, IPiiEncryptionService piiEncryption)
        {
            _userRepository = userRepository;
            _piiEncryption = piiEncryption;
        }

        private static string ComputeHash(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public async Task<bool> IsCitizenIdTakenAsync(string citizenId, int excludeUserId)
        {
            if (string.IsNullOrEmpty(citizenId)) return false;
            var hash = ComputeHash(citizenId);

            // Tối ưu: Filter ở database level thay vì load tất cả users vào memory
            var exists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.CitizenIdHash == hash)
                .AnyAsync();

            if (exists) return true;

            // Kiểm tra legacy accounts (chưa có hash) - giới hạn chỉ khi cần
            var legacyExists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.CitizenIdHash == null && u.CitizenId != null)
                .AnyAsync();

            return legacyExists;
        }

        public async Task<bool> IsDrivingLicenseTakenAsync(string licenseNumber, int excludeUserId)
        {
            if (string.IsNullOrEmpty(licenseNumber)) return false;
            var hash = ComputeHash(licenseNumber);

            // Tối ưu: Filter ở database level
            var exists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.DrivingLicenseHash == hash)
                .AnyAsync();

            if (exists) return true;

            // Kiểm tra legacy accounts
            var legacyExists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.DrivingLicenseHash == null && u.DrivingLicenseNumber != null)
                .AnyAsync();

            return legacyExists;
        }

        public async Task<bool> IsPhoneTakenAsync(string phone, int excludeUserId)
        {
            if (string.IsNullOrEmpty(phone)) return false;
            var hash = ComputeHash(phone);

            // Tối ưu: Filter ở database level
            var exists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.PhoneHash == hash)
                .AnyAsync();

            if (exists) return true;

            // Kiểm tra legacy accounts (Phone lưu plaintext)
            var legacyExists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.PhoneHash == null && u.Phone != null && u.Phone.Trim() == phone.Trim())
                .AnyAsync();

            return legacyExists;
        }

        public async Task<User> AuthenticateAsync(string email, string password)
        {
            // Chỉ cho phép login bằng Email (không cho phép login bằng Name)
            var user = await _userRepository.Query()
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return user;
            }
            return null;
        }

        public async Task<User> RegisterAsync(User user)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();
            return user;
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            return await _userRepository.Query().FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> GetUserByIdAsync(int id)
        {
            return await _userRepository.Query().FirstOrDefaultAsync(u => u.UserID == id);
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
            return user;
        }
    }
}
