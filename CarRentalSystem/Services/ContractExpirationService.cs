using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.Services
{
    /// <summary>
    /// Background service that automatically expires contracts 
    /// that haven't been signed within 7 days.
    /// Runs every hour to check for expired contracts.
    /// Does NOT revert Driver.IsApproved or User.IsVerified.
    /// </summary>
    public class ContractExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContractExpirationService> _logger;

        public ContractExpirationService(IServiceProvider serviceProvider, ILogger<ContractExpirationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ContractExpirationService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckExpiredContracts();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking expired contracts.");
                }

                // Run every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task CheckExpiredContracts()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.Now;

            // ═══ Expire Driver Contracts ═══
            var expiredDriverContracts = await db.DriverContracts
                .Where(dc => dc.Status == "Pending" && dc.ExpiresAt < now)
                .ToListAsync();

            foreach (var contract in expiredDriverContracts)
            {
                contract.Status = "Expired";
                _logger.LogInformation("Driver contract #{ContractNo} expired (not signed within 7 days).", contract.ContractNo);
            }

            // ═══ Expire Partner Contracts ═══
            var expiredPartnerContracts = await db.PartnerContracts
                .Where(pc => pc.Status == "Pending" && pc.ExpiresAt < now)
                .ToListAsync();

            foreach (var contract in expiredPartnerContracts)
            {
                contract.Status = "Expired";
                _logger.LogInformation("Partner contract #{ContractNo} expired (not signed within 7 days).", contract.ContractNo);
            }

            if (expiredDriverContracts.Count > 0 || expiredPartnerContracts.Count > 0)
            {
                await db.SaveChangesAsync();
                _logger.LogInformation("Expired {DriverCount} driver contracts and {PartnerCount} partner contracts.",
                    expiredDriverContracts.Count, expiredPartnerContracts.Count);
            }
        }
    }
}
