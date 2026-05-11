using Audit.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spydersoft.Audit;

namespace Spydersoft.Audit.UnitTests.MessagingAuditDataProviderTests;

internal class GenericMappingTests
{
    private IAuditEventEmitter _emitter = null!;
    private MessagingAuditDataProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _emitter = Substitute.For<IAuditEventEmitter>();
        var options = Options.Create(new AuditOptions { Source = "test-app" });
        _provider = new MessagingAuditDataProvider(
            _emitter,
            options,
            NullLogger<MessagingAuditDataProvider>.Instance);
    }

    [Test]
    public void Map_NonEfEvent_ProducesSingleRecord()
    {
        var auditEvent = new AuditEvent
        {
            EventType = "OrderShipped",
            StartDate = DateTimeOffset.UtcNow.UtcDateTime,
            Environment = new AuditEventEnvironment { UserName = "alice" },
            CustomFields = new Dictionary<string, object> { ["OrderId"] = 42, ["Carrier"] = "UPS" },
        };

        var records = _provider.Map(auditEvent).ToList();

        Assert.That(records, Has.Count.EqualTo(1));
        var rec = records[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rec.Source, Is.EqualTo("test-app"));
            Assert.That(rec.EventType, Is.EqualTo("OrderShipped"));
            Assert.That(rec.EntityType, Is.EqualTo("OrderShipped"));
            Assert.That(rec.Action, Is.EqualTo("Custom"));
            Assert.That(rec.UserId, Is.EqualTo("alice"));
            Assert.That(rec.OldValues, Is.Null);
            Assert.That(rec.EntityId, Is.Null);
            Assert.That(rec.NewValues, Does.Contain("OrderId"));
            Assert.That(rec.NewValues, Does.Contain("Carrier"));
        }
    }

    [Test]
    public async Task InsertEventAsync_EmitsMappedRecords_ReturnsGeneratedGuid()
    {
        var auditEvent = new AuditEvent
        {
            EventType = "Test",
            StartDate = DateTimeOffset.UtcNow.UtcDateTime,
        };

        var result = await _provider.InsertEventAsync(auditEvent, CancellationToken.None);

        Assert.That(Guid.TryParse(result.ToString(), out _), Is.True);
        await _emitter.Received(1).EmitAsync(
            Arg.Is<IEnumerable<AuditRecord>>(rs => rs.Count() == 1),
            Arg.Any<CancellationToken>());
    }
}
