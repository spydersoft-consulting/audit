using Spydersoft.AuditApi;

namespace Spydersoft.AuditApi.UnitTests.AuthorizationPoliciesTests;

internal class PolicyNameTests
{
    [Test]
    public void Read_IsAuditReadScope() =>
        Assert.That(AuthorizationPolicies.Read, Is.EqualTo("audit:read"));
}
