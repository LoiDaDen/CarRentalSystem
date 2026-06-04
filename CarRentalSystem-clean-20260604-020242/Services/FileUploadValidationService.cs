using Microsoft.AspNetCore.Http;

namespace CarRentalSystem.Services
{
    public interface IFileUploadValidationService
    {
        Task<bool> IsAllowedImageAsync(IFormFile file, long maxBytes);
        Task<bool> IsAllowedImageOrPdfAsync(IFormFile file, long maxBytes);
    }

    public class FileUploadValidationService : IFileUploadValidationService
    {
        private static readonly Dictionary<string, byte[][]> ImageSignatures = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
            [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }
        };

        private static readonly byte[] PdfSignature = { 0x25, 0x50, 0x44, 0x46 };

        public async Task<bool> IsAllowedImageAsync(IFormFile file, long maxBytes)
        {
            if (!ValidateBasic(file, maxBytes, out var ext)) return false;
            if (!ImageSignatures.TryGetValue(ext, out var signatures)) return false;
            return await MatchesAnySignatureAsync(file, signatures);
        }

        public async Task<bool> IsAllowedImageOrPdfAsync(IFormFile file, long maxBytes)
        {
            if (!ValidateBasic(file, maxBytes, out var ext)) return false;
            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                return await MatchesAnySignatureAsync(file, new[] { PdfSignature });

            if (!ImageSignatures.TryGetValue(ext, out var signatures)) return false;
            return await MatchesAnySignatureAsync(file, signatures);
        }

        private static bool ValidateBasic(IFormFile file, long maxBytes, out string extension)
        {
            extension = Path.GetExtension(file.FileName);
            return file.Length > 0 &&
                   file.Length <= maxBytes &&
                   !string.IsNullOrWhiteSpace(extension);
        }

        private static async Task<bool> MatchesAnySignatureAsync(IFormFile file, byte[][] signatures)
        {
            var maxLength = signatures.Max(s => s.Length);
            var buffer = new byte[maxLength];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(buffer);
            return signatures.Any(sig => read >= sig.Length && buffer.Take(sig.Length).SequenceEqual(sig));
        }
    }
}
