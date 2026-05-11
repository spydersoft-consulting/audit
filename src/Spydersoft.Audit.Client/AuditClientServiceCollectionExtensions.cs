using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Spydersoft.Audit.Client.Internal.Generated;

namespace Spydersoft.Audit.Client;

/// <summary>
/// DI registration helpers for the Spydersoft.Audit.Client query side.
/// </summary>
public static class AuditClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAuditQueryClient"/> backed by the Kiota-generated
    /// <c>AuditApiClient</c>. The HTTP pipeline uses
    /// <see cref="AuditClientAccessTokenProvider"/> for OAuth2
    /// <c>client_credentials</c> auth when a token endpoint is configured.
    /// </summary>
    public static IServiceCollection AddSpydersoftAuditClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AuditClientOptions>()
            .Bind(configuration.GetSection(AuditClientOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl),
                "AuditClientOptions.BaseUrl is required.")
            .ValidateOnStart();

        services.AddHttpClient();
        services.AddSingleton<IAccessTokenProvider, AuditClientAccessTokenProvider>();
        services.AddSingleton<IAuthenticationProvider>(sp =>
            new BaseBearerTokenAuthenticationProvider(sp.GetRequiredService<IAccessTokenProvider>()));

        services.AddSingleton<IRequestAdapter>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AuditClientOptions>>().Value;
            var adapter = new HttpClientRequestAdapter(sp.GetRequiredService<IAuthenticationProvider>())
            {
                BaseUrl = opts.BaseUrl.TrimEnd('/'),
            };
            return adapter;
        });

        services.AddSingleton<AuditApiClient>(sp =>
            new AuditApiClient(sp.GetRequiredService<IRequestAdapter>()));

        services.AddSingleton<IAuditQueryClient, AuditQueryClient>();

        return services;
    }
}
