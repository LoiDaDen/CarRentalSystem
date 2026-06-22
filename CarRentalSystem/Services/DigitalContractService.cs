using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IDigitalContractService
    {
        Task<DigitalContract?> EnsureForBookingAsync(int bookingId, int? actorUserId = null, string? requestUrl = null, CancellationToken cancellationToken = default);
        Task<DigitalContract?> EnsureForTransactionAsync(int transactionId, int? actorUserId = null, string? requestUrl = null, CancellationToken cancellationToken = default);
        Task<DigitalContractSignResult> SignCustomerAsync(int contractId, int customerUserId, string? signatureBase64, string contentRootPath, string? ipAddress, string? userAgent, string? requestUrl = null, CancellationToken cancellationToken = default);
        string RenderContractContent(DigitalContract contract);
        string ComputeRenderedContentHash(DigitalContract contract);
    }

    public record DigitalContractSignResult(bool IsSuccess, DigitalContract? Contract, string? ErrorMessage);

    public class DigitalContractService : IDigitalContractService
    {
        private const int ContractVersion = 2;
        private const int MaxSignatureBytes = 1024 * 1024;
        private const string CustomerSignaturePrefix = "data:image/png;base64,";

        private readonly ApplicationDbContext _context;
        private readonly IPiiEncryptionService _piiEncryption;
        private readonly IAuditTrailService _auditTrail;

        public DigitalContractService(
            ApplicationDbContext context,
            IPiiEncryptionService piiEncryption,
            IAuditTrailService auditTrail)
        {
            _context = context;
            _piiEncryption = piiEncryption;
            _auditTrail = auditTrail;
        }

        public async Task<DigitalContract?> EnsureForBookingAsync(int bookingId, int? actorUserId = null, string? requestUrl = null, CancellationToken cancellationToken = default)
        {
            var existing = await _context.DigitalContracts
                .FirstOrDefaultAsync(c => c.BookingID == bookingId, cancellationToken);
            if (existing != null)
            {
                await EnsureContractEvidenceAsync(existing, cancellationToken);
                return existing;
            }

            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(b => b.Car).ThenInclude(c => c!.Owner)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId, cancellationToken);
            if (booking == null)
                throw new InvalidOperationException("Booking not found.");

            if (!RequiresDigitalContract(booking))
                return null;

            var ownerEvidence = await BuildOwnerSignatureEvidenceAsync(GetBookingOwnerIds(new[] { booking }), cancellationToken);
            var contract = BuildBookingContract(booking, ownerEvidence);
            _context.DigitalContracts.Add(contract);
            await _context.SaveChangesAsync(cancellationToken);
            await RecordContractCreatedAsync(contract, actorUserId ?? booking.UserID, requestUrl, cancellationToken);

            return contract;
        }

        public async Task<DigitalContract?> EnsureForTransactionAsync(int transactionId, int? actorUserId = null, string? requestUrl = null, CancellationToken cancellationToken = default)
        {
            var existing = await _context.DigitalContracts
                .FirstOrDefaultAsync(c => c.TransactionID == transactionId, cancellationToken);
            if (existing != null)
            {
                await EnsureContractEvidenceAsync(existing, cancellationToken);
                return existing;
            }

            var transaction = await _context.BookingTransactions
                .Include(t => t.User)
                .Include(t => t.Bookings).ThenInclude(b => b.Car).ThenInclude(c => c!.CarBrand)
                .Include(t => t.Bookings).ThenInclude(b => b.Car).ThenInclude(c => c!.Owner)
                .FirstOrDefaultAsync(t => t.TransactionID == transactionId, cancellationToken);
            if (transaction == null)
                throw new InvalidOperationException("Booking transaction not found.");
            if (!transaction.Bookings.Any())
                throw new InvalidOperationException("Booking transaction has no bookings.");

            var modes = transaction.Bookings
                .Select(b => CarContractSigningModes.Normalize(b.Car?.ContractSigningMode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (modes.Count > 1)
                throw new InvalidOperationException("Cart contains mixed contract signing modes.");
            if (modes[0] == CarContractSigningModes.PhysicalSelfManaged)
                return null;

            var ownerEvidence = await BuildOwnerSignatureEvidenceAsync(GetBookingOwnerIds(transaction.Bookings), cancellationToken);
            var contract = BuildTransactionContract(transaction, ownerEvidence);
            _context.DigitalContracts.Add(contract);
            await _context.SaveChangesAsync(cancellationToken);
            await RecordContractCreatedAsync(contract, actorUserId ?? transaction.UserID, requestUrl, cancellationToken);

            return contract;
        }

        public async Task<DigitalContractSignResult> SignCustomerAsync(
            int contractId,
            int customerUserId,
            string? signatureBase64,
            string contentRootPath,
            string? ipAddress,
            string? userAgent,
            string? requestUrl = null,
            CancellationToken cancellationToken = default)
        {
            var contract = await _context.DigitalContracts
                .Include(c => c.Booking)
                .Include(c => c.BookingTransaction)
                .FirstOrDefaultAsync(c => c.DigitalContractID == contractId, cancellationToken);
            if (contract == null)
                return new DigitalContractSignResult(false, null, "Không tìm thấy hợp đồng.");

            var ownerUserId = contract.Booking?.UserID ?? contract.BookingTransaction?.UserID;
            if (ownerUserId != customerUserId)
                return new DigitalContractSignResult(false, contract, "Bạn không có quyền ký hợp đồng này.");

            if (contract.IsSignedByCustomer)
                return new DigitalContractSignResult(false, contract, "Hợp đồng này đã được ký.");

            if (!TryDecodePngSignature(signatureBase64, out var signatureBytes, out var errorMessage))
                return new DigitalContractSignResult(false, contract, errorMessage);

            await EnsureContractEvidenceAsync(contract, cancellationToken);

            var ownerFolder = Path.Combine(contentRootPath, "App_Data", "SecureUploads", "Contracts", customerUserId.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(ownerFolder);

            var signatureFileName = $"{Guid.NewGuid():N}_sig.png";
            var signaturePath = Path.Combine(ownerFolder, signatureFileName);
            await File.WriteAllBytesAsync(signaturePath, signatureBytes!, cancellationToken);

            var now = DateTime.UtcNow;
            contract.CustomerSignatureUrl = $"/SecureFile/GetSecureImage?folder=Contracts&fileName={customerUserId}/{signatureFileName}";
            contract.CustomerSignatureHash = ComputeSha256Hex(signatureBytes!);
            contract.IsSignedByCustomer = true;
            contract.CustomerSignDate = now;
            contract.SignedAt = now;
            contract.SignedIpAddress = Truncate(ipAddress, 45);
            contract.SignedUserAgent = Truncate(userAgent, 500);
            contract.SignerIdentitySnapshotJson = await BuildSignerSnapshotAsync(customerUserId, cancellationToken);
            contract.RenderedContentHash ??= ComputeRenderedContentHash(contract);

            await _context.SaveChangesAsync(cancellationToken);

            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "SignDigitalContract",
                EntityType = nameof(DigitalContract),
                EntityId = contract.DigitalContractID,
                UserId = customerUserId,
                Description = $"Customer signed digital contract {contract.ContractNo}.",
                IpAddress = Truncate(ipAddress, 45),
                UserAgent = Truncate(userAgent, 500),
                RequestUrl = requestUrl,
                NewValues = JsonSerializer.Serialize(new
                {
                    contract.DigitalContractID,
                    contract.BookingID,
                    contract.TransactionID,
                    contract.CustomerSignatureHash,
                    contract.RenderedContentHash,
                    contract.SignedAt
                })
            });

            return new DigitalContractSignResult(true, contract, null);
        }

        public string RenderContractContent(DigitalContract contract)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"ContractNo: {contract.ContractNo}");
            builder.AppendLine($"Version: {contract.ContractVersion}");
            builder.AppendLine($"ContractSigningMode: {contract.ContractSigningMode}");
            builder.AppendLine($"OwnerUserIds: {contract.OwnerUserIdsJson}");
            builder.AppendLine($"OwnerSignatureHash: {contract.OwnerSignatureHash}");
            builder.AppendLine($"OwnerSignatureProfileSnapshot: {contract.OwnerSignatureProfileSnapshotJson}");
            builder.AppendLine($"CustomerName: {contract.CustomerName}");
            builder.AppendLine($"CustomerIdentityCard: {contract.CustomerIdentityCard}");
            builder.AppendLine($"CustomerPhone: {contract.CustomerPhone}");
            builder.AppendLine($"CustomerAddress: {contract.CustomerAddress}");
            builder.AppendLine($"CarLicensePlate: {contract.CarLicensePlate}");
            builder.AppendLine($"CarModel: {contract.CarModel}");
            builder.AppendLine($"RentalStartDate: {contract.RentalStartDate:O}");
            builder.AppendLine($"RentalEndDate: {contract.RentalEndDate:O}");
            builder.AppendLine($"RentalPricePerDay: {contract.RentalPricePerDay.ToString("0.##", CultureInfo.InvariantCulture)}");
            builder.AppendLine($"TotalContractValue: {contract.TotalContractValue.ToString("0.##", CultureInfo.InvariantCulture)}");
            builder.AppendLine($"DepositAmount: {contract.DepositAmount.ToString("0.##", CultureInfo.InvariantCulture)}");
            builder.AppendLine("TermsAndConditions:");
            builder.AppendLine(contract.TermsAndConditions ?? string.Empty);
            return builder.ToString();
        }

        public string ComputeRenderedContentHash(DigitalContract contract)
        {
            return ComputeSha256Hex(Encoding.UTF8.GetBytes(RenderContractContent(contract)));
        }

        private static bool RequiresDigitalContract(Booking booking)
        {
            return CarContractSigningModes.Normalize(booking.Car?.ContractSigningMode) == CarContractSigningModes.Digital;
        }

        private static IReadOnlyList<int> GetBookingOwnerIds(IEnumerable<Booking> bookings)
        {
            return bookings
                .Select(b => b.Car?.OwnerID)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        private async Task<OwnerSignatureEvidence> BuildOwnerSignatureEvidenceAsync(IReadOnlyList<int> ownerIds, CancellationToken cancellationToken)
        {
            if (ownerIds.Count == 0)
                throw new InvalidOperationException("Digital contracts require an owner signature profile.");

            var users = await _context.Users
                .AsNoTracking()
                .Where(u => ownerIds.Contains(u.UserID))
                .ToDictionaryAsync(u => u.UserID, cancellationToken);

            var profiles = await _context.OwnerDigitalSignatureProfiles
                .AsNoTracking()
                .Where(p => ownerIds.Contains(p.UserID))
                .ToDictionaryAsync(p => p.UserID, cancellationToken);

            var snapshots = new List<object>();
            foreach (var ownerId in ownerIds)
            {
                if (!profiles.TryGetValue(ownerId, out var profile) || !profile.IsReady)
                    throw new InvalidOperationException("Chủ xe chưa hoàn tất hồ sơ chữ ký số.");

                users.TryGetValue(ownerId, out var user);
                snapshots.Add(new
                {
                    OwnerUserID = ownerId,
                    OwnerName = user?.Name,
                    user?.Email,
                    Provider = profile.ProviderName,
                    SerialNumber = profile.CertificateSerialNumber,
                    Subject = profile.CertificateSubject,
                    ValidUntil = profile.CertificateValidUntil,
                    profile.SignatureImageUrl,
                    profile.SignatureImageHash,
                    SnapshotAt = DateTime.UtcNow
                });
            }

            var firstProfile = profiles[ownerIds[0]];
            return new OwnerSignatureEvidence(
                JsonSerializer.Serialize(ownerIds),
                firstProfile.SignatureImageUrl,
                string.Join("|", ownerIds.Select(id => profiles[id].SignatureImageHash).Where(hash => !string.IsNullOrWhiteSpace(hash))),
                JsonSerializer.Serialize(snapshots));
        }

        private DigitalContract BuildBookingContract(Booking booking, OwnerSignatureEvidence ownerEvidence)
        {
            var carName = $"{booking.Car?.CarBrand?.Name} {booking.Car?.CarName}".Trim();
            var contract = new DigitalContract
            {
                BookingID = booking.BookingID,
                ContractNo = $"HD-B{booking.BookingID}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                CustomerName = booking.User?.Name ?? string.Empty,
                CustomerIdentityCard = _piiEncryption.DecryptIfNotEmpty(booking.User?.CitizenId) ?? string.Empty,
                CustomerPhone = booking.User?.Phone ?? string.Empty,
                CustomerAddress = booking.User?.Address ?? string.Empty,
                CarLicensePlate = booking.Car?.LicensePlate ?? string.Empty,
                CarModel = string.IsNullOrWhiteSpace(carName) ? "N/A" : carName,
                RentalStartDate = booking.StartDate,
                RentalEndDate = booking.EndDate,
                RentalPricePerDay = ResolveDailyPrice(booking.TotalPrice, booking.StartDate, booking.EndDate),
                TotalContractValue = booking.TotalPrice,
                DepositAmount = ResolveDepositAmount(booking),
                PaymentMethod = booking.Payment?.PaymentMethod,
                TermsAndConditions = BuildTerms(
                    $"- Xe: {carName}, BKS: {booking.Car?.LicensePlate ?? "N/A"}, Giá trị thuê: {booking.TotalPrice:N0} VND",
                    booking.TotalPrice,
                    ResolveDepositAmount(booking),
                    booking.StartDate,
                    booking.EndDate),
                ContractVersion = ContractVersion,
                ContractSigningMode = CarContractSigningModes.Digital,
                OwnerUserIdsJson = ownerEvidence.OwnerUserIdsJson,
                OwnerSignatureUrl = ownerEvidence.OwnerSignatureUrl,
                OwnerSignatureHash = ownerEvidence.OwnerSignatureHash,
                OwnerSignatureProfileSnapshotJson = ownerEvidence.OwnerSignatureProfileSnapshotJson,
                IsSignedByCustomer = false,
                IsSignedByOwner = true,
                OwnerSignDate = DateTime.UtcNow
            };
            contract.RenderedContentHash = ComputeRenderedContentHash(contract);
            return contract;
        }

        private DigitalContract BuildTransactionContract(BookingTransaction transaction, OwnerSignatureEvidence ownerEvidence)
        {
            var bookings = transaction.Bookings.OrderBy(b => b.BookingID).ToList();
            var firstStart = bookings.Min(b => b.StartDate);
            var lastEnd = bookings.Max(b => b.EndDate);
            var totalValue = bookings.Sum(b => b.TotalPrice);
            var totalDeposit = bookings.Sum(ResolveDepositAmount);
            var carLines = bookings.Select((booking, index) =>
            {
                var carName = $"{booking.Car?.CarBrand?.Name} {booking.Car?.CarName}".Trim();
                return $"- Xe {index + 1}: {carName}, BKS: {booking.Car?.LicensePlate ?? "N/A"}, Giá trị thuê: {booking.TotalPrice:N0} VND";
            }).ToList();

            var contract = new DigitalContract
            {
                TransactionID = transaction.TransactionID,
                ContractNo = $"HD-T{transaction.TransactionID}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                CustomerName = transaction.User?.Name ?? string.Empty,
                CustomerIdentityCard = _piiEncryption.DecryptIfNotEmpty(transaction.User?.CitizenId) ?? string.Empty,
                CustomerPhone = transaction.User?.Phone ?? string.Empty,
                CustomerAddress = transaction.User?.Address ?? string.Empty,
                CarLicensePlate = string.Join(", ", bookings.Select(b => b.Car?.LicensePlate ?? "N/A")),
                CarModel = string.Join(" | ", bookings.Select(b => $"{b.Car?.CarBrand?.Name} {b.Car?.CarName}".Trim())),
                RentalStartDate = firstStart,
                RentalEndDate = lastEnd,
                RentalPricePerDay = 0,
                TotalContractValue = totalValue > 0 ? totalValue : transaction.TotalAmount,
                DepositAmount = totalDeposit,
                TermsAndConditions = BuildTerms(string.Join(Environment.NewLine, carLines), totalValue > 0 ? totalValue : transaction.TotalAmount, totalDeposit, firstStart, lastEnd),
                ContractVersion = ContractVersion,
                ContractSigningMode = CarContractSigningModes.Digital,
                OwnerUserIdsJson = ownerEvidence.OwnerUserIdsJson,
                OwnerSignatureUrl = ownerEvidence.OwnerSignatureUrl,
                OwnerSignatureHash = ownerEvidence.OwnerSignatureHash,
                OwnerSignatureProfileSnapshotJson = ownerEvidence.OwnerSignatureProfileSnapshotJson,
                IsSignedByCustomer = false,
                IsSignedByOwner = true,
                OwnerSignDate = DateTime.UtcNow
            };
            contract.RenderedContentHash = ComputeRenderedContentHash(contract);
            return contract;
        }

        private static decimal ResolveDepositAmount(Booking booking)
        {
            if (booking.DepositAmount > 0) return booking.DepositAmount;
            if (booking.ReservationFee > 0) return booking.ReservationFee;
            return DepositPolicy.Calculate(booking.TotalPrice);
        }

        private async Task EnsureContractEvidenceAsync(DigitalContract contract, CancellationToken cancellationToken)
        {
            var changed = false;
            if (contract.ContractVersion <= 0)
            {
                contract.ContractVersion = ContractVersion;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(contract.ContractSigningMode))
            {
                contract.ContractSigningMode = CarContractSigningModes.Digital;
                changed = true;
            }

            if (!contract.IsSignedByOwner)
            {
                contract.IsSignedByOwner = true;
                contract.OwnerSignDate = DateTime.UtcNow;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(contract.RenderedContentHash))
            {
                contract.RenderedContentHash = ComputeRenderedContentHash(contract);
                changed = true;
            }

            if (changed)
                await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<string> BuildSignerSnapshotAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken);
            object snapshot = user == null
                ? new { UserID = userId }
                : new
                {
                    user.UserID,
                    user.Name,
                    user.Email,
                    user.Phone,
                    user.CitizenIdHash,
                    user.VerificationStatus,
                    user.IsVerified,
                    SnapshotAt = DateTime.UtcNow
                };

            return JsonSerializer.Serialize(snapshot);
        }

        private async Task RecordContractCreatedAsync(DigitalContract contract, int actorUserId, string? requestUrl, CancellationToken cancellationToken)
        {
            await _auditTrail.RecordAsync(new AuditLog
            {
                Action = "CreateDigitalContract",
                EntityType = nameof(DigitalContract),
                EntityId = contract.DigitalContractID,
                UserId = actorUserId,
                Description = $"Digital contract {contract.ContractNo} created.",
                RequestUrl = requestUrl,
                NewValues = JsonSerializer.Serialize(new
                {
                    contract.DigitalContractID,
                    contract.BookingID,
                    contract.TransactionID,
                    contract.ContractNo,
                    contract.RenderedContentHash
                })
            });
        }

        private static string BuildTerms(string vehicleLines, decimal totalValue, decimal depositAmount, DateTime startDate, DateTime endDate)
        {
            return $@"Sau khi bàn bạc, thỏa thuận, hai bên thống nhất ký kết Hợp đồng thuê xe tự lái với các điều khoản như sau:

ĐIỀU 1: NỘI DUNG HỢP ĐỒNG
{vehicleLines}

ĐIỀU 2: GIÁ TRỊ HỢP ĐỒNG, PHƯƠNG THỨC THANH TOÁN
- Tổng giá trị thuê xe: {totalValue:N0} VND.
- Tiền cọc/thế chấp dự kiến: {depositAmount:N0} VND.
- Khách thuê thanh toán các khoản còn lại theo chính sách thanh toán và bàn giao xe của hệ thống.

ĐIỀU 3: TRÁCH NHIỆM CHUNG
- Khách thuê cam kết là người trực tiếp điều khiển xe và có giấy phép lái xe hợp lệ.
- Không sử dụng xe thuê vào mục đích vi phạm pháp luật, cầm cố, thế chấp, cho thuê lại hoặc giao cho người không đủ điều kiện điều khiển.
- Khách thuê chịu chi phí nhiên liệu, cầu đường, bãi đỗ, phạt vi phạm giao thông và các phát sinh do lỗi sử dụng trong thời gian thuê.
- Khách thuê nhận xe, kiểm tra hiện trạng và ký biên bản bàn giao trước khi sử dụng xe.
- Khách thuê trả xe đúng thời gian, địa điểm và tình trạng đã thỏa thuận, trừ hao mòn hợp lý.

ĐIỀU 4: HIỆU LỰC HỢP ĐỒNG
- Hợp đồng có giá trị từ {startDate:HH:mm dd/MM/yyyy} đến hết {endDate:HH:mm dd/MM/yyyy}.
- Hợp đồng điện tử này được giao kết bằng thông điệp dữ liệu và bằng chứng ký lưu trong hệ thống.";
        }

        private static decimal ResolveDailyPrice(decimal totalPrice, DateTime startDate, DateTime endDate)
        {
            var days = Math.Max(1, (int)Math.Ceiling((endDate - startDate).TotalDays));
            return Math.Round(totalPrice / days, 0, MidpointRounding.AwayFromZero);
        }

        private static bool TryDecodePngSignature(string? signatureBase64, out byte[]? bytes, out string? errorMessage)
        {
            bytes = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(signatureBase64))
            {
                errorMessage = "Vui lòng ký trước khi gửi hợp đồng.";
                return false;
            }

            var payload = signatureBase64.Trim();
            if (payload.StartsWith(CustomerSignaturePrefix, StringComparison.OrdinalIgnoreCase))
                payload = payload[CustomerSignaturePrefix.Length..];
            else if (payload.Contains(',', StringComparison.Ordinal))
                payload = payload[(payload.IndexOf(",", StringComparison.Ordinal) + 1)..];

            try
            {
                bytes = Convert.FromBase64String(payload);
            }
            catch (FormatException)
            {
                errorMessage = "Chữ ký không đúng định dạng.";
                return false;
            }

            if (bytes.Length == 0 || bytes.Length > MaxSignatureBytes)
            {
                errorMessage = "Chữ ký rỗng hoặc vượt quá dung lượng cho phép.";
                return false;
            }

            if (!HasPngSignature(bytes))
            {
                errorMessage = "Chữ ký phải là ảnh PNG hợp lệ.";
                return false;
            }

            return true;
        }

        private static bool HasPngSignature(byte[] bytes)
        {
            byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            return bytes.Length >= png.Length && png.SequenceEqual(bytes.Take(png.Length));
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private sealed record OwnerSignatureEvidence(
            string OwnerUserIdsJson,
            string? OwnerSignatureUrl,
            string OwnerSignatureHash,
            string OwnerSignatureProfileSnapshotJson);
    }
}
