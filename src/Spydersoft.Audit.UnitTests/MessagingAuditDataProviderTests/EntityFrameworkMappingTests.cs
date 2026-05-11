using Audit.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spydersoft.Audit;
using Spydersoft.Audit.UnitTests.Fakes;

namespace Spydersoft.Audit.UnitTests.MessagingAuditDataProviderTests;

/// <summary>
/// Verifies the data provider's EF Core path. The provider reads
/// <c>auditEvent.CustomFields["EntityFrameworkEvent"]</c> via <c>dynamic</c>, so we
/// can supply test fakes that expose the same shape (Entries, EntityType, Action,
/// PrimaryKey, ColumnValues, Changes) without depending on Audit.EntityFramework.Core.
/// </summary>
internal class EntityFrameworkMappingTests
{
    private IAuditEventEmitter _emitter = null!;
    private MessagingAuditDataProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _emitter = Substitute.For<IAuditEventEmitter>();
        var options = Options.Create(new AuditOptions { Source = "pitstop" });
        _provider = new MessagingAuditDataProvider(
            _emitter,
            options,
            NullLogger<MessagingAuditDataProvider>.Instance);
    }

    [Test]
    public void Map_EfEvent_OneRecordPerEntry()
    {
        var efEvent = new FakeEntityFrameworkEvent
        {
            Entries = new List<FakeEventEntry>
            {
                FakeEventEntry.Insert("FillUp", primaryKey: 42, values: new() { ["Gallons"] = 13.5m }),
                FakeEventEntry.Insert("Vehicle", primaryKey: 7, values: new() { ["Make"] = "Ford" }),
            },
        };
        var auditEvent = new AuditEvent
        {
            EventType = "EFCore",
            StartDate = DateTimeOffset.UtcNow.UtcDateTime,
            Environment = new AuditEventEnvironment { UserName = "matt" },
            CustomFields = new Dictionary<string, object> { ["EntityFrameworkEvent"] = efEvent },
        };

        var records = _provider.Map(auditEvent).ToList();

        Assert.That(records, Has.Count.EqualTo(2));
        Assert.That(records[0].EntityType, Is.EqualTo("FillUp"));
        Assert.That(records[1].EntityType, Is.EqualTo("Vehicle"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(records.All(r => r.Source == "pitstop"));
            Assert.That(records.All(r => r.EventType == "EFCore"));
            Assert.That(records.All(r => r.UserId == "matt"));
            Assert.That(records.All(r => r.Action == "Insert"));
        }
    }

    [Test]
    public void Map_InsertEntry_HasNewValues_NoOldValues()
    {
        var efEvent = new FakeEntityFrameworkEvent
        {
            Entries = new() { FakeEventEntry.Insert("FillUp", 1, new() { ["Gallons"] = 10.0m }) },
        };
        var auditEvent = NewEfAuditEvent(efEvent);

        var rec = _provider.Map(auditEvent).Single();

        Assert.That(rec.OldValues, Is.Null);
        Assert.That(rec.NewValues, Does.Contain("Gallons"));
    }

    [Test]
    public void Map_DeleteEntry_NoNewValues()
    {
        var efEvent = new FakeEntityFrameworkEvent
        {
            Entries = new() { FakeEventEntry.Delete("FillUp", 1) },
        };
        var auditEvent = NewEfAuditEvent(efEvent);

        var rec = _provider.Map(auditEvent).Single();

        Assert.That(rec.NewValues, Is.Null);
        Assert.That(rec.Action, Is.EqualTo("Delete"));
    }

    [Test]
    public void Map_UpdateEntry_HasOldAndNewValues()
    {
        var efEvent = new FakeEntityFrameworkEvent
        {
            Entries = new()
            {
                FakeEventEntry.Update(
                    "FillUp",
                    primaryKey: 1,
                    columnValues: new() { ["Gallons"] = 13.0m },
                    changes: new()
                    {
                        new FakeEventEntryChange { ColumnName = "Gallons", OriginalValue = 12.5m, NewValue = 13.0m },
                    }),
            },
        };
        var auditEvent = NewEfAuditEvent(efEvent);

        var rec = _provider.Map(auditEvent).Single();

        Assert.That(rec.OldValues, Does.Contain("Gallons"));
        Assert.That(rec.OldValues, Does.Contain("12.5"));
        Assert.That(rec.NewValues, Does.Contain("13"));
    }

    [Test]
    public void Map_PrimaryKeySerializedAsJson()
    {
        var efEvent = new FakeEntityFrameworkEvent
        {
            Entries = new() { FakeEventEntry.Insert("FillUp", 42, new() { ["X"] = 1 }) },
        };
        var auditEvent = NewEfAuditEvent(efEvent);

        var rec = _provider.Map(auditEvent).Single();

        Assert.That(rec.EntityId, Is.Not.Null.And.Contain("42"));
    }

    private static AuditEvent NewEfAuditEvent(FakeEntityFrameworkEvent efEvent) => new()
    {
        EventType = "EFCore",
        StartDate = DateTimeOffset.UtcNow.UtcDateTime,
        Environment = new AuditEventEnvironment { UserName = "matt" },
        CustomFields = new Dictionary<string, object> { ["EntityFrameworkEvent"] = efEvent },
    };
}
