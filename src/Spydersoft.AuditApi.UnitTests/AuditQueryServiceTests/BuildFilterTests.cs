using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Spydersoft.Audit.Client;
using Spydersoft.AuditApi.Services;
using Spydersoft.AuditApi.Storage;

namespace Spydersoft.AuditApi.UnitTests.AuditQueryServiceTests;

/// <summary>
/// White-box tests for <see cref="AuditQueryService.BuildFilter"/>. The filter is
/// what gates the read-side queries — silently mis-building it would leak
/// records across <c>Source</c> or <c>UserId</c> boundaries, so each clause
/// gets its own assertion against the rendered BSON.
/// </summary>
internal class BuildFilterTests
{
    private static BsonDocument Render(FilterDefinition<AuditEventDocument> filter)
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<AuditEventDocument>();
        return filter.Render(new RenderArgs<AuditEventDocument>(serializer, BsonSerializer.SerializerRegistry));
    }

    [Test]
    public void BuildFilter_EmptyQuery_ReturnsEmptyFilter()
    {
        var filter = AuditQueryService.BuildFilter(new AuditQuery());

        var rendered = Render(filter);

        // Empty filter renders as {} — no constraints, every doc matches.
        Assert.That(rendered.ElementCount, Is.Zero);
    }

    [Test]
    public void BuildFilter_SourceOnly_AddsSingleSourceClause()
    {
        var filter = AuditQueryService.BuildFilter(new AuditQuery { Source = "pitstop" });

        var rendered = Render(filter);

        // A single-clause filter is unwrapped from $and by the builder.
        Assert.That(rendered.Contains("Source"), Is.True);
        Assert.That(rendered["Source"].AsString, Is.EqualTo("pitstop"));
    }

    [Test]
    public void BuildFilter_EntityTypeOnly_AddsEntityTypeClause()
    {
        var filter = AuditQueryService.BuildFilter(new AuditQuery { EntityType = "FillUp" });

        var rendered = Render(filter);

        Assert.That(rendered["EntityType"].AsString, Is.EqualTo("FillUp"));
    }

    [Test]
    public void BuildFilter_UserIdOnly_AddsUserIdClause()
    {
        var filter = AuditQueryService.BuildFilter(new AuditQuery { UserId = "matt" });

        var rendered = Render(filter);

        Assert.That(rendered["UserId"].AsString, Is.EqualTo("matt"));
    }

    [Test]
    public void BuildFilter_DateRange_AddsGteAndLteOnOccurredAt()
    {
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var filter = AuditQueryService.BuildFilter(new AuditQuery { From = from, To = to });

        var rendered = Render(filter);
        var json = rendered.ToJson();

        // Both bounds present, on the OccurredAt field, via $gte and $lte.
        Assert.That(json, Does.Contain("OccurredAt"));
        Assert.That(json, Does.Contain("$gte"));
        Assert.That(json, Does.Contain("$lte"));
    }

    [Test]
    public void BuildFilter_AllFields_AllClausesPresent()
    {
        var query = new AuditQuery
        {
            Source = "pitstop",
            EntityType = "FillUp",
            UserId = "matt",
            From = DateTimeOffset.UtcNow.AddDays(-7),
            To = DateTimeOffset.UtcNow,
        };

        var filter = AuditQueryService.BuildFilter(query);

        // Mongo's filter renderer flattens non-conflicting equality clauses
        // into the root object rather than wrapping them in $and — so we assert
        // on the rendered string containing each expected field/operator
        // rather than on a specific structural layout.
        var json = Render(filter).ToJson();

        Assert.That(json, Does.Contain("Source"));
        Assert.That(json, Does.Contain("pitstop"));
        Assert.That(json, Does.Contain("EntityType"));
        Assert.That(json, Does.Contain("FillUp"));
        Assert.That(json, Does.Contain("UserId"));
        Assert.That(json, Does.Contain("matt"));
        Assert.That(json, Does.Contain("OccurredAt"));
        Assert.That(json, Does.Contain("$gte"));
        Assert.That(json, Does.Contain("$lte"));
    }

    [Test]
    public void BuildFilter_EmptyStringsAreIgnored()
    {
        // Distinguishing "no filter on this field" from "filter for empty string".
        // The service treats empty strings as "no filter" — important so callers
        // passing `?source=` don't unexpectedly look up documents with an
        // empty Source.
        var filter = AuditQueryService.BuildFilter(new AuditQuery
        {
            Source = "",
            EntityType = "",
            UserId = "",
        });

        var rendered = Render(filter);

        Assert.That(rendered.ElementCount, Is.Zero);
    }
}
