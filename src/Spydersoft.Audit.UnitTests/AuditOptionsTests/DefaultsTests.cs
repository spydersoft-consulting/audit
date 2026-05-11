using Spydersoft.Audit;

namespace Spydersoft.Audit.UnitTests.AuditOptionsTests;

internal class DefaultsTests
{
    [Test]
    public void SectionName_IsAudit() =>
        Assert.That(AuditOptions.SectionName, Is.EqualTo("Audit"));

    [Test]
    public void Source_DefaultsToEmpty() =>
        Assert.That(new AuditOptions().Source, Is.Empty);

    [Test]
    public void Topic_DefaultsToAuditEvents() =>
        Assert.That(new AuditOptions().Topic, Is.EqualTo("audit.events"));
}
