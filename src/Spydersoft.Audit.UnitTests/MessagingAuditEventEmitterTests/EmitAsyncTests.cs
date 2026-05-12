using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Spydersoft.Audit;
using Spydersoft.Messaging;

namespace Spydersoft.Audit.UnitTests.MessagingAuditEventEmitterTests;

internal class EmitAsyncTests
{
    private IMessagePublisher _publisher = null!;
    private MessagingAuditEventEmitter _emitter = null!;

    [SetUp]
    public void SetUp()
    {
        _publisher = Substitute.For<IMessagePublisher>();
        var options = Options.Create(new AuditOptions { Source = "test-app", Topic = "audit.events" });
        _emitter = new MessagingAuditEventEmitter(
            _publisher,
            options,
            NullLogger<MessagingAuditEventEmitter>.Instance);
    }

    [Test]
    public async Task EmitAsync_PublishesEachRecordOnConfiguredTopic()
    {
        var records = new[]
        {
            new AuditRecord { Source = "test-app", EventType = "EFCore", EntityType = "FillUp", Action = "Insert" },
            new AuditRecord { Source = "test-app", EventType = "EFCore", EntityType = "FillUp", Action = "Update" },
        };

        await _emitter.EmitAsync(records);

        await _publisher.Received(1).PublishAsync("audit.events", records[0], Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync("audit.events", records[1], Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EmitAsync_SwallowsTransportFailuresAndContinues()
    {
        var records = new[]
        {
            new AuditRecord { Source = "test-app", EventType = "EFCore", EntityType = "A", Action = "Insert" },
            new AuditRecord { Source = "test-app", EventType = "EFCore", EntityType = "B", Action = "Insert" },
        };

        // First call throws; second succeeds. ThrowsAsync (not Throws) — the method
        // is async, so the simulated failure should surface via the returned Task,
        // matching real-world failure behaviour and silencing NS5003.
        _publisher
            .PublishAsync("audit.events", records[0], Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("simulated transport failure"));

        Assert.DoesNotThrowAsync(async () => await _emitter.EmitAsync(records));

        // Second record is still attempted despite the first failing.
        await _publisher.Received(1).PublishAsync("audit.events", records[1], Arg.Any<CancellationToken>());
    }

    [Test]
    public void EmitAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _publisher
            .PublishAsync(Arg.Any<string>(), Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _emitter.EmitAsync(
                new[] { new AuditRecord { Source = "x", EventType = "y", EntityType = "z", Action = "Insert" } },
                cts.Token));
    }

    [Test]
    public async Task EmitAsync_NoRecords_DoesNothing()
    {
        await _emitter.EmitAsync(Array.Empty<AuditRecord>());
        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<string>(), Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }
}
