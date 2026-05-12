using Spydersoft.Audit.Client;

namespace Spydersoft.Audit.Client.UnitTests.AuditClientOptionsTests;

internal class DefaultsTests
{
    [Test]
    public void SectionName_IsAuditClient() =>
        Assert.That(AuditClientOptions.SectionName, Is.EqualTo("AuditClient"));

    [Test]
    public void BaseUrl_DefaultsToEmpty() =>
        Assert.That(new AuditClientOptions().BaseUrl, Is.Empty);

    [Test]
    public void Scope_DefaultsToAuditRead() =>
        Assert.That(new AuditClientOptions().Scope, Is.EqualTo("audit:read"));

    [Test]
    public void TokenEndpoint_DefaultsToNull() =>
        Assert.That(new AuditClientOptions().TokenEndpoint, Is.Null);
}
