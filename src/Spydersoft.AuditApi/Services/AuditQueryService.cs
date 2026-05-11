using MongoDB.Driver;
using Spydersoft.Audit.Client;
using Spydersoft.AuditApi.Storage;

namespace Spydersoft.AuditApi.Services;

/// <summary>
/// Read-side query logic over the audit Mongo collection. Endpoints stay thin —
/// they validate inputs and call into this service. Returns
/// <see cref="Spydersoft.Audit.Client"/> DTOs directly so the wire format and the
/// typed client stay in lockstep.
/// </summary>
public sealed class AuditQueryService
{
    private const int MaxLimit = 500;
    private const int MaxByEntityResults = 500;

    private readonly IMongoCollection<AuditEventDocument> _collection;

    public AuditQueryService(IMongoCollection<AuditEventDocument> collection)
    {
        _collection = collection;
    }

    public async Task<AuditPage> SearchAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var skip = Math.Max(0, query.Skip);
        var limit = Math.Clamp(query.Limit, 1, MaxLimit);

        var totalTask = _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var docsTask = _collection.Find(filter)
            .SortByDescending(d => d.OccurredAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalTask, docsTask);

        return new AuditPage(
            Items: docsTask.Result.Select(ToView).ToList(),
            Total: (int) Math.Min(totalTask.Result, int.MaxValue),
            Skip: skip,
            Limit: limit);
    }

    public async Task<AuditEventView?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
        return doc is null ? null : ToView(doc);
    }

    public async Task<IReadOnlyList<AuditEventView>> GetByEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var docs = await _collection
            .Find(d => d.EntityType == entityType && d.EntityId == entityId)
            .SortByDescending(d => d.OccurredAt)
            .Limit(MaxByEntityResults)
            .ToListAsync(cancellationToken);
        return docs.Select(ToView).ToList();
    }

    public async Task<IReadOnlyList<string>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await _collection
            .Distinct<string>("Source", FilterDefinition<AuditEventDocument>.Empty, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
        return sources;
    }

    private static FilterDefinition<AuditEventDocument> BuildFilter(AuditQuery query)
    {
        var b = Builders<AuditEventDocument>.Filter;
        var filters = new List<FilterDefinition<AuditEventDocument>>();

        if (!string.IsNullOrEmpty(query.Source))     filters.Add(b.Eq(d => d.Source, query.Source));
        if (!string.IsNullOrEmpty(query.EntityType)) filters.Add(b.Eq(d => d.EntityType, query.EntityType));
        if (!string.IsNullOrEmpty(query.UserId))     filters.Add(b.Eq(d => d.UserId, query.UserId));
        if (query.From is { } from)                  filters.Add(b.Gte(d => d.OccurredAt, from));
        if (query.To is { } to)                      filters.Add(b.Lte(d => d.OccurredAt, to));

        return filters.Count == 0 ? FilterDefinition<AuditEventDocument>.Empty : b.And(filters);
    }

    internal static AuditEventView ToView(AuditEventDocument doc) => new(
        Id: doc.Id,
        Source: doc.Source,
        EventType: doc.EventType,
        EntityType: doc.EntityType,
        EntityId: doc.EntityId,
        Action: doc.Action,
        UserId: doc.UserId,
        OccurredAt: doc.OccurredAt,
        OldValues: doc.OldValues,
        NewValues: doc.NewValues,
        CorrelationId: doc.CorrelationId,
        MessageId: doc.MessageId);
}
