using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Spydersoft.AuditApi.Infrastructure;

/// <summary>
/// Replaces the default <see cref="ControllerFeatureProvider"/> with one that
/// excludes any controller marked with <see cref="TestingOnlyAttribute"/> when
/// the host is not running in the "Testing" environment.
/// </summary>
public sealed class TestingOnlyControllerProvider(bool isTestingEnvironment) : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        if (!base.IsController(typeInfo))
        {
            return false;
        }

        var isTestingOnly = typeInfo.GetCustomAttribute<TestingOnlyAttribute>() is not null;
        return !isTestingOnly || isTestingEnvironment;
    }
}
