using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Spydersoft.Audit;
using Spydersoft.Messaging;

namespace Spydersoft.AuditProcessor;

/// <summary>
/// <see cref="IMessageHandler{T}"/> implementation that persists each
/// <see cref="AuditRecord"/> envelope as an <see cref="AuditEventDocument"/> in
/// MongoDB. Envelope-level metadata (<c>MessageId</c>, <c>CorrelationId</c>) is
/// preserved for trace-back to the publishing application.
/// </summary>
public sealed class AuditRecordHandler : IMessageHandler<AuditRecord>
{
    private readonly IMongoCollection<AuditEventDocument> _collection;
    private readonly ILogger<AuditRecordHandler> _logger;

    public AuditRecordHandler(
        IMongoCollection<AuditEventDocument> collection,
        ILogger<AuditRecordHandler> logger)
    {
        _collection = collection;
        _logger = logger;
    }

    public async Task HandleAsync(MessageEnvelope<AuditRecord> envelope, CancellationToken cancellationToken = default)
    {
        var doc = Map(envelope);
        await _collection.InsertOneAsync(doc, options: null, cancellationToken: cancellationToken);
        _logger.LogDebug(
            "Persisted audit record {Source}/{EntityType}/{EntityId} action={Action} messageId={MessageId}",
            doc.Source, doc.EntityType, doc.EntityId, doc.Action, doc.MessageId);
    }

    internal static AuditEventDocument Map(MessageEnvelope<AuditRecord> envelope)
    {
        var record = envelope.Payload;
        return new AuditEventDocument
        {
            Source = record.Source,
            EventType = record.EventType,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            Action = record.Action,
            UserId = record.UserId,
            OccurredAt = record.OccurredAt,
            OldValues = record.OldValues,
            NewValues = record.NewValues,
            CorrelationId = envelope.CorrelationId,
            MessageId = envelope.MessageId,
        };
    }
}
