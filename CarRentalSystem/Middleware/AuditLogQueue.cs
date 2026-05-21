using System.Threading.Channels;
using CarRentalSystem.Models;
using CarRentalSystem.Services;

namespace CarRentalSystem.Middleware
{
    public interface IAuditLogQueue
    {
        bool TryEnqueue(AuditLog log);
        IAsyncEnumerable<AuditLog> ReadAllAsync(CancellationToken cancellationToken);
    }

    public sealed class BoundedAuditLogQueue : IAuditLogQueue
    {
        private readonly Channel<AuditLog> _channel;

        public BoundedAuditLogQueue(IConfiguration configuration)
        {
            var capacity = configuration.GetValue("Audit:QueueCapacity", 5000);
            _channel = Channel.CreateBounded<AuditLog>(new BoundedChannelOptions(Math.Max(capacity, 100))
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public bool TryEnqueue(AuditLog log)
        {
            return _channel.Writer.TryWrite(log);
        }

        public IAsyncEnumerable<AuditLog> ReadAllAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.ReadAllAsync(cancellationToken);
        }
    }

    public sealed class AuditLogBackgroundWriter : BackgroundService
    {
        private readonly IAuditLogQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditLogBackgroundWriter> _logger;

        public AuditLogBackgroundWriter(
            IAuditLogQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<AuditLogBackgroundWriter> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var log in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var auditTrail = scope.ServiceProvider.GetRequiredService<IAuditTrailService>();
                    await auditTrail.RecordAsync(log, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist queued audit log for {Action} {EntityType}.", log.Action, log.EntityType);
                }
            }
        }
    }
}
