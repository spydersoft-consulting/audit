using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Spydersoft.AuditProcessor;

namespace Spydersoft.AuditProcessor.UnitTests.AuditIndexInitializerTests;

internal class StartAsyncTests
{
    [Test]
    public async Task StartAsync_CreatesAllFourIndexes()
    {
        var indexManager = Substitute.For<IMongoIndexManager<AuditEventDocument>>();
        var collection = Substitute.For<IMongoCollection<AuditEventDocument>>();
        collection.Indexes.Returns(indexManager);

        var initializer = new AuditIndexInitializer(collection, NullLogger<AuditIndexInitializer>.Instance);

        await initializer.StartAsync(CancellationToken.None);

        // Verify a single CreateMany call carrying exactly four index specs —
        // any future additions/removals to the initializer should bump this.
        await indexManager.Received(1).CreateManyAsync(
            Arg.Is<IEnumerable<CreateIndexModel<AuditEventDocument>>>(models => models.Count() == 4),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void StartAsync_BubblesMongoFailures()
    {
        // The initializer deliberately re-throws — without indexes the API
        // degrades to collection scans at scale, and we'd rather fail loud at
        // startup than silently accept that.
        var indexManager = Substitute.For<IMongoIndexManager<AuditEventDocument>>();
        indexManager
            .CreateManyAsync(
                Arg.Any<IEnumerable<CreateIndexModel<AuditEventDocument>>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("simulated mongo failure"));

        var collection = Substitute.For<IMongoCollection<AuditEventDocument>>();
        collection.Indexes.Returns(indexManager);

        var initializer = new AuditIndexInitializer(collection, NullLogger<AuditIndexInitializer>.Instance);

        Assert.ThrowsAsync<InvalidOperationException>(() => initializer.StartAsync(CancellationToken.None));
    }

    [Test]
    public async Task StopAsync_IsNoOp()
    {
        var collection = Substitute.For<IMongoCollection<AuditEventDocument>>();
        var initializer = new AuditIndexInitializer(collection, NullLogger<AuditIndexInitializer>.Instance);

        // Should complete instantly and never touch Mongo.
        await initializer.StopAsync(CancellationToken.None);

        var indexManager = collection.Indexes;
        await indexManager.DidNotReceive().CreateManyAsync(
            Arg.Any<IEnumerable<CreateIndexModel<AuditEventDocument>>>(),
            Arg.Any<CancellationToken>());
    }
}
