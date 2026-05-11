using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Spydersoft.AuditApi.Storage;

/// <summary>
/// Mongo document shape used by the read API. Mirrors the processor's storage class.
/// Duplicated here intentionally — the API doesn't take a project reference on the
/// processor; the document shape is part of the storage contract.
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
