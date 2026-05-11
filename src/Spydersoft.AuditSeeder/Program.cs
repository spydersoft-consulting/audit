using MongoDB.Bson;
using MongoDB.Driver;
using Spydersoft.AuditSeeder;

var connectionString = args.ElementAtOrDefault(0)
    ?? Environment.GetEnvironmentVariable("AUDIT_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__audit-mongo")
    ?? "mongodb://localhost:27017/audit";

var testKey = Environment.GetEnvironmentVariable("AUDIT_TEST_KEY")
    ?? "jRv3YFPH/19t9t5CgsEFgAkykfW5bQhHmceMprLgzlQ=";

if (args.Contains("--token-only"))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { token = TokenGenerator.Generate(testKey) }));
    return;
}

var url = new MongoUrl(connectionString);
var dbName = string.IsNullOrEmpty(url.DatabaseName) ? "audit" : url.DatabaseName;
var client = new MongoClient(url);
var collection = client.GetDatabase(dbName).GetCollection<BsonDocument>("events");

Console.WriteLine($"Seeding audit data into {dbName}.events...");
await SeedData.SeedAsync(collection);
Console.WriteLine("Seed complete.");

Console.WriteLine();
Console.WriteLine("Test token (set as AUDIT_TEST_TOKEN):");
Console.WriteLine(TokenGenerator.Generate(testKey));
