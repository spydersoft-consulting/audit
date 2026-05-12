using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spydersoft.Audit.Client;

namespace Spydersoft.Audit.Client.UnitTests.AuditClientAccessTokenProviderTests;

internal class GetAuthorizationTokenAsyncTests
{
    private static readonly Uri AnyUri = new("https://audit.example.com/api/audits");

    private static AuditClientAccessTokenProvider CreateProvider(
        AuditClientOptions options,
        IHttpClientFactory factory)
    {
        return new AuditClientAccessTokenProvider(
            Options.Create(options),
            factory,
            NullLogger<AuditClientAccessTokenProvider>.Instance);
    }

    [Test]
    public async Task NoTokenEndpoint_ReturnsEmptyString()
    {
        // Empty TokenEndpoint signals "don't fetch a token" — used in scenarios
        // where the caller attaches the token some other way, and in tests.
        var factory = Substitute.For<IHttpClientFactory>();
        var provider = CreateProvider(
            new AuditClientOptions { BaseUrl = "https://audit.example.com", TokenEndpoint = null },
            factory);

        var token = await provider.GetAuthorizationTokenAsync(AnyUri);

        Assert.That(token, Is.Empty);
        factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Test]
    public async Task ValidResponse_ReturnsAccessToken()
    {
        var stub = new CountingHandler(_ => CreateOkTokenResponse("issued-token-abc", expiresIn: 300));
        var factory = SubstituteFactoryReturning(new HttpClient(stub));
        var provider = CreateProvider(NewOptionsWithTokenEndpoint(), factory);

        var token = await provider.GetAuthorizationTokenAsync(AnyUri);

        Assert.That(token, Is.EqualTo("issued-token-abc"));
        Assert.That(stub.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SecondCall_WithinCacheWindow_DoesNotReHitTokenEndpoint()
    {
        // First response sets up a 300-second token; second call should be served
        // from the cache rather than hitting the token endpoint again.
        var stub = new CountingHandler(_ => CreateOkTokenResponse("issued-token-xyz", expiresIn: 300));
        var factory = SubstituteFactoryReturning(new HttpClient(stub));
        var provider = CreateProvider(NewOptionsWithTokenEndpoint(), factory);

        var first = await provider.GetAuthorizationTokenAsync(AnyUri);
        var second = await provider.GetAuthorizationTokenAsync(AnyUri);

        Assert.That(first, Is.EqualTo("issued-token-xyz"));
        Assert.That(second, Is.EqualTo("issued-token-xyz"));
        Assert.That(stub.CallCount, Is.EqualTo(1), "Cached token should prevent a second HTTP call.");
    }

    [Test]
    public async Task NonSuccessStatus_ReturnsEmptyString()
    {
        // The provider deliberately swallows token-endpoint failures so the
        // calling request still goes out (it'll just 401 against the API).
        // That keeps audit-read failures from breaking the consumer's
        // user-facing request.
        var stub = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var factory = SubstituteFactoryReturning(new HttpClient(stub));
        var provider = CreateProvider(NewOptionsWithTokenEndpoint(), factory);

        var token = await provider.GetAuthorizationTokenAsync(AnyUri);

        Assert.That(token, Is.Empty);
    }

    [Test]
    public async Task EmptyAccessTokenInResponse_ReturnsEmptyString()
    {
        // Defensive: if the token endpoint returns a 200 with an empty
        // access_token (misconfigured client, identity provider quirk), we
        // shouldn't accidentally use empty string as a bearer token.
        var stub = new CountingHandler(_ => CreateOkTokenResponse(accessToken: "", expiresIn: 300));
        var factory = SubstituteFactoryReturning(new HttpClient(stub));
        var provider = CreateProvider(NewOptionsWithTokenEndpoint(), factory);

        var token = await provider.GetAuthorizationTokenAsync(AnyUri);

        Assert.That(token, Is.Empty);
    }

    // ---- Helpers ---------------------------------------------------------

    private static AuditClientOptions NewOptionsWithTokenEndpoint() => new()
    {
        BaseUrl = "https://audit.example.com",
        TokenEndpoint = "https://auth.example.com/connect/token",
        ClientId = "test-client",
        ClientSecret = "test-secret",
        Scope = "audit:read",
    };

    private static IHttpClientFactory SubstituteFactoryReturning(HttpClient http)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(http);
        return factory;
    }

    private static HttpResponseMessage CreateOkTokenResponse(string accessToken, int expiresIn) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                access_token = accessToken,
                expires_in = expiresIn,
                token_type = "Bearer",
            }),
        };

    /// <summary>
    /// In-memory <see cref="HttpMessageHandler"/> that records call count and
    /// produces a fresh response per invocation. Lets us assert cache behaviour
    /// without standing up a real HTTP server.
    /// </summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }

        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }
}
