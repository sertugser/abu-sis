using FluentAssertions;
using SIS.Application.Identity.Exceptions;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Services.Scope;

namespace SIS.Tests.Application;

public class ScopeGuardTests
{
    private sealed class StubScope : ICurrentUserScope
    {
        public bool IsActive { get; init; }
        public int? FacultyId { get; init; }
        public Task<bool> IsActiveAsync() => Task.FromResult(IsActive);
        public Task<int?> GetFacultyIdAsync() => Task.FromResult(FacultyId);
    }

    [Fact]
    public void NoOp_WhenScopeInactive()
    {
        var guard = new ScopeGuard(new StubScope { IsActive = false });

        var act = () => guard.EnsureCanWrite(ScopeKind.Faculty, 3);

        act.Should().NotThrow();
    }

    [Fact]
    public void Allows_WhenFacultyMatches()
    {
        var guard = new ScopeGuard(new StubScope { IsActive = true, FacultyId = 5 });

        var act = () => guard.EnsureCanWrite(ScopeKind.Faculty, 5);

        act.Should().NotThrow();
    }

    [Fact]
    public void Throws_WhenFacultyDiffers()
    {
        var guard = new ScopeGuard(new StubScope { IsActive = true, FacultyId = 5 });

        var act = () => guard.EnsureCanWrite(ScopeKind.Faculty, 8);

        act.Should().Throw<ScopeViolationException>();
    }

    [Fact]
    public void Throws_WhenScopeActiveButFacultyMissing()
    {
        var guard = new ScopeGuard(new StubScope { IsActive = true, FacultyId = null });

        var act = () => guard.EnsureCanWrite(ScopeKind.Faculty, 1);

        act.Should().Throw<ScopeViolationException>();
    }
}
