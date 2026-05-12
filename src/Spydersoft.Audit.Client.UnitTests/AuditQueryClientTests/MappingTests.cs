using Spydersoft.Audit.Client;
using GeneratedModels = Spydersoft.Audit.Client.Internal.Generated.Models;

namespace Spydersoft.Audit.Client.UnitTests.AuditQueryClientTests;

/// <summary>
/// White-box mapping tests for the wrapper layer. Covers the translation from
/// the Kiota-generated nullable model types to our public sealed-record DTOs.
/// </summary>
internal class MappingTests
{
    [Test]
    public void ToView_FullyPopulated_MapsEveryField()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var raw = new GeneratedModels.AuditEventView
        {
            Id = "abc",
            Source = "pitstop",
            EventType = "EFCore",
            EntityType = "FillUp",
            EntityId = """{"Id":42}""",
            Action = "Insert",
            UserId = "matt",
            OccurredAt = occurredAt,
            OldValues = """{"Gallons":12.5}""",
            NewValues = """{"Gallons":13.0}""",
            CorrelationId = "trace-1",
            MessageId = "msg-1",
        };

        var view = AuditQueryClient.ToView(raw);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo("abc"));
            Assert.That(view.Source, Is.EqualTo("pitstop"));
            Assert.That(view.EventType, Is.EqualTo("EFCore"));
            Assert.That(view.EntityType, Is.EqualTo("FillUp"));
            Assert.That(view.EntityId, Is.EqualTo("""{"Id":42}"""));
            Assert.That(view.Action, Is.EqualTo("Insert"));
            Assert.That(view.UserId, Is.EqualTo("matt"));
            Assert.That(view.OccurredAt, Is.EqualTo(occurredAt));
            Assert.That(view.OldValues, Is.EqualTo("""{"Gallons":12.5}"""));
            Assert.That(view.NewValues, Is.EqualTo("""{"Gallons":13.0}"""));
            Assert.That(view.CorrelationId, Is.EqualTo("trace-1"));
            Assert.That(view.MessageId, Is.EqualTo("msg-1"));
        }
    }

    [Test]
    public void ToView_NullStringFields_BecomeEmptyForRequiredAndNullForOptional()
    {
        // Kiota generates everything nullable. Our public record requires Id/Source/etc. to
        // be non-null strings — the wrapper substitutes empty strings rather than throwing.
        var raw = new GeneratedModels.AuditEventView();

        var view = AuditQueryClient.ToView(raw);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.Empty);
            Assert.That(view.Source, Is.Empty);
            Assert.That(view.EventType, Is.Empty);
            Assert.That(view.EntityType, Is.Empty);
            Assert.That(view.Action, Is.Empty);
            Assert.That(view.OccurredAt, Is.EqualTo(DateTimeOffset.MinValue));
            Assert.That(view.EntityId, Is.Null);
            Assert.That(view.UserId, Is.Null);
            Assert.That(view.OldValues, Is.Null);
            Assert.That(view.NewValues, Is.Null);
            Assert.That(view.CorrelationId, Is.Null);
            Assert.That(view.MessageId, Is.Null);
        }
    }

    [Test]
    public void ToPage_NullRaw_ReturnsEmptyPage()
    {
        var page = AuditQueryClient.ToPage(null);

        Assert.That(page.Items, Is.Empty);
        Assert.That(page.Total, Is.Zero);
        Assert.That(page.Skip, Is.Zero);
        Assert.That(page.Limit, Is.Zero);
    }

    [Test]
    public void ToPage_PopulatedRaw_MapsItemsAndPagingFields()
    {
        var raw = new GeneratedModels.AuditPage
        {
            Items = new List<GeneratedModels.AuditEventView>
            {
                new() { Id = "a", Source = "pitstop", Action = "Insert" },
                new() { Id = "b", Source = "pitstop", Action = "Update" },
            },
            Total = 25,
            Skip = 0,
            Limit = 10,
        };

        var page = AuditQueryClient.ToPage(raw);

        Assert.That(page.Items, Has.Count.EqualTo(2));
        Assert.That(page.Items[0].Id, Is.EqualTo("a"));
        Assert.That(page.Items[1].Action, Is.EqualTo("Update"));
        Assert.That(page.Total, Is.EqualTo(25));
        Assert.That(page.Skip, Is.Zero);
        Assert.That(page.Limit, Is.EqualTo(10));
    }

    [Test]
    public void ToPage_NullItemsList_ReturnsEmptyItems()
    {
        var raw = new GeneratedModels.AuditPage
        {
            Items = null,
            Total = 0,
            Skip = 0,
            Limit = 50,
        };

        var page = AuditQueryClient.ToPage(raw);

        Assert.That(page.Items, Is.Empty);
        Assert.That(page.Limit, Is.EqualTo(50));
    }
}
