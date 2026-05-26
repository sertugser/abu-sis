using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class PermissionDelegationServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DelegationSvc-{Guid.NewGuid()}")
            .Options);

    private static PermissionDelegationService CreateService(AppDbContext ctx) =>
        new(ctx, new Mock<IPermissionService>().Object);

    private static ApplicationUser User(int id, string userName) =>
        new() { Id = id, UserName = userName, FirstName = userName, LastName = "Test" };

    private static async Task SeedUsersAsync(AppDbContext ctx, params ApplicationUser[] users)
    {
        ctx.Users.AddRange(users);
        await ctx.SaveChangesAsync();
    }

    private static CreateDelegationDto NewDto(
        int delegator,
        int delegatee,
        DateTime? start = null,
        DateTime? end = null,
        string reason = "İzin vekâleti") =>
        new(delegator, delegatee,
            start ?? DateTime.UtcNow.AddHours(-1),
            end ?? DateTime.UtcNow.AddHours(1),
            reason);

    [Fact]
    public async Task CreateAsync_Throws_WhenSelfDelegation()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(NewDto(1, 1), createdByUserId: 99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*kendisine vekâlet*");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenEndBeforeStart()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var dto = NewDto(1, 2, start: DateTime.UtcNow.AddHours(2), end: DateTime.UtcNow);
        var act = () => svc.CreateAsync(dto, createdByUserId: 99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Bitiş zamanı*");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenReasonEmpty()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(NewDto(1, 2, reason: "   "), createdByUserId: 99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sebebi*");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenReasonTooLong()
    {
        using var ctx = CreateContext();
        await SeedUsersAsync(ctx, User(1, "a"), User(2, "b"));
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(NewDto(1, 2, reason: new string('x', 501)), createdByUserId: 99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*500 karakter*");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenDelegatorIsSystemAdmin()
    {
        using var ctx = CreateContext();
        await SeedUsersAsync(ctx, User(1, "sysadmin"), User(2, "deputy"));
        ctx.Roles.Add(new IdentityRole<int> { Id = 1, Name = "SystemAdministrator", NormalizedName = "SYSTEMADMINISTRATOR" });
        ctx.Set<IdentityUserRole<int>>().Add(new IdentityUserRole<int> { UserId = 1, RoleId = 1 });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(NewDto(1, 2), createdByUserId: 99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sistem yöneticisinin yetkileri*");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenOverlappingDelegationExists()
    {
        using var ctx = CreateContext();
        await SeedUsersAsync(ctx, User(1, "a"), User(2, "b"));
        var now = DateTime.UtcNow;
        ctx.PermissionDelegations.Add(new PermissionDelegation
        {
            DelegatorUserId = 1,
            DelegateeUserId = 2,
            StartsAtUtc = now.AddHours(-1),
            EndsAtUtc = now.AddHours(3),
            Reason = "mevcut",
            CreatedAtUtc = now,
            CreatedByUserId = 99,
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(
            NewDto(1, 2, start: now, end: now.AddHours(2)), createdByUserId: 99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*çakışan tarih aralığında*");
    }

    [Fact]
    public async Task CreateAsync_Succeeds_AndReturnsActiveStatus()
    {
        using var ctx = CreateContext();
        await SeedUsersAsync(ctx, User(1, "veren"), User(2, "vekil"));
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(NewDto(1, 2), createdByUserId: 99);

        result.Id.Should().BeGreaterThan(0);
        result.DelegatorUserId.Should().Be(1);
        result.DelegateeUserId.Should().Be(2);
        result.Status.Should().Be("Active");
        (await ctx.PermissionDelegations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_ReturnsPending_WhenStartInFuture()
    {
        using var ctx = CreateContext();
        await SeedUsersAsync(ctx, User(1, "veren"), User(2, "vekil"));
        var svc = CreateService(ctx);

        var future = DateTime.UtcNow.AddDays(1);
        var result = await svc.CreateAsync(
            NewDto(1, 2, start: future, end: future.AddDays(1)), createdByUserId: 99);

        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task RevokeAsync_SetsRevoked_AndSecondRevokeThrows()
    {
        using var ctx = CreateContext();
        await SeedUsersAsync(ctx, User(1, "veren"), User(2, "vekil"));
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(NewDto(1, 2), createdByUserId: 99);

        var revoked = await svc.RevokeAsync(created.Id, new RevokeDelegationDto("erken bitti"), revokedByUserId: 7);
        revoked.Status.Should().Be("Revoked");
        revoked.RevokeReason.Should().Be("erken bitti");

        var act = () => svc.RevokeAsync(created.Id, new RevokeDelegationDto(null), revokedByUserId: 7);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*zaten iptal*");
    }

    [Fact]
    public async Task ListAsync_ExcludesRevokedAndExpired_ByDefault()
    {
        using var ctx = CreateContext();
        await SeedUsersAsync(ctx, User(1, "a"), User(2, "b"));
        var now = DateTime.UtcNow;
        ctx.PermissionDelegations.AddRange(
            new PermissionDelegation { DelegatorUserId = 1, DelegateeUserId = 2, StartsAtUtc = now.AddHours(-1), EndsAtUtc = now.AddHours(1), Reason = "aktif", CreatedAtUtc = now, CreatedByUserId = 9 },
            new PermissionDelegation { DelegatorUserId = 1, DelegateeUserId = 2, StartsAtUtc = now.AddDays(-5), EndsAtUtc = now.AddDays(-3), Reason = "expired", CreatedAtUtc = now, CreatedByUserId = 9 },
            new PermissionDelegation { DelegatorUserId = 1, DelegateeUserId = 2, StartsAtUtc = now.AddHours(-1), EndsAtUtc = now.AddHours(2), Reason = "revoked", RevokedAtUtc = now, CreatedAtUtc = now, CreatedByUserId = 9 });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var active = await svc.ListAsync(includeInactive: false, null, null);
        active.Should().ContainSingle().Which.Reason.Should().Be("aktif");

        var all = await svc.ListAsync(includeInactive: true, null, null);
        all.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActiveDelegatedRoleNamesAsync_ExcludesSysAdminAndLockedDelegator()
    {
        using var ctx = CreateContext();
        var dean = User(2, "dean");
        var sysadmin = User(3, "sysadmin");
        var locked = User(4, "locked");
        locked.LockoutEnabled = true;
        locked.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1);
        await SeedUsersAsync(ctx, User(1, "vekil"), dean, sysadmin, locked);

        ctx.Roles.AddRange(
            new IdentityRole<int> { Id = 1, Name = "Dean", NormalizedName = "DEAN" },
            new IdentityRole<int> { Id = 2, Name = "SystemAdministrator", NormalizedName = "SYSTEMADMINISTRATOR" },
            new IdentityRole<int> { Id = 3, Name = "Instructor", NormalizedName = "INSTRUCTOR" });
        ctx.Set<IdentityUserRole<int>>().AddRange(
            new IdentityUserRole<int> { UserId = 2, RoleId = 1 },
            new IdentityUserRole<int> { UserId = 3, RoleId = 2 },
            new IdentityUserRole<int> { UserId = 4, RoleId = 3 });

        var now = DateTime.UtcNow;
        ctx.PermissionDelegations.AddRange(
            new PermissionDelegation { DelegatorUserId = 2, DelegateeUserId = 1, StartsAtUtc = now.AddHours(-1), EndsAtUtc = now.AddHours(1), Reason = "dean", CreatedAtUtc = now, CreatedByUserId = 9 },
            new PermissionDelegation { DelegatorUserId = 3, DelegateeUserId = 1, StartsAtUtc = now.AddHours(-1), EndsAtUtc = now.AddHours(1), Reason = "sysadmin", CreatedAtUtc = now, CreatedByUserId = 9 },
            new PermissionDelegation { DelegatorUserId = 4, DelegateeUserId = 1, StartsAtUtc = now.AddHours(-1), EndsAtUtc = now.AddHours(1), Reason = "locked", CreatedAtUtc = now, CreatedByUserId = 9 });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var roles = await svc.GetActiveDelegatedRoleNamesAsync(userId: 1);

        roles.Should().BeEquivalentTo("Dean");
    }
}
