using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace CarRentalSystem.Services
{
    public sealed record PasswordVerificationResult(bool Success, bool NeedsRehash);

    public interface IPasswordHashService
    {
        string HashPassword(string password);
        PasswordVerificationResult Verify(string password, string? storedHash);
    }

    public sealed class PasswordHashService : IPasswordHashService
    {
        private const string Prefix = "$argon2id$";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int DegreeOfParallelism = 2;
        private const int Iterations = 3;
        private const int MemorySizeKb = 64 * 1024;

        public string HashPassword(string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = ComputeArgon2id(password, salt);
            return $"{Prefix}v=19$m={MemorySizeKb},t={Iterations},p={DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public PasswordVerificationResult Verify(string password, string? storedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
                return new PasswordVerificationResult(false, false);

            if (storedHash.StartsWith(Prefix, StringComparison.Ordinal))
                return VerifyArgon2id(password, storedHash);

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash)
                    ? new PasswordVerificationResult(true, true)
                    : new PasswordVerificationResult(false, false);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return new PasswordVerificationResult(false, false);
            }
        }

        private static PasswordVerificationResult VerifyArgon2id(string password, string storedHash)
        {
            var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 || parts[0] != "argon2id" || parts[1] != "v=19")
                return new PasswordVerificationResult(false, false);

            if (!TryParseParameters(parts[2], out var memory, out var iterations, out var parallelism))
                return new PasswordVerificationResult(false, false);

            byte[] salt;
            byte[] expectedHash;
            try
            {
                salt = Convert.FromBase64String(parts[3]);
                expectedHash = Convert.FromBase64String(parts[4]);
            }
            catch (FormatException)
            {
                return new PasswordVerificationResult(false, false);
            }

            var actualHash = ComputeArgon2id(password, salt, memory, iterations, parallelism, expectedHash.Length);
            var success = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            var needsRehash = success &&
                (memory != MemorySizeKb || iterations != Iterations || parallelism != DegreeOfParallelism || expectedHash.Length != HashSize);
            return new PasswordVerificationResult(success, needsRehash);
        }

        private static byte[] ComputeArgon2id(
            string password,
            byte[] salt,
            int memorySizeKb = MemorySizeKb,
            int iterations = Iterations,
            int degreeOfParallelism = DegreeOfParallelism,
            int hashSize = HashSize)
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = degreeOfParallelism,
                Iterations = iterations,
                MemorySize = memorySizeKb
            };
            return argon2.GetBytes(hashSize);
        }

        private static bool TryParseParameters(string value, out int memory, out int iterations, out int parallelism)
        {
            memory = iterations = parallelism = 0;
            foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var pair = item.Split('=', 2);
                if (pair.Length != 2 || !int.TryParse(pair[1], out var parsed))
                    return false;

                switch (pair[0])
                {
                    case "m":
                        memory = parsed;
                        break;
                    case "t":
                        iterations = parsed;
                        break;
                    case "p":
                        parallelism = parsed;
                        break;
                    default:
                        return false;
                }
            }

            return memory > 0 && iterations > 0 && parallelism > 0;
        }
    }
}
