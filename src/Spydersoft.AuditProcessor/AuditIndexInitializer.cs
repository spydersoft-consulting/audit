using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Spydersoft.AuditProcessor;

/// <summary>
/// Creates the default indexes on the audit collection at startup. Idempotent —
/// CreateMany is a no-op for indexes that already exist with matching key specs.
/// </summary>
public sealed class AuditIndexInitializer : IHostedService
{
    private readonly IMongoCollection<AuditEventDocument> _collection;
    private readonly ILogger<AuditIndexInitializer> _logger;

    public AuditIndexInitializer(
        IMongoCollection<AuditEventDocument> collection,
        ILogger<AuditIndexInitializer> logger)
    {
        _collection = collection;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<AuditEventDocument>.IndexKeys;
        var indexes = new[]
        {
            new CreateIndexModel<AuditEventDocument>(keys.Ascending(d => d.Source)),
            new CreateIndexModel<AuditEventDocument>(
                keys.Ascending(d => d.EntityType).Ascending(d => d.EntityId)),
            new CreateIndexModel<AuditEventDocument>(keys.Descending(d => d.OccurredAt)),
            new CreateIndexModel<AuditEventDocument>(keys.Ascending(d => d.UserId)),
        };

        try
        {
            var names = await _collection.Indexes.CreateManyAsync(indexes, cancellationToken);
            _logger.LogInformation("Audit collection indexes verified: {Indexes}", string.Join(", ", names));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit collection indexes");
            // Re-throw — without the indexes the API will be slow at scale and we'd
            // rather fail loud at startup than silently degrade.
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
