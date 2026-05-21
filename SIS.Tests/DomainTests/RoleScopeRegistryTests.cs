using FluentAssertions;
using SIS.Domain.Identity;

namespace SIS.Tests.DomainTests;

public class RoleScopeRegistryTests
{
    [Fact]
    public void IsScopeBound_ReturnsTrue_ForDean()
    {
        RoleScopeRegistry.IsScopeBound("Dean").Should().BeTrue();
    }

    [Fact]
    public void IsScopeBound_ReturnsFalse_ForInstructor()
    {
        RoleScopeRegistry.IsScopeBound("Instructor").Should().BeFalse();
    }

    [Fact]
    public void IsScopeBound_ReturnsFalse_ForSystemAdministrator()
    {
        RoleScopeRegistry.IsScopeBound("SystemAdministrator").Should().BeFalse();
    }

    [Fact]
    public void IsScopeBound_IsCaseSensitive()
    {
        RoleScopeRegistry.IsScopeBound("dean").Should().BeFalse();
    }
}
