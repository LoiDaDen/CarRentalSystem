using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using Microsoft.EntityFrameworkCore;
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
    }
    
    public class AuthService : IAuthService
    {
        private readonly IRepository<User> _userRepository;

        public AuthService(IRepository<User> userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<bool> IsCitizenIdTakenAsync(string citizenId, int excludeUserId)
        {
            if (string.IsNullOrEmpty(citizenId)) return false;
            return await _userRepository.Query()
                .AnyAsync(u => u.CitizenId == citizenId && u.UserID != excludeUserId);
        }

        public async Task<User> AuthenticateAsync(string email, string password)
        {
            // Hỗ trợ đăng nhập bằng Email hoặc Username (Name)
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
