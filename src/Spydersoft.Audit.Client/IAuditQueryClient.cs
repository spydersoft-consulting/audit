namespace Spydersoft.Audit.Client;

/// <summary>
/// Typed query interface over the Spydersoft Audit API. The full mockable surface for
/// consumer apps that read audit history. Production implementation
/// (<see cref="AuditQueryClient"/>) is registered via
/// <c>AddSpydersoftAuditClient</c>; tests substitute a fake.
/// </summary>
public interface IAuditQueryClient
{
    /// <summary>
    /// Searches the audit log with optional filters. Returns a single page of results.
    /// </summary>
    Task<AuditPage> SearchAsync(AuditQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single record by its server-assigned id. Returns null if missing.
    /// </summary>
    Task<AuditEventView?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all records for a given entity, newest first. Server-side limit applies
    /// (default 500) — for unbounded scans, page via <see cref="SearchAsync"/>.
    /// </summary>
    Task<IReadOnlyList<AuditEventView>> GetByEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct list of <c>Source</c> values currently in the audit index — useful
    /// for populating UI filter dropdowns.
    /// </summary>
    Task<IReadOnlyList<string>> GetSourcesAsync(CancellationToken cancellationToken = default);
}
