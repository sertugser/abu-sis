using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Identity.Services;
using SIS.Domain.Enums;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class PermissionBundleServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BundleSvc-{Guid.NewGuid()}")
            .Options);

    private static Permission Perm(int id, string key) =>
        new() { Id = id, Key = key, Category = PermissionCategory.Menu };

    private static (PermissionBundleService svc, Mock<IPermissionService> permSvc) CreateService(AppDbContext ctx)
    {
        var permSvc = new Mock<IPermissionService>();
        permSvc.Setup(p => p.InvalidateRoleCacheAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        return (new PermissionBundleService(ctx, permSvc.Object), permSvc);
    }

    private static async Task SeedBundleAsync(AppDbContext ctx, int bundleId, params int[] permissionIds)
    {
        ctx.PermissionBundles.Add(new PermissionBundle
        {
            Id = bundleId,
            Name = $"Bundle-{bundleId}",
            IsSystemDefault = false,
            Items = permissionIds.Select(pid => new PermissionBundleItem { BundleId = bundleId, PermissionId = pid }).ToList(),
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task ApplyBundleToRoleAsync_AddsBundleRows_AndPreservesManual()
    {
        using var ctx = CreateContext();
        ctx.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole<int> { Id = 10, Name = "Dean", NormalizedName = "DEAN" });
        ctx.Permissions.AddRange(Perm(1, "menu.a.view"), Perm(2, "menu.b.view"), Perm(3, "menu.c.view"));
        // Mevcut manuel yetki
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = null });
        await ctx.SaveChangesAsync();
        await SeedBundleAsync(ctx, bundleId: 5, 2, 3);

        var (svc, permSvc) = CreateService(ctx);
        await svc.ApplyBundleToRoleAsync(roleId: 10, bundleId: 5, actorUserId: 99);

        var rows = await ctx.RolePermissions.Where(rp => rp.RoleId == 10).ToListAsync();
        rows.Should().HaveCount(3);
        rows.Single(r => r.PermissionId == 1).SourceBundleId.Should().BeNull();
        rows.Where(r => r.SourceBundleId == 5).Select(r => r.PermissionId).Should().BeEquivalentTo(new[] { 2, 3 });
        permSvc.Verify(p => p.InvalidateRoleCacheAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyBundleToRoleAsync_Reapply_ReplacesOnlySameBundleRows()
    {
        using var ctx = CreateContext();
        ctx.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole<int> { Id = 10, Name = "Dean", NormalizedName = "DEAN" });
        ctx.Permissions.AddRange(Perm(1, "menu.a.view"), Perm(2, "menu.b.view"), Perm(3, "menu.c.view"));
        await ctx.SaveChangesAsync();
        await SeedBundleAsync(ctx, bundleId: 5, 1, 2);

        var (svc, _) = CreateService(ctx);
        await svc.ApplyBundleToRoleAsync(10, 5, 99);

        // Paket içeriğini değiştir (1,2 → 2,3), yeniden uygula
        var bundle = await ctx.PermissionBundles.Include(b => b.Items).FirstAsync(b => b.Id == 5);
        ctx.PermissionBundleItems.RemoveRange(bundle.Items);
        ctx.PermissionBundleItems.AddRange(
            new PermissionBundleItem { BundleId = 5, PermissionId = 2 },
            new PermissionBundleItem { BundleId = 5, PermissionId = 3 });
        await ctx.SaveChangesAsync();

        await svc.ApplyBundleToRoleAsync(10, 5, 99);

        var rows = await ctx.RolePermissions.Where(rp => rp.RoleId == 10 && rp.SourceBundleId == 5).ToListAsync();
        rows.Select(r => r.PermissionId).Should().BeEquivalentTo(new[] { 2, 3 });
    }

    [Fact]
    public async Task RemoveBundleFromRoleAsync_RemovesOnlyThatBundlesRows()
    {
        using var ctx = CreateContext();
        ctx.Permissions.AddRange(Perm(1, "menu.a.view"), Perm(2, "menu.b.view"));
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = null });
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = 5 });
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 2, SourceBundleId = 6 });
        await ctx.SaveChangesAsync();

        var (svc, _) = CreateService(ctx);
        await svc.RemoveBundleFromRoleAsync(roleId: 10, bundleId: 5);

        var rows = await ctx.RolePermissions.Where(rp => rp.RoleId == 10).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(r => r.PermissionId == 1 && r.SourceBundleId == null);
        rows.Should().ContainSingle(r => r.PermissionId == 2 && r.SourceBundleId == 6);
    }

    [Fact]
    public async Task GetAppliedBundlesAsync_FlagsStale_WhenBundleContentChanged()
    {
        using var ctx = CreateContext();
        ctx.Permissions.AddRange(Perm(1, "menu.a.view"), Perm(2, "menu.b.view"), Perm(3, "menu.c.view"));
        await ctx.SaveChangesAsync();
        // Bundle 5 güncel içeriği: {1,2}; Bundle 6 güncel içeriği: {3}
        await SeedBundleAsync(ctx, bundleId: 5, 1, 2);
        await SeedBundleAsync(ctx, bundleId: 6, 3);
        // Role uygulanmış snapshot: bundle 5 → {1} (eski!), bundle 6 → {3} (güncel)
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = 5 });
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 3, SourceBundleId = 6 });
        await ctx.SaveChangesAsync();

        var (svc, _) = CreateService(ctx);
        var applied = await svc.GetAppliedBundlesAsync(10);

        applied.Should().HaveCount(2);
        applied.Single(a => a.BundleId == 5).IsStale.Should().BeTrue();
        applied.Single(a => a.BundleId == 6).IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task GetAppliedBundlesAsync_ReturnsEmpty_WhenNoBundlesApplied()
    {
        using var ctx = CreateContext();
        ctx.Permissions.Add(Perm(1, "menu.a.view"));
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = null });
        await ctx.SaveChangesAsync();

        var (svc, _) = CreateService(ctx);
        var applied = await svc.GetAppliedBundlesAsync(10);
        applied.Should().BeEmpty();
    }
}
