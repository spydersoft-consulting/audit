namespace Spydersoft.Audit;

/// <summary>
/// The primary mockable surface for publishing audit events. Sits between Audit.NET's
/// data provider and the underlying transport (Spydersoft.Messaging) so tests can mock
/// at the audit-domain level instead of the transport level.
/// </summary>
public interface IAuditEventEmitter
{
    /// <summary>
    /// Publishes one or more <see cref="AuditRecord"/>s on the configured audit topic.
    /// Implementations should never throw on transport failures during normal operation —
    /// audit emission must not break the user-facing operation that produced the events.
    /// </summary>
    Task EmitAsync(IEnumerable<AuditRecord> records, CancellationToken cancellationToken = default);
}
