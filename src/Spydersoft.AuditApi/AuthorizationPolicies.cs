namespace Spydersoft.AuditApi;

/// <summary>
/// Single read scope. Configured at runtime through the IdentityServer admin UI —
/// see <c>docs/identity-configuration.md</c>.
/// </summary>
public static class AuthorizationPolicies
{
    public const string Read = "audit:read";
}
