using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public record RefundServiceResult(bool IsSuccess, string Message, RefundRequest? RefundRequest = null, RefundPayout? Payout = null);

    public interface IRefundService
    {
        Task<RefundRequest> RequestRefundAsync(int bookingId, decimal amount, string reason);
        Task<RefundServiceResult> ApproveAndStartPayoutAsync(int refundRequestId, int approvedByUserId, string recipientBin, string recipientAccountNumber, string? recipientAccountName);
        Task<int> PollPendingPayoutsAsync();
    }

    public class RefundService : IRefundService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentLedgerService _ledger;
        private readonly IPayOSPayoutService _payOSPayoutService;
        private readonly IPiiEncryptionService _piiEncryption;

        public RefundService(
            ApplicationDbContext context,
            IPaymentLedgerService ledger,
            IPayOSPayoutService payOSPayoutService,
            IPiiEncryptionService piiEncryption)
        {
            _context = context;
            _ledger = ledger;
            _payOSPayoutService = payOSPayoutService;
            _piiEncryption = piiEncryption;
        }

        public Task<RefundRequest> RequestRefundAsync(int bookingId, decimal amount, string reason)
        {
            return _ledger.CreateRefundRequestAsync(bookingId, amount, reason);
        }

        public async Task<RefundServiceResult> ApproveAndStartPayoutAsync(int refundRequestId, int approvedByUserId, string recipientBin, string recipientAccountNumber, string? recipientAccountName)
        {
            var request = await _context.RefundRequests
                .Include(r => r.Booking)
                .FirstOrDefaultAsync(r => r.RefundRequestID == refundRequestId);
            if (request == null)
                return new RefundServiceResult(false, "Refund request not found.");
            if (request.Amount <= 0)
                return new RefundServiceResult(false, "Refund amount is invalid.", request);

            var existing = await _context.RefundPayouts
                .FirstOrDefaultAsync(p => p.RefundRequestID == refundRequestId &&
                    p.TransactionState != PaymentStatusConstants.Failed);
            if (existing != null)
                return new RefundServiceResult(true, "Refund payout already exists.", request, existing);

            request.Status = RefundRequestStatusConstants.Approved;
            request.ApprovedByUserID = approvedByUserId;
            request.ApprovedAt = DateTime.UtcNow;

            var payout = new RefundPayout
            {
                RefundRequestID = refundRequestId,
                ReferenceId = $"refund_{refundRequestId}",
                IdempotencyKey = $"refund-payout:{refundRequestId}",
                Amount = request.Amount,
                ApprovalState = PaymentStatusConstants.Pending,
                TransactionState = PaymentStatusConstants.Pending,
                EncryptedRecipientBin = _piiEncryption.Encrypt(recipientBin),
                EncryptedRecipientAccountNumber = _piiEncryption.Encrypt(recipientAccountNumber),
                EncryptedRecipientAccountName = _piiEncryption.EncryptIfNotEmpty(recipientAccountName),
                MaskedRecipientAccount = MaskAccount(recipientAccountNumber)
            };
            _context.RefundPayouts.Add(payout);
            await _context.SaveChangesAsync();

            if (!_payOSPayoutService.IsEnabled)
            {
                request.Status = RefundRequestStatusConstants.PendingManual;
                payout.ApprovalState = PaymentStatusConstants.PendingManual;
                payout.TransactionState = PaymentStatusConstants.PendingManual;
                payout.LastError = "PayOS payout is disabled or not configured.";
                await _context.SaveChangesAsync();
                return new RefundServiceResult(true, "PayOS payout disabled; refund moved to manual queue.", request, payout);
            }

            payout.AttemptCount++;
            payout.SubmittedAt = DateTime.UtcNow;
            var providerResult = await _payOSPayoutService.CreatePayoutAsync(new PayOSPayoutCreateCommand(
                payout.ReferenceId,
                payout.Amount,
                $"Hoan coc booking #{request.BookingID}",
                recipientBin,
                recipientAccountNumber,
                recipientAccountName,
                payout.IdempotencyKey));

            payout.RawResponse = providerResult.RawResponse;
            if (!providerResult.IsSuccess)
            {
                payout.LastError = providerResult.ErrorMessage;
                if (providerResult.IsPermissionMissing)
                {
                    request.Status = RefundRequestStatusConstants.PendingManual;
                    payout.ApprovalState = PaymentStatusConstants.PendingManual;
                    payout.TransactionState = PaymentStatusConstants.PendingManual;
                    await _context.SaveChangesAsync();
                    return new RefundServiceResult(true, "PayOS payout permission missing; refund moved to manual queue.", request, payout);
                }

                request.Status = RefundRequestStatusConstants.Failed;
                payout.ApprovalState = PaymentStatusConstants.Failed;
                payout.TransactionState = PaymentStatusConstants.Failed;
                payout.FailedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return new RefundServiceResult(false, providerResult.ErrorMessage ?? "PayOS payout failed.", request, payout);
            }

            payout.PayOSPayoutId = providerResult.PayoutId;
            payout.ApprovalState = providerResult.ApprovalState ?? PaymentStatusConstants.Processing;
            payout.TransactionState = providerResult.TransactionState ?? PaymentStatusConstants.Processing;
            request.Status = RefundRequestStatusConstants.Processing;
            await _context.SaveChangesAsync();

            return new RefundServiceResult(true, "PayOS payout submitted.", request, payout);
        }

        public async Task<int> PollPendingPayoutsAsync()
        {
            var payouts = await _context.RefundPayouts
                .Include(p => p.RefundRequest)
                    .ThenInclude(r => r!.Booking)
                .Where(p => p.PayOSPayoutId != null &&
                    p.TransactionState != PaymentStatusConstants.Refunded &&
                    p.TransactionState != RefundRequestStatusConstants.Paid &&
                    p.TransactionState != PaymentStatusConstants.Failed &&
                    p.TransactionState != PaymentStatusConstants.PendingManual)
                .ToListAsync();

            var updated = 0;
            foreach (var payout in payouts)
            {
                var providerResult = await _payOSPayoutService.GetPayoutAsync(payout.PayOSPayoutId!);
                payout.AttemptCount++;
                payout.RawResponse = providerResult.RawResponse ?? payout.RawResponse;
                if (!providerResult.IsSuccess)
                {
                    payout.LastError = providerResult.ErrorMessage;
                    continue;
                }

                payout.ApprovalState = providerResult.ApprovalState ?? payout.ApprovalState;
                payout.TransactionState = providerResult.TransactionState ?? payout.TransactionState;

                if (IsSucceeded(payout.TransactionState))
                {
                    payout.TransactionState = RefundRequestStatusConstants.Paid;
                    payout.CompletedAt = DateTime.UtcNow;
                    if (payout.RefundRequest != null)
                    {
                        payout.RefundRequest.Status = RefundRequestStatusConstants.Paid;
                        payout.RefundRequest.PaidAt = DateTime.UtcNow;
                        if (payout.RefundRequest.Booking != null)
                            payout.RefundRequest.Booking.DepositStatus = DepositStatusConstants.Refunded;

                        await _ledger.RecordAsync(new PaymentLedgerEntry
                        {
                            BookingID = payout.RefundRequest.BookingID,
                            EntryType = PaymentLedgerEntryTypeConstants.Refund,
                            Amount = payout.Amount,
                            Provider = "PayOS",
                            Status = PaymentStatusConstants.Refunded,
                            PostedAt = DateTime.UtcNow,
                            IdempotencyKey = $"refund-payout-paid:{payout.RefundPayoutID}",
                            Description = $"PayOS refund payout reconciled for request #{payout.RefundRequestID}."
                        });
                    }
                }
                else if (IsFailed(payout.TransactionState))
                {
                    payout.FailedAt = DateTime.UtcNow;
                    if (payout.RefundRequest != null)
                        payout.RefundRequest.Status = RefundRequestStatusConstants.Failed;
                }

                updated++;
            }

            await _context.SaveChangesAsync();
            return updated;
        }

        private static bool IsSucceeded(string? state)
        {
            return string.Equals(state, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "SUCCESS", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "PAID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFailed(string? state)
        {
            return string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "REJECTED", StringComparison.OrdinalIgnoreCase);
        }

        private static string MaskAccount(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber)) return string.Empty;
            var clean = accountNumber.Trim();
            return clean.Length <= 4 ? new string('*', clean.Length) : new string('*', clean.Length - 4) + clean[^4..];
        }
    }
}
