using Spydersoft.Audit.Client.Internal.Generated;
using GeneratedModels = Spydersoft.Audit.Client.Internal.Generated.Models;

namespace Spydersoft.Audit.Client;

/// <summary>
/// Default <see cref="IAuditQueryClient"/> implementation. Wraps the
/// Kiota-generated <see cref="AuditApiClient"/> and translates between Kiota's
/// generated model types and the public <see cref="AuditEventView"/> /
/// <see cref="AuditPage"/> records that consumers depend on.
/// </summary>
/// <remarks>
/// <para>
/// Why a wrapper exists at all: the generated client lives under
/// <c>Spydersoft.Audit.Client.Internal.Generated</c> — its API can change every
/// time we regenerate from a new OpenAPI snapshot. Pinning consumer code to
/// <see cref="IAuditQueryClient"/> insulates them from those churns and lets
/// tests substitute a mock without standing up Kiota's plumbing.
/// </para>
/// </remarks>
public sealed class AuditQueryClient : IAuditQueryClient
{
    private readonly AuditApiClient _client;

    public AuditQueryClient(AuditApiClient client)
    {
        _client = client;
    }

    public async Task<AuditPage> SearchAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var raw = await _client.Api.Audits.GetAsync(c =>
        {
            c.QueryParameters.Source = query.Source;
            c.QueryParameters.EntityType = query.EntityType;
            c.QueryParameters.UserId = query.UserId;
            c.QueryParameters.From = query.From;
            c.QueryParameters.To = query.To;
            c.QueryParameters.Skip = query.Skip;
            c.QueryParameters.Limit = query.Limit;
        }, cancellationToken);
        return ToPage(raw);
    }

    public async Task<AuditEventView?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var raw = await _client.Api.Audits[id].GetAsync(cancellationToken: cancellationToken);
        return raw is null ? null : ToView(raw);
    }

    public async Task<IReadOnlyList<AuditEventView>> GetByEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var raw = await _client.Api.Audits.ByEntity[entityType][entityId]
            .GetAsync(cancellationToken: cancellationToken);
        return raw is null
            ? Array.Empty<AuditEventView>()
            : raw.Select(ToView).ToList();
    }

    public async Task<IReadOnlyList<string>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _client.Api.Audits.Sources.GetAsync(cancellationToken: cancellationToken);
        return raw is null
            ? Array.Empty<string>()
            : raw.ToList();
    }

    // ---- Mapping ---------------------------------------------------------

    internal static AuditPage ToPage(GeneratedModels.AuditPage? raw)
    {
        if (raw is null)
        {
            return new AuditPage(Array.Empty<AuditEventView>(), 0, 0, 0);
        }

        var items = raw.Items?.Select(ToView).ToList() ?? new List<AuditEventView>();
        return new AuditPage(
            Items: items,
            Total: raw.Total ?? 0,
            Skip: raw.Skip ?? 0,
            Limit: raw.Limit ?? 0);
    }

    internal static AuditEventView ToView(GeneratedModels.AuditEventView raw) => new(
        Id: raw.Id ?? string.Empty,
        Source: raw.Source ?? string.Empty,
        EventType: raw.EventType ?? string.Empty,
        EntityType: raw.EntityType ?? string.Empty,
        EntityId: raw.EntityId,
        Action: raw.Action ?? string.Empty,
        UserId: raw.UserId,
        OccurredAt: raw.OccurredAt ?? DateTimeOffset.MinValue,
        OldValues: raw.OldValues,
        NewValues: raw.NewValues,
        CorrelationId: raw.CorrelationId,
        MessageId: raw.MessageId);
}
