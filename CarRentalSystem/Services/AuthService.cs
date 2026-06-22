using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
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
        private readonly IIdentityHashService _identityHash;
        private readonly IPasswordHashService _passwordHashService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IRepository<User> userRepository,
            IPiiEncryptionService piiEncryption,
            IIdentityHashService identityHash,
            IPasswordHashService passwordHashService,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _piiEncryption = piiEncryption;
            _identityHash = identityHash;
            _passwordHashService = passwordHashService;
            _logger = logger;
        }

        public async Task<bool> IsCitizenIdTakenAsync(string citizenId, int excludeUserId)
        {
            if (string.IsNullOrEmpty(citizenId)) return false;
            var hashes = _identityHash.HashesForLookup(citizenId);

            // Tối ưu: Filter ở database level thay vì load tất cả users vào memory
            var exists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.CitizenIdHash != null && hashes.Contains(u.CitizenIdHash))
                .AnyAsync();

            if (exists) return true;

            // Kiểm tra legacy accounts chưa có hash bằng giá trị thực, tránh coi mọi bản ghi legacy là trùng.
            var legacyCitizenIds = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.CitizenIdHash == null && u.CitizenId != null)
                .Select(u => u.CitizenId)
                .ToListAsync();

            return legacyCitizenIds.Any(stored =>
                string.Equals(TryDecryptLegacyPii(stored, "CitizenId")?.Trim(), citizenId.Trim(), StringComparison.Ordinal));
        }

        public async Task<bool> IsDrivingLicenseTakenAsync(string licenseNumber, int excludeUserId)
        {
            if (string.IsNullOrEmpty(licenseNumber)) return false;
            var hashes = _identityHash.HashesForLookup(licenseNumber);

            // Tối ưu: Filter ở database level
            var exists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.DrivingLicenseHash != null && hashes.Contains(u.DrivingLicenseHash))
                .AnyAsync();

            if (exists) return true;

            // Kiểm tra legacy accounts chưa có hash bằng giá trị thực.
            var legacyLicenseNumbers = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.DrivingLicenseHash == null && u.DrivingLicenseNumber != null)
                .Select(u => u.DrivingLicenseNumber)
                .ToListAsync();

            return legacyLicenseNumbers.Any(stored =>
                string.Equals(TryDecryptLegacyPii(stored, "DrivingLicenseNumber")?.Trim(), licenseNumber.Trim(), StringComparison.Ordinal));
        }

        private string? TryDecryptLegacyPii(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            try
            {
                return _piiEncryption.DecryptIfNotEmpty(value);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException || ex is ArgumentException)
            {
                _logger.LogWarning(ex, "Skipping corrupt legacy PII value while checking uniqueness for {FieldName}.", fieldName);
                return null;
            }
        }

        public async Task<bool> IsPhoneTakenAsync(string phone, int excludeUserId)
        {
            if (string.IsNullOrEmpty(phone)) return false;
            var hashes = _identityHash.HashesForLookup(phone);

            // Tối ưu: Filter ở database level
            var exists = await _userRepository.Query()
                .Where(u => u.UserID != excludeUserId)
                .Where(u => u.PhoneHash != null && hashes.Contains(u.PhoneHash))
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

            if (user != null)
            {
                var verification = _passwordHashService.Verify(password, user.PasswordHash);
                if (!verification.Success)
                    return null;

                if (verification.NeedsRehash)
                {
                    user.PasswordHash = _passwordHashService.HashPassword(password);
                    _userRepository.Update(user);
                    await _userRepository.SaveChangesAsync();
                }

                return user;
            }
            return null;
        }

        public async Task<User> RegisterAsync(User user)
        {
            user.PasswordHash = _passwordHashService.HashPassword(user.PasswordHash);
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
