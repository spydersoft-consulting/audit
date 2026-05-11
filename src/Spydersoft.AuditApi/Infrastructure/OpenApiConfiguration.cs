using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Spydersoft.AuditApi.Infrastructure;

/// <summary>
/// Centralised <see cref="OpenApiOptions"/> configuration shared between the
/// production startup (<c>Program.cs</c>) and the standalone exporter
/// (<see cref="OpenApiExporter"/>). Anything that affects the generated document
/// — title, version, server list, security schemes — lives here so the snapshot
/// committed alongside the API matches what consumers see at runtime.
/// </summary>
internal static class OpenApiConfiguration
{
    private const string BearerSchemeName = "bearerAuth";

    public static IServiceCollection AddSpydersoftAuditOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            // .NET 10's OpenAPI emits integer parameters/properties as `["integer", "string"]`
            // with a numeric pattern, which Kiota can't handle and falls back to `string`.
            // Normalise back to plain `integer` so the generated client gets a proper int.
            options.AddSchemaTransformer((schema, _, _) =>
            {
                if (schema.Type is not null
                    && schema.Type.Value.HasFlag(JsonSchemaType.Integer)
                    && schema.Type.Value.HasFlag(JsonSchemaType.String))
                {
                    schema.Type = JsonSchemaType.Integer;
                    schema.Pattern = null;
                }
                return Task.CompletedTask;
            });

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Spydersoft Audit API",
                    Version = "v1",
                    Description =
                        "Read-only HTTP API over the Spydersoft audit log. Records are " +
                        "published by consumer apps (e.g. PitStop) via Spydersoft.Audit and " +
                        "persisted to MongoDB by Spydersoft.AuditProcessor.",
                };

                // Clear the auto-discovered server list. Kiota infers the base URL
                // from the consuming application's configuration at runtime; baking
                // the dev/random-port URL into the snapshot would mislead generators.
                document.Servers = [];

                // Bearer security scheme. Every endpoint requires audit:read.
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT bearer token issued by the configured IdentityServer with the audit:read scope.",
                };

                document.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = [],
                    },
                ];

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
