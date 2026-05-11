namespace Spydersoft.Audit.Client;

/// <summary>
/// Configuration for the Spydersoft.Audit.Client HTTP client. Bound from the
/// "AuditClient" configuration section by <c>AddSpydersoftAuditClient</c>.
/// </summary>
public class AuditClientOptions
{
    public const string SectionName = "AuditClient";

    /// <summary>
    /// Base URL of the audit query API. Required.
    /// Examples: <c>https://audit.mattgerega.net</c> for the test environment.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 token endpoint used by the auth handler to acquire a bearer token.
    /// Set to <c>null</c> to use the ambient <see cref="System.Security.Claims.ClaimsPrincipal"/>'s
    /// JWT (browser/SPA-style flows where the inbound token is forwarded).
    /// </summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>
    /// Client id for the client_credentials grant. Only used when
    /// <see cref="TokenEndpoint"/> is set.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client secret for the client_credentials grant. Should come from a secret store,
    /// not <c>appsettings.json</c>.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Scope to request. Defaults to <c>audit:read</c>.
    /// </summary>
    public string Scope { get; set; } = "audit:read";
}
