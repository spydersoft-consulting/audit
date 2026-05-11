using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spydersoft.Audit.Client;
using Spydersoft.AuditApi.Services;

namespace Spydersoft.AuditApi.Controllers;

/// <summary>
/// Read-only HTTP surface over the audit log. All endpoints require the
/// <c>audit:read</c> scope.
/// </summary>
[ApiController]
[Route("api/audits")]
[Authorize(Policy = AuthorizationPolicies.Read)]
[Produces("application/json")]
[Tags("Audits")]
public sealed class AuditsController(AuditQueryService service) : ControllerBase
{
    /// <summary>
    /// Search the audit log with optional filters. Newest records first; pagination
    /// applied via <c>Skip</c> + <c>Limit</c> on the query.
    /// </summary>
    /// <param name="query">Filter and pagination parameters bound from the query string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet(Name = nameof(SearchAudits))]
    [ProducesResponseType(typeof(AuditPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditPage>> SearchAudits(
        [FromQuery] AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await service.SearchAsync(query, cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// Returns the distinct list of <c>Source</c> values currently in the audit
    /// index. Useful for populating UI filter dropdowns.
    /// </summary>
    [HttpGet("sources", Name = nameof(GetSources))]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetSources(CancellationToken cancellationToken = default)
    {
        var sources = await service.GetSourcesAsync(cancellationToken);
        return Ok(sources);
    }

    /// <summary>
    /// Returns all audit records for the given entity, newest first. Server-side
    /// limit applies (default 500). For unbounded scans, page via the search endpoint.
    /// </summary>
    /// <param name="entityType">The entity type (e.g. <c>FillUp</c>).</param>
    /// <param name="entityId">The entity id (typically a JSON-serialised primary key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("by-entity/{entityType}/{entityId}", Name = nameof(GetByEntity))]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEventView>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AuditEventView>>> GetByEntity(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var items = await service.GetByEntityAsync(entityType, entityId, cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Fetches a single audit record by its server-assigned id.
    /// </summary>
    /// <param name="id">The record's <c>_id</c> (24-character hex ObjectId).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{id}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(AuditEventView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditEventView>> GetById(
        string id,
        CancellationToken cancellationToken = default)
    {
        var view = await service.GetByIdAsync(id, cancellationToken);
        return view is null ? NotFound() : Ok(view);
    }
}
