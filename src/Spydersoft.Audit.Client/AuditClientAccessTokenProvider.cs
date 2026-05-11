using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Spydersoft.Audit.Client;

/// <summary>
/// Kiota <see cref="IAccessTokenProvider"/> that performs an OAuth2
/// <c>client_credentials</c> exchange against the configured token endpoint and
/// caches the resulting bearer token in-memory until shortly before its expiry.
/// </summary>
/// <remarks>
/// <para>
/// The token endpoint is optional — when <see cref="AuditClientOptions.TokenEndpoint"/>
/// is null/empty, this provider returns an empty access token, which Kiota's
/// <c>BaseBearerTokenAuthenticationProvider</c> treats as "no auth header".
/// That suits scenarios where the caller already populates the request via
/// another mechanism, or in tests.
/// </para>
/// <para>
/// User-context flows (forwarding an inbound JWT from <c>HttpContext.User</c>)
/// are documented in the spec but not implemented here — adequate for v1 since
/// every consumer is server-to-server.
/// </para>
/// </remarks>
public sealed class AuditClientAccessTokenProvider : IAccessTokenProvider
{
    private readonly AuditClientOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuditClientAccessTokenProvider> _logger;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;

    public AuditClientAccessTokenProvider(
        IOptions<AuditClientOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AuditClientAccessTokenProvider> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Allows any host — the AuditApi is single-host and we don't speculatively
    /// attach tokens to other URLs.
    /// </summary>
    public AllowedHostsValidator AllowedHostsValidator { get; } = new();

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.TokenEndpoint))
        {
            return string.Empty;
        }

        if (_cachedToken is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
            {
                return _cachedToken;
            }

            using var http = _httpClientFactory.CreateClient();
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _options.ClientId ?? string.Empty),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret ?? string.Empty),
                new KeyValuePair<string, string>("scope", _options.Scope),
            });

            var response = await http.PostAsync(_options.TokenEndpoint, form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to acquire audit-client token: {Status}",
                    response.StatusCode);
                return string.Empty;
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
            if (token is null || string.IsNullOrEmpty(token.AccessToken))
            {
                _logger.LogWarning("Token endpoint returned an empty token");
                return string.Empty;
            }

            _cachedToken = token.AccessToken;
            // Refresh 30 seconds before actual expiry to avoid using a token that
            // expires mid-request.
            _cacheExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(token.ExpiresIn - 30, 30));
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType);
}
