using System.Text.Json;
using Audit.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Spydersoft.Audit;

/// <summary>
/// Audit.NET data provider that maps Audit.NET <see cref="AuditEvent"/>s to
/// <see cref="AuditRecord"/>s and emits them via the registered
/// <see cref="IAuditEventEmitter"/>.
/// </summary>
/// <remarks>
/// <para>
/// Transport-agnostic — does not reference RabbitMQ or any other broker; the actual
/// message dispatch happens in the emitter. Tests substitute the emitter to assert
/// the mapping logic without involving any infrastructure.
/// </para>
/// <para>
/// EF Core events produce one <see cref="AuditRecord"/> per <c>EventEntry</c> so each
/// entity change is independently queryable. Non-EF events produce a single record.
/// </para>
/// <para>
/// EF Core data is read out of <c>AuditEvent.CustomFields["EntityFrameworkEvent"]</c>
/// via <c>dynamic</c>, avoiding a compile-time dependency on
/// <c>Audit.EntityFramework.Core</c> — consumers that don't use EF Core do not pull
/// it in transitively.
/// </para>
/// </remarks>
public sealed class MessagingAuditDataProvider : AuditDataProvider
{
    private const string EntityFrameworkEventKey = "EntityFrameworkEvent";

    private readonly IAuditEventEmitter _emitter;
    private readonly AuditOptions _options;
    private readonly ILogger<MessagingAuditDataProvider> _logger;

    public MessagingAuditDataProvider(
        IAuditEventEmitter emitter,
        IOptions<AuditOptions> options,
        ILogger<MessagingAuditDataProvider> logger)
    {
        _emitter = emitter;
        _options = options.Value;
        _logger = logger;
    }

    public override object InsertEvent(AuditEvent auditEvent)
        => InsertEventAsync(auditEvent, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<object> InsertEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var records = Map(auditEvent);
        await _emitter.EmitAsync(records, cancellationToken);
        // Audit.NET's contract: the return value becomes the persisted event id.
        // We don't track persisted records on this side — RabbitMQ message ids and
        // the AuditApi's _id provide the authoritative identifiers.
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Maps a single Audit.NET <see cref="AuditEvent"/> to one or more
    /// <see cref="AuditRecord"/>s. EF Core events produce one record per entity entry;
    /// all others produce a single record.
    /// </summary>
    internal IEnumerable<AuditRecord> Map(AuditEvent auditEvent)
    {
        var efEvent = TryGetEntityFrameworkEvent(auditEvent);
        return efEvent is not null
            ? MapEntityFrameworkEvent(auditEvent, efEvent)
            : new[] { MapGenericEvent(auditEvent) };
    }

    private object? TryGetEntityFrameworkEvent(AuditEvent auditEvent)
    {
        if (auditEvent.CustomFields is null) return null;
        return auditEvent.CustomFields.TryGetValue(EntityFrameworkEventKey, out var ef) ? ef : null;
    }

    private IEnumerable<AuditRecord> MapEntityFrameworkEvent(AuditEvent auditEvent, object efEvent)
    {
        // Access EntityFrameworkEvent.Entries via dynamic to avoid a compile-time dep
        // on Audit.EntityFramework.Core. Each entry exposes:
        //   string Table or string EntityType  (we read EntityType, falling back to Table)
        //   string Action                      ("Insert" / "Update" / "Delete")
        //   IDictionary<string,object> PrimaryKey
        //   IDictionary<string,object> ColumnValues
        //   ICollection<EventEntryChange> Changes  (only on Update)
        //
        // If the shape changes in a future Audit.NET major, the dynamic dispatch will
        // throw and the unit tests will catch it before this lands.
        dynamic dynEf = efEvent;

        var entries = dynEf.Entries as System.Collections.IEnumerable;
        if (entries is null) yield break;

        foreach (var entry in entries)
        {
            yield return MapEntityFrameworkEntry(auditEvent, entry);
        }
    }

    private AuditRecord MapEntityFrameworkEntry(AuditEvent auditEvent, dynamic entry)
    {
        string entityType = TryGetString(() => entry.EntityType) ?? TryGetString(() => entry.Table) ?? "Unknown";
        string action = TryGetString(() => entry.Action) ?? "Unknown";

        // Primary key: dictionary -> JSON string. Empty dictionary -> null.
        string? entityId = null;
        try
        {
            var pk = entry.PrimaryKey as System.Collections.IDictionary;
            if (pk is not null && pk.Count > 0)
            {
                entityId = JsonSerializer.Serialize(pk);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to serialize PrimaryKey for {EntityType}", entityType);
        }

        // Column values: dictionary -> JSON string, or null on Delete.
        string? newValues = null;
        if (!string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var cv = entry.ColumnValues as System.Collections.IDictionary;
                if (cv is not null)
                {
                    newValues = JsonSerializer.Serialize(cv);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to serialize ColumnValues for {EntityType}", entityType);
            }
        }

        // Original values: only present on Update via Changes collection.
        string? oldValues = null;
        if (string.Equals(action, "Update", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                oldValues = SerializeChanges(entry);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to serialize Changes for {EntityType}", entityType);
            }
        }

        return new AuditRecord
        {
            Source = _options.Source,
            EventType = "EFCore",
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = auditEvent.Environment?.UserName,
            OccurredAt = auditEvent.StartDate,
            OldValues = oldValues,
            NewValues = newValues,
        };
    }

    private static string? SerializeChanges(dynamic entry)
    {
        var changes = entry.Changes as System.Collections.IEnumerable;
        if (changes is null) return null;

        var dict = new Dictionary<string, object?>();
        foreach (var change in changes)
        {
            dynamic d = change;
            string col = d.ColumnName;
            object? original = d.OriginalValue;
            dict[col] = original;
        }
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
    }

    private AuditRecord MapGenericEvent(AuditEvent auditEvent)
    {
        string newValues;
        try
        {
            newValues = auditEvent.CustomFields is { Count: > 0 }
                ? JsonSerializer.Serialize(auditEvent.CustomFields)
                : "{}";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to serialize CustomFields for non-EF audit event");
            newValues = "{}";
        }

        return new AuditRecord
        {
            Source = _options.Source,
            EventType = auditEvent.EventType ?? "Unknown",
            EntityType = auditEvent.EventType ?? "Unknown",
            EntityId = null,
            Action = "Custom",
            UserId = auditEvent.Environment?.UserName,
            OccurredAt = auditEvent.StartDate,
            OldValues = null,
            NewValues = newValues,
        };
    }

    private static string? TryGetString(Func<object?> accessor)
    {
        try { return accessor()?.ToString(); }
        catch { return null; }
    }
}
