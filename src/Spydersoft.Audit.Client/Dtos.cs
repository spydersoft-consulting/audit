namespace Spydersoft.Audit.Client;

/// <summary>
/// Query parameters for <see cref="IAuditQueryClient.SearchAsync"/>.
/// </summary>
public sealed record AuditQuery
{
    public string? Source { get; init; }
    public string? EntityType { get; init; }
    public string? UserId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int Skip { get; init; } = 0;
    public int Limit { get; init; } = 50;
}

/// <summary>
/// A page of audit records returned by <see cref="IAuditQueryClient.SearchAsync"/>.
/// </summary>
public sealed record AuditPage(
    IReadOnlyList<AuditEventView> Items,
    int Total,
    int Skip,
    int Limit);

/// <summary>
/// Wire shape for an audit record returned by the AuditApi. Mirrors the storage
/// document — same fields plus a string id.
/// </summary>
public sealed record AuditEventView(
    string Id,
    string Source,
    string EventType,
    string EntityType,
    string? EntityId,
    string Action,
    string? UserId,
    DateTimeOffset OccurredAt,
    string? OldValues,
    string? NewValues,
    string? CorrelationId,
    string? MessageId);
