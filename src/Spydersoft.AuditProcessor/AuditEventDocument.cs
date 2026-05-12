using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Spydersoft.AuditProcessor;

/// <summary>
/// MongoDB storage shape for an audit record. Mirrors <see cref="Spydersoft.Audit.AuditRecord"/>
/// plus envelope metadata (CorrelationId, MessageId) for trace-back to the original publish.
/// </summary>
public sealed class AuditEventDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Source { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string? EntityId { get; set; }
    public string Action { get; set; } = default!;
    public string? UserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? CorrelationId { get; set; }
    public string? MessageId { get; set; }
}
