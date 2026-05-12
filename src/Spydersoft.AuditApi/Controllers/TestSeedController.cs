using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Spydersoft.AuditApi.Infrastructure;
using Spydersoft.AuditApi.Storage;

namespace Spydersoft.AuditApi.Controllers;

/// <summary>
/// Test-only seed endpoint used by Playwright tests. <strong>Only registered when
/// the host environment is "Testing"</strong> — see
/// <c>Program.cs</c>. Inserts an <see cref="AuditEventDocument"/> directly into
/// MongoDB, bypassing the publish→consume pipeline so read-side tests stay fast and
/// deterministic.
/// </summary>
/// <remarks>
/// Authorisation is intentionally relaxed (the controller has no
/// <c>[Authorize]</c> attribute) so the test fixture can seed without a token.
/// This is acceptable because the controller is only mapped in the Testing
/// environment, never in test/stage/production deployments.
/// </remarks>
[ApiController]
[Route("api/test/audits")]
[TestingOnly]
public sealed class TestSeedController(IMongoCollection<AuditEventDocument> collection) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AuditEventDocument>> Seed(
        [FromBody] SeedRequest request,
        CancellationToken cancellationToken)
    {
        var doc = new AuditEventDocument
        {
            Source = request.Source,
            EventType = request.EventType ?? "EFCore",
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Action = request.Action ?? "Insert",
            UserId = request.UserId,
            OccurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow,
            OldValues = request.OldValues,
            NewValues = request.NewValues,
            CorrelationId = request.CorrelationId,
            MessageId = request.MessageId ?? Guid.NewGuid().ToString(),
        };
        await collection.InsertOneAsync(doc, options: null, cancellationToken: cancellationToken);
        return Created($"/api/audits/{doc.Id}", doc);
    }

    [HttpDelete]
    public async Task<IActionResult> ClearAll(CancellationToken cancellationToken)
    {
        await collection.DeleteManyAsync(FilterDefinition<AuditEventDocument>.Empty, cancellationToken);
        return NoContent();
    }

    public sealed record SeedRequest(
        string Source,
        string EntityType,
        string? EntityId,
        string? UserId,
        string? EventType = null,
        string? Action = null,
        DateTimeOffset? OccurredAt = null,
        string? OldValues = null,
        string? NewValues = null,
        string? CorrelationId = null,
        string? MessageId = null);
}
