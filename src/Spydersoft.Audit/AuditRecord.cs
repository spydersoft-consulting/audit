namespace Spydersoft.Audit;

/// <summary>
/// The over-the-wire audit contract published by consuming applications and consumed
/// by the AuditProcessor. Plain serializable DTO — no Audit.NET, MongoDB, or other
/// platform-specific types.
/// </summary>
public sealed class AuditRecord
{
    /// <summary>
    /// Identifies the publishing application. Set from <see cref="AuditOptions.Source"/>.
    /// Examples: "pitstop", "techradar", "identity".
    /// </summary>
    public string Source { get; init; } = default!;

    /// <summary>
    /// Audit.NET's <c>EventType</c> string (e.g. "EFCore", "Action", "Custom").
    /// </summary>
    public string EventType { get; init; } = default!;

    /// <summary>
    /// The entity or table name being audited.
    /// </summary>
    public string EntityType { get; init; } = default!;

    /// <summary>
    /// Primary key value(s) serialized as a JSON string. Null for non-entity events.
    /// </summary>
    public string? EntityId { get; init; }

    /// <summary>
    /// "Insert", "Update", "Delete", or "Custom".
    /// </summary>
    public string Action { get; init; } = default!;

    /// <summary>
    /// Identity of the user who triggered the change, if available.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// When the audited operation occurred (Audit.NET's <c>StartDate</c>).
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Serialized JSON of the entity state before the change. Null for inserts.
    /// </summary>
    public string? OldValues { get; init; }

    /// <summary>
    /// Serialized JSON of the entity state after the change. Null for deletes.
    /// </summary>
    public string? NewValues { get; init; }
}
