using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services;

public interface ICustomerDrivingLicenseSyncService
{
    Task<CustomerDrivingLicense?> EnsureVerifiedLicenseForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<CustomerDrivingLicense> UpsertFromEkycSuccessAsync(
        User user,
        string? licenseNumber,
        string licenseFrontUrl,
        string licenseBackUrl,
        CancellationToken cancellationToken = default);
}

public sealed class CustomerDrivingLicenseSyncService : ICustomerDrivingLicenseSyncService
{
    private const string VerifiedStatus = "Verified";
    private readonly ApplicationDbContext _context;
    private readonly IPiiEncryptionService _piiEncryption;
    private readonly IIdentityHashService _identityHash;

    public CustomerDrivingLicenseSyncService(
        ApplicationDbContext context,
        IPiiEncryptionService piiEncryption,
        IIdentityHashService identityHash)
    {
        _context = context;
        _piiEncryption = piiEncryption;
        _identityHash = identityHash;
    }

    public async Task<CustomerDrivingLicense?> EnsureVerifiedLicenseForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var existing = await GetLatestLicenseAsync(userId, onlyValid: true, cancellationToken);
        if (existing != null)
            return existing;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken);
        if (!HasVerifiedLegacyLicense(user))
            return null;

        return await UpsertFromEkycSuccessAsync(
            user!,
            null,
            user!.DrivingLicenseUrl!,
            user.DrivingLicenseBackUrl ?? user.DrivingLicenseUrl!,
            cancellationToken);
    }

    public async Task<CustomerDrivingLicense> UpsertFromEkycSuccessAsync(
        User user,
        string? licenseNumber,
        string licenseFrontUrl,
        string licenseBackUrl,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var license = await GetLatestLicenseAsync(user.UserID, onlyValid: false, cancellationToken);
        if (license == null)
        {
            license = new CustomerDrivingLicense
            {
                UserID = user.UserID,
                CreatedAt = now
            };
            _context.CustomerDrivingLicenses.Add(license);
        }

        var normalizedLicenseNumber = string.IsNullOrWhiteSpace(licenseNumber)
            ? null
            : licenseNumber.Trim();

        license.LicenseNumber = normalizedLicenseNumber == null
            ? user.DrivingLicenseNumber
            : _piiEncryption.Encrypt(normalizedLicenseNumber);
        license.LicenseNumberHash = normalizedLicenseNumber == null
            ? user.DrivingLicenseHash
            : _identityHash.HashForStorage(normalizedLicenseNumber);
        license.FrontImageUrl = licenseFrontUrl;
        license.BackImageUrl = licenseBackUrl;
        license.VerificationStatus = VerifiedStatus;
        license.IsApproved = true;
        license.IsActive = true;
        license.UpdatedAt = now;

        var otherLicenses = await _context.CustomerDrivingLicenses
            .Where(l => l.UserID == user.UserID && l.CustomerDrivingLicenseID != license.CustomerDrivingLicenseID)
            .ToListAsync(cancellationToken);
        foreach (var other in otherLicenses)
            other.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
        return license;
    }

    private Task<CustomerDrivingLicense?> GetLatestLicenseAsync(int userId, bool onlyValid, CancellationToken cancellationToken)
    {
        var query = _context.CustomerDrivingLicenses
            .Where(l => l.UserID == userId);

        if (onlyValid)
        {
            query = query.Where(l =>
                l.IsActive &&
                l.IsApproved &&
                l.VerificationStatus == VerifiedStatus &&
                (!l.ExpiryDate.HasValue || l.ExpiryDate.Value.Date >= DateTime.UtcNow.Date));
        }

        return query
            .OrderByDescending(l => l.UpdatedAt ?? l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool HasVerifiedLegacyLicense(User? user)
    {
        return user != null &&
            user.IsVerified &&
            string.Equals(user.VerificationStatus, VerifiedStatus, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(user.DrivingLicenseHash) &&
            !string.IsNullOrWhiteSpace(user.DrivingLicenseUrl);
    }
}
