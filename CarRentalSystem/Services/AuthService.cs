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

            var users = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .ToListAsync();

            foreach (var u in users)
            {
                // Kiem tra theo hash (tai khoan moi)
                if (!string.IsNullOrEmpty(u.CitizenIdHash) && u.CitizenIdHash == hash)
                    return true;

                // Kiem tra trong CitizenId cu (chua co hash) — so sanh truc tiep
                if (string.IsNullOrEmpty(u.CitizenIdHash) && !string.IsNullOrEmpty(u.CitizenId))
                {
                    var decrypted = _piiEncryption.DecryptIfNotEmpty(u.CitizenId);
                    if (!string.IsNullOrEmpty(decrypted) && decrypted.Trim() == citizenId.Trim())
                        return true;
                }
            }
            return false;
        }

        public async Task<bool> IsDrivingLicenseTakenAsync(string licenseNumber, int excludeUserId)
        {
            if (string.IsNullOrEmpty(licenseNumber)) return false;
            var hash = ComputeHash(licenseNumber);

            var users = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .ToListAsync();

            foreach (var u in users)
            {
                if (!string.IsNullOrEmpty(u.DrivingLicenseHash) && u.DrivingLicenseHash == hash)
                    return true;

                if (string.IsNullOrEmpty(u.DrivingLicenseHash) && !string.IsNullOrEmpty(u.DrivingLicenseNumber))
                {
                    var decrypted = _piiEncryption.DecryptIfNotEmpty(u.DrivingLicenseNumber);
                    if (!string.IsNullOrEmpty(decrypted) && decrypted.Trim() == licenseNumber.Trim())
                        return true;
                }
            }
            return false;
        }

        public async Task<bool> IsPhoneTakenAsync(string phone, int excludeUserId)
        {
            if (string.IsNullOrEmpty(phone)) return false;
            var hash = ComputeHash(phone);

            var users = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .ToListAsync();

            foreach (var u in users)
            {
                if (!string.IsNullOrEmpty(u.PhoneHash) && u.PhoneHash == hash)
                    return true;

                // Phone co the la plaintext cu — so sanh truc tiep
                if (string.IsNullOrEmpty(u.PhoneHash) && !string.IsNullOrEmpty(u.Phone)
                    && u.Phone.Trim() == phone.Trim())
                    return true;
            }
            return false;
        }

        public async Task<User> AuthenticateAsync(string email, string password)
        {
            var user = await _userRepository.Query()
                .FirstOrDefaultAsync(u => u.Email == email || u.Name == email);
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
