using Spydersoft.AuditApi.Services;
using Spydersoft.AuditApi.Storage;

namespace Spydersoft.AuditApi.UnitTests.AuditQueryServiceTests;

internal class ToViewTests
{
    [Test]
    public void ToView_MapsAllFields()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var doc = new AuditEventDocument
        {
            Id = "abc123",
            Source = "pitstop",
            EventType = "EFCore",
            EntityType = "FillUp",
            EntityId = """{"Id":42}""",
            Action = "Insert",
            UserId = "matt",
            OccurredAt = occurredAt,
            OldValues = null,
            NewValues = """{"Gallons":13.0}""",
            CorrelationId = "trace-123",
            MessageId = "msg-456",
        };

        var view = AuditQueryService.ToView(doc);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo("abc123"));
            Assert.That(view.Source, Is.EqualTo("pitstop"));
            Assert.That(view.EventType, Is.EqualTo("EFCore"));
            Assert.That(view.EntityType, Is.EqualTo("FillUp"));
            Assert.That(view.EntityId, Is.EqualTo("""{"Id":42}"""));
            Assert.That(view.Action, Is.EqualTo("Insert"));
            Assert.That(view.UserId, Is.EqualTo("matt"));
            Assert.That(view.OccurredAt, Is.EqualTo(occurredAt));
            Assert.That(view.OldValues, Is.Null);
            Assert.That(view.NewValues, Is.EqualTo("""{"Gallons":13.0}"""));
            Assert.That(view.CorrelationId, Is.EqualTo("trace-123"));
            Assert.That(view.MessageId, Is.EqualTo("msg-456"));
        }
    }

    [Test]
    public void ToView_MinimalDocument_RoundTripsNullableFields()
    {
        var doc = new AuditEventDocument
        {
            Source = "x",
            EventType = "Custom",
            EntityType = "Y",
            Action = "Custom",
            OccurredAt = DateTimeOffset.UtcNow,
        };

        var view = AuditQueryService.ToView(doc);

        Assert.That(view.EntityId, Is.Null);
        Assert.That(view.UserId, Is.Null);
        Assert.That(view.OldValues, Is.Null);
        Assert.That(view.NewValues, Is.Null);
        Assert.That(view.CorrelationId, Is.Null);
        Assert.That(view.MessageId, Is.Null);
    }
}
