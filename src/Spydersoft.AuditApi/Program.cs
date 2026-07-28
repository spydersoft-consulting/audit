using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Spydersoft.AuditApi;
using Spydersoft.AuditApi.Infrastructure;
using Spydersoft.AuditApi.Services;
using Spydersoft.AuditApi.Storage;
using Spydersoft.Platform.Hosting.StartupExtensions;
using Spydersoft.Platform.Hosting.Telemetry;

// Special mode: write the OpenAPI document to disk and exit. Driven by
// scripts/regen-openapi.ps1 to refresh the committed snapshot.
if (args.Length >= 2 && args[0] == "--export-openapi")
{
    return await OpenApiExporter.ExportAsync(args[1]);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddSpydersoftTelemetry(typeof(Program).Assembly,
    new ConfigurationFunctions
    {
        // Kubernetes probes hit these every few seconds; they add nothing but noise to traces.
        AspNetFilterFunction = context => !IsHealthCheckPath(context.Request.Path.Value)
    })
       .AddSpydersoftSerilog();

var healthCheckOptions = builder.AddSpydersoftHealthChecks();

// Auth — JWT bearer against the configured IdentityServer authority. In Testing
// mode, validate against a symmetric test key shared with Spydersoft.AuditSeeder
// so Playwright tests can mint their own tokens without a real IdP.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (builder.Environment.IsEnvironment("Testing"))
        {
            var testKey = builder.Configuration["Auth:TestKey"]!;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(testKey)),
            };
        }
        else
        {
            options.Authority = builder.Configuration["Auth:Authority"];
            options.Audience = builder.Configuration["Auth:Audience"];
        }
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.Read,
        p => p.RequireClaim("scope", AuthorizationPolicies.Read));

// MongoDB. Connection-string parsing is deferred into the factory delegates so the
// host can build (e.g. for build-time OpenAPI document generation) even when the
// connection string isn't configured. At runtime, the first request that needs the
// collection will resolve these and throw if the connection string is missing.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(GetMongoUrl(sp.GetRequiredService<IConfiguration>())));
builder.Services.AddSingleton(sp =>
{
    var url = GetMongoUrl(sp.GetRequiredService<IConfiguration>());
    var dbName = string.IsNullOrEmpty(url.DatabaseName) ? "audit" : url.DatabaseName;
    return sp.GetRequiredService<IMongoClient>().GetDatabase(dbName);
});
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<AuditEventDocument>("events"));

static MongoUrl GetMongoUrl(IConfiguration configuration)
{
    var conn = configuration.GetConnectionString("audit-mongo");
    if (string.IsNullOrWhiteSpace(conn))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:audit-mongo is required (a MongoDB connection string).");
    }
    return new MongoUrl(conn);
}

builder.Services.AddSingleton<AuditQueryService>();

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(apm =>
    {
        // Replace the default ControllerFeatureProvider with one that filters out
        // [TestingOnly] controllers when not running in the Testing environment.
        var defaults = apm.FeatureProviders
            .OfType<ControllerFeatureProvider>()
            .ToList();
        foreach (var p in defaults)
        {
            apm.FeatureProviders.Remove(p);
        }
        apm.FeatureProviders.Add(
            new TestingOnlyControllerProvider(builder.Environment.IsEnvironment("Testing")));
    });
builder.Services.AddSpydersoftAuditOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSpydersoftRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseSpydersoftHealthChecks(healthCheckOptions);

await app.RunAsync();
return 0;

static bool IsHealthCheckPath(string? path) =>
    string.Equals(path, "/livez", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(path, "/readyz", StringComparison.OrdinalIgnoreCase);

// Make Program visible to the integration-test fixture.
public partial class Program { }
