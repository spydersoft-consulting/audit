using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using Spydersoft.Audit;
using Spydersoft.AuditProcessor;
using Spydersoft.Messaging;

namespace Spydersoft.AuditProcessor.UnitTests.AuditRecordHandlerTests;

internal class HandleAsyncTests
{
    [Test]
    public async Task HandleAsync_InsertsMappedDocument()
    {
        var collection = Substitute.For<IMongoCollection<AuditEventDocument>>();
        var handler = new AuditRecordHandler(collection, NullLogger<AuditRecordHandler>.Instance);

        var record = new AuditRecord
        {
            Source = "pitstop",
            EventType = "EFCore",
            EntityType = "FillUp",
            EntityId = """{"Id":42}""",
            Action = "Insert",
            UserId = "matt",
            OccurredAt = DateTimeOffset.UtcNow,
            NewValues = """{"Gallons":13.0}""",
        };
        var envelope = new MessageEnvelope<AuditRecord>
        {
            Payload = record,
            Topic = "audit.events",
            CorrelationId = "trace-123",
        };

        await handler.HandleAsync(envelope);

        await collection.Received(1).InsertOneAsync(
            Arg.Is<AuditEventDocument>(d =>
                d.Source == "pitstop" &&
                d.EntityType == "FillUp" &&
                d.EntityId == """{"Id":42}""" &&
                d.Action == "Insert" &&
                d.UserId == "matt" &&
                d.NewValues == """{"Gallons":13.0}""" &&
                d.CorrelationId == "trace-123" &&
                d.MessageId == envelope.MessageId),
            Arg.Any<InsertOneOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void Map_PopulatesEnvelopeMetadata()
    {
        var record = new AuditRecord
        {
            Source = "pitstop",
            EventType = "EFCore",
            EntityType = "FillUp",
            Action = "Insert",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        var envelope = new MessageEnvelope<AuditRecord>
        {
            Payload = record,
            CorrelationId = "trace-abc",
        };

        var doc = AuditRecordHandler.Map(envelope);

        Assert.That(doc.MessageId, Is.EqualTo(envelope.MessageId));
        Assert.That(doc.CorrelationId, Is.EqualTo("trace-abc"));
        Assert.That(doc.Id, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Map_NoCorrelationId_StaysNull()
    {
        var record = new AuditRecord
        {
            Source = "pitstop",
            EventType = "EFCore",
            EntityType = "FillUp",
            Action = "Insert",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        var envelope = new MessageEnvelope<AuditRecord> { Payload = record };

        var doc = AuditRecordHandler.Map(envelope);

        Assert.That(doc.CorrelationId, Is.Null);
    }
}
