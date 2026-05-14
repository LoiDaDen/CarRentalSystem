using CarRentalSystem.Services;

namespace CarRentalSystem.BackgroundJobs
{
    public class RefundPayoutPollingJob
    {
        private readonly IRefundService _refundService;
        private readonly ILogger<RefundPayoutPollingJob> _logger;

        public RefundPayoutPollingJob(IRefundService refundService, ILogger<RefundPayoutPollingJob> logger)
        {
            _refundService = refundService;
            _logger = logger;
        }

        public async Task Execute()
        {
            var updated = await _refundService.PollPendingPayoutsAsync();
            if (updated > 0)
                _logger.LogInformation("Updated {Count} refund payout(s).", updated);
        }
    }
}
