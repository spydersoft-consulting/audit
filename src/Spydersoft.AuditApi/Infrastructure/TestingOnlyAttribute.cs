namespace Spydersoft.AuditApi.Infrastructure;

/// <summary>
/// Marks a controller as Testing-only — it will only be registered with MVC when
/// the host environment is "Testing". See <see cref="TestingOnlyControllerProvider"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TestingOnlyAttribute : Attribute;
