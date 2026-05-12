var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var mongo = builder.AddMongoDB("mongo");
var auditDb = mongo.AddDatabase("audit-mongo", databaseName: "audit");

var dashboardOtlp = builder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"]
    ?? "http://localhost:18889";

var processor = builder.AddProject<Projects.Spydersoft_AuditProcessor>("audit-processor")
    .WithReference(rabbitmq)
    .WithReference(auditDb)
    .WaitFor(rabbitmq)
    .WaitFor(mongo);

var api = builder.AddProject<Projects.Spydersoft_AuditApi>("audit-api")
    .WithReference(auditDb)
    .WaitFor(mongo);

foreach (var (typeKey, endpointKey) in new[]
{
    ("Telemetry__Trace__Type",   "Telemetry__Trace__Otlp__Endpoint"),
    ("Telemetry__Metrics__Type", "Telemetry__Metrics__Otlp__Endpoint"),
    ("Telemetry__Log__Type",     "Telemetry__Log__Otlp__Endpoint"),
})
{
    foreach (var p in new[] { processor, api })
    {
        p.WithEnvironment(typeKey, builder.Configuration[typeKey] ?? "otlp");
        p.WithEnvironment(endpointKey, builder.Configuration[endpointKey] ?? dashboardOtlp);
    }
}

if (builder.Environment.EnvironmentName == "Testing")
{
    var testKey = builder.Configuration["Auth:TestKey"]
        ?? "jRv3YFPH/19t9t5CgsEFgAkykfW5bQhHmceMprLgzlQ=";

    api.WithEnvironment("DOTNET_ENVIRONMENT", "Testing")
       .WithEnvironment("Auth__TestKey", testKey);

    builder.AddProject<Projects.Spydersoft_AuditSeeder>("audit-seeder")
        .WithReference(auditDb)
        .WaitFor(api)
        .WithEnvironment("AUDIT_TEST_KEY", testKey);
}
else
{
    // Local dev mode (Development or default). Auth:Authority points at the
    // deployed test IdP by default; override via user-secrets on this AppHost
    // to disable or redirect.
    api
        .WithEnvironment("Auth__Authority",
            builder.Configuration["Auth:Authority"] ?? "https://auth.mattgerega.net")
        .WithEnvironment("Auth__Audience",
            builder.Configuration["Auth:Audience"] ?? "audit-api");
}

await builder.Build().RunAsync();
