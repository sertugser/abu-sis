using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scope;

namespace SIS.Tests.Application;

public class CurrentUserScopeTests
{
    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Scope-{Guid.NewGuid()}")
            .Options;

    private static IHttpContextAccessor HttpContextAccessor(int? userId, params string[] roles)
    {
        var context = new DefaultHttpContext();
        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                ..roles.Select(role => new Claim(ClaimTypes.Role, role))
            ], "TestAuth"));
        }

        return new HttpContextAccessor { HttpContext = context };
    }

    [Fact]
    public async Task IsActive_False_WhenNoUser()
    {
        var options = CreateOptions();
        var sut = new CurrentUserScope(HttpContextAccessor(null), options);

        (await sut.IsActiveAsync()).Should().BeFalse();
        (await sut.GetFacultyIdAsync()).Should().BeNull();
    }

    [Fact]
    public async Task IsActive_False_WhenSystemAdmin()
    {
        var options = CreateOptions();
        var sut = new CurrentUserScope(HttpContextAccessor(1, "SystemAdministrator"), options);

        (await sut.IsActiveAsync()).Should().BeFalse();
        (await sut.GetFacultyIdAsync()).Should().BeNull();
    }

    [Fact]
    public async Task IsActive_False_WhenNoScopeBoundRole()
    {
        var options = CreateOptions();
        var sut = new CurrentUserScope(HttpContextAccessor(2, "Instructor"), options);

        (await sut.IsActiveAsync()).Should().BeFalse();
        (await sut.GetFacultyIdAsync()).Should().BeNull();
    }

    [Fact]
    public async Task IsActive_True_WhenDeanHasFacultyScope()
    {
        var options = CreateOptions();
        await using (var db = new AppDbContext(options))
        {
            db.UserScopes.Add(new UserScope
            {
                UserId = 3,
                Kind = ScopeKind.Faculty,
                FacultyId = 7,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var sut = new CurrentUserScope(HttpContextAccessor(3, "Dean"), options);

        (await sut.IsActiveAsync()).Should().BeTrue();
        (await sut.GetFacultyIdAsync()).Should().Be(7);
        sut.IsActive.Should().BeTrue();
        sut.FacultyId.Should().Be(7);
    }

    [Fact]
    public async Task IsActive_True_ButFacultyNull_WhenDeanHasNoScopeRow()
    {
        var sut = new CurrentUserScope(HttpContextAccessor(4, "Dean"), CreateOptions());

        (await sut.IsActiveAsync()).Should().BeTrue();
        (await sut.GetFacultyIdAsync()).Should().BeNull();
        sut.IsActive.Should().BeTrue();
        sut.FacultyId.Should().BeNull();
    }
}
