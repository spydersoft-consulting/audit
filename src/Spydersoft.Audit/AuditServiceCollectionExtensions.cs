using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Spydersoft.Audit;

/// <summary>
/// DI registration helpers for the Spydersoft.Audit publish side.
/// </summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers Spydersoft.Audit services and binds <see cref="AuditOptions"/> from
    /// configuration. Must be called after a registration that provides
    /// <see cref="Spydersoft.Messaging.IMessagePublisher"/> (e.g.
    /// <c>AddSpydersoftRabbitMq</c>).
    /// </summary>
    /// <remarks>
    /// After <c>WebApplication</c>/<c>IHost</c> is built, the caller wires
    /// <see cref="MessagingAuditDataProvider"/> into Audit.NET's static configuration:
    /// <code>
    /// var provider = app.Services.GetRequiredService&lt;MessagingAuditDataProvider&gt;();
    /// Audit.Core.Configuration.Setup().UseCustomProvider(provider);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddSpydersoftAudit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AuditOptions>()
            .Bind(configuration.GetSection(AuditOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Source),
                "AuditOptions.Source is required.")
            .ValidateOnStart();

        services.AddSingleton<IAuditEventEmitter, MessagingAuditEventEmitter>();
        services.AddSingleton<MessagingAuditDataProvider>();
        return services;
    }
}
