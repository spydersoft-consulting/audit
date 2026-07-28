using MongoDB.Driver;
using Spydersoft.Audit;
using Spydersoft.AuditProcessor;
using Spydersoft.Messaging;
using Spydersoft.Messaging.RabbitMQ;
using Spydersoft.Platform.Hosting.StartupExtensions;
using Spydersoft.Platform.Hosting.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddSpydersoftTelemetry(typeof(Program).Assembly,
    new ConfigurationFunctions
    {
        // Kubernetes probes hit these every few seconds; they add nothing but noise to traces.
        AspNetFilterFunction = context => !IsHealthCheckPath(context.Request.Path.Value)
    })
       .AddSpydersoftSerilog();

var healthCheckOptions = builder.AddSpydersoftHealthChecks();

// Messaging — RabbitMQ transport + AuditRecord consumer bound to "audit.events".
builder.Services
    .AddSpydersoftMessaging()
    .AddSpydersoftRabbitMq(builder.Configuration)
    .AddConsumer<AuditRecord, AuditRecordHandler>(
        topic: "audit.events",
        queueName: "audit-processor");

// MongoDB — single shared client + collection registered for the handler.
var mongoConnection = builder.Configuration.GetConnectionString("audit-mongo")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:audit-mongo is required (e.g. mongodb://user:pass@mongodb/audit)");
var mongoUrl = new MongoUrl(mongoConnection);
var databaseName = string.IsNullOrEmpty(mongoUrl.DatabaseName) ? "audit" : mongoUrl.DatabaseName;

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoUrl));
builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
});
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<AuditEventDocument>("events"));

// Index creation runs at startup before the consumer begins processing.
builder.Services.AddHostedService<AuditIndexInitializer>();

var app = builder.Build();

app.UseSpydersoftHealthChecks(healthCheckOptions);

await app.RunAsync();

static bool IsHealthCheckPath(string? path) =>
    string.Equals(path, "/livez", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(path, "/readyz", StringComparison.OrdinalIgnoreCase);
