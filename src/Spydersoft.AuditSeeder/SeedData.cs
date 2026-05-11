using MongoDB.Bson;
using MongoDB.Driver;

namespace Spydersoft.AuditSeeder;

/// <summary>
/// Seeds the MongoDB <c>audit.events</c> collection with a small set of canned
/// records for tests. Idempotent — re-runs are no-ops because each record carries
/// a deterministic <c>_id</c>.
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(IMongoCollection<BsonDocument> collection)
    {
        var seed = new[]
        {
            BuildDoc(
                id: "650000000000000000000001",
                source: "pitstop",
                entityType: "Vehicle",
                entityId: """{"Id":1}""",
                action: "Insert",
                userId: "seeder-test-user",
                newValues: """{"Make":"Ford","Model":"Bronco","Year":2024}"""),
            BuildDoc(
                id: "650000000000000000000002",
                source: "pitstop",
                entityType: "FillUp",
                entityId: """{"Id":1}""",
                action: "Insert",
                userId: "seeder-test-user",
                newValues: """{"Gallons":13.0,"OdometerReading":1000}"""),
            BuildDoc(
                id: "650000000000000000000003",
                source: "pitstop",
                entityType: "FillUp",
                entityId: """{"Id":1}""",
                action: "Update",
                userId: "seeder-test-user",
                oldValues: """{"OdometerReading":1000}""",
                newValues: """{"OdometerReading":1100}"""),
        };

        foreach (var doc in seed)
        {
            // Upsert by _id so re-running the seeder is harmless.
            var id = doc["_id"];
            await collection.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                doc,
                new ReplaceOptions { IsUpsert = true });
        }
    }

    private static BsonDocument BuildDoc(
        string id,
        string source,
        string entityType,
        string entityId,
        string action,
        string userId,
        string? newValues = null,
        string? oldValues = null) => new()
    {
        ["_id"] = ObjectId.Parse(id),
        ["Source"] = source,
        ["EventType"] = "EFCore",
        ["EntityType"] = entityType,
        ["EntityId"] = entityId,
        ["Action"] = action,
        ["UserId"] = userId,
        ["OccurredAt"] = DateTime.UtcNow,
        ["OldValues"] = oldValues is null ? BsonNull.Value : oldValues,
        ["NewValues"] = newValues is null ? BsonNull.Value : newValues,
        ["CorrelationId"] = BsonNull.Value,
        ["MessageId"] = id,
    };
}
