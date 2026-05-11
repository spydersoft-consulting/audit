using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spydersoft.Messaging;

namespace Spydersoft.Audit;

/// <summary>
/// Default <see cref="IAuditEventEmitter"/> implementation. Publishes each
/// <see cref="AuditRecord"/> via the registered <see cref="IMessagePublisher"/>
/// on the topic configured in <see cref="AuditOptions.Topic"/>.
/// </summary>
/// <remarks>
/// Transport failures are logged at warning level and swallowed — emitting an audit
/// record must never break the user-facing operation that produced it.
/// </remarks>
public sealed class MessagingAuditEventEmitter : IAuditEventEmitter
{
    private readonly IMessagePublisher _publisher;
    private readonly AuditOptions _options;
    private readonly ILogger<MessagingAuditEventEmitter> _logger;

    public MessagingAuditEventEmitter(
        IMessagePublisher publisher,
        IOptions<AuditOptions> options,
        ILogger<MessagingAuditEventEmitter> logger)
    {
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EmitAsync(IEnumerable<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            try
            {
                await _publisher.PublishAsync(_options.Topic, record, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to publish audit record for {EntityType}/{EntityId} on {Topic}",
                    record.EntityType,
                    record.EntityId,
                    _options.Topic);
            }
        }
    }
}
