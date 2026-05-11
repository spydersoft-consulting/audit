using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Spydersoft.AuditApi.Services;

namespace Spydersoft.AuditApi.Infrastructure;

/// <summary>
/// Standalone OpenAPI document exporter. Spins up a minimal WebApplication —
/// controllers + AddOpenApi only, no telemetry/auth/Mongo — binds to a random
/// loopback port, fetches <c>/openapi/v1.json</c> in-process, writes it to disk,
/// and shuts down. Used by <c>scripts/regen-openapi.ps1</c> to refresh the
/// committed snapshot at <c>src/Spydersoft.AuditApi/openapi/audit-api-v1.json</c>
/// before Kiota regenerates the client.
/// </summary>
internal static class OpenApiExporter
{
    // Bind to any free loopback port — the address is observed via IServerAddressesFeature
    // immediately after StartAsync to issue an in-process HTTP request for the spec.
    private const string LoopbackBindAddress = "http://127.0.0.1:0";

    public static async Task<int> ExportAsync(string outputPath)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(LoopbackBindAddress);

        // AuditQueryService is a controller dependency — register a stub so
        // controller activation doesn't fail if the OpenAPI generator pokes at it.
        // The stub never executes; OpenAPI generation only enumerates metadata.
        builder.Services.AddSingleton<AuditQueryService>(_ =>
            throw new InvalidOperationException(
                "AuditQueryService should not be activated during OpenAPI export."));

        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(apm =>
            {
                // Same Testing-only filter logic as in production startup, but in
                // export mode we always want the production view of the spec
                // (TestSeedController excluded), so pass false.
                var defaults = apm.FeatureProviders
                    .OfType<ControllerFeatureProvider>()
                    .ToList();
                foreach (var p in defaults)
                {
                    apm.FeatureProviders.Remove(p);
                }
                apm.FeatureProviders.Add(new TestingOnlyControllerProvider(isTestingEnvironment: false));
            });

        builder.Services.AddSpydersoftAuditOpenApi();

        var app = builder.Build();
        app.MapOpenApi();
        app.MapControllers();

        await app.StartAsync();

        try
        {
            var addresses = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses
                ?? throw new InvalidOperationException("Could not resolve listening address.");
            var baseUrl = addresses.First();

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var response = await http.GetAsync("/openapi/v1.json");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(outputPath, json);

            Console.WriteLine($"OpenAPI document written to {outputPath} ({json.Length:N0} bytes)");
            return 0;
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
