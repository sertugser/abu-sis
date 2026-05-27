using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SIS.Domain.Enums;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class PermissionServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PermSvc-{Guid.NewGuid()}")
            .Options);

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(
        ApplicationUser user,
        IList<string> roles)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        mgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(roles);
        return mgr;
    }

    private static Permission Perm(int id, string key, PermissionCategory cat = PermissionCategory.Menu) =>
        new() { Id = id, Key = key, Category = cat };

    [Fact]
    public async Task GetUserPermissionsAsync_ReturnsAllKeys_WhenUserIsSystemAdmin()
    {
        using var ctx = CreateContext();
        ctx.Permissions.AddRange(
            Perm(1, "menu.a.view"),
            Perm(2, "menu.b.view"),
            Perm(3, "system.x.assign", PermissionCategory.System));
        await ctx.SaveChangesAsync();

        var user = new ApplicationUser { Id = 42, UserName = "sysadmin" };
        var userMgr = CreateUserManagerMock(user, new[] { "SystemAdministrator" });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new PermissionService(ctx, cache, userMgr.Object);

        var result = await svc.GetUserPermissionsAsync(user.Id);

        result.Should().BeEquivalentTo("menu.a.view", "menu.b.view", "system.x.assign");
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ReturnsRoleAggregatedKeys_ForNormalUser()
    {
        using var ctx = CreateContext();
        ctx.Roles.Add(new IdentityRole<int> { Id = 10, Name = "Dean", NormalizedName = "DEAN" });
        ctx.Permissions.AddRange(
            Perm(1, "menu.faculty.view"),
            Perm(2, "menu.faculty.edit"),
            Perm(3, "menu.unrelated.view"));
        ctx.RolePermissions.AddRange(
            new RolePermission { RoleId = 10, PermissionId = 1 },
            new RolePermission { RoleId = 10, PermissionId = 2 });
        await ctx.SaveChangesAsync();

        var user = new ApplicationUser { Id = 5, UserName = "dean1" };
        var userMgr = CreateUserManagerMock(user, new[] { "Dean" });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new PermissionService(ctx, cache, userMgr.Object);

        var result = await svc.GetUserPermissionsAsync(user.Id);

        result.Should().BeEquivalentTo("menu.faculty.view", "menu.faculty.edit");
        result.Should().NotContain("menu.unrelated.view");
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ReturnsCachedSet_OnSecondCall()
    {
        using var ctx = CreateContext();
        ctx.Permissions.Add(Perm(1, "menu.a.view"));
        await ctx.SaveChangesAsync();

        var user = new ApplicationUser { Id = 7, UserName = "u" };
        var userMgr = CreateUserManagerMock(user, new[] { "SystemAdministrator" });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new PermissionService(ctx, cache, userMgr.Object);

        var first = await svc.GetUserPermissionsAsync(user.Id);
        ctx.Permissions.Add(Perm(2, "menu.b.view"));
        await ctx.SaveChangesAsync();

        var second = await svc.GetUserPermissionsAsync(user.Id);

        // Cache hit → ikinci çağrı ilk sonucu döner, yeni eklenen permission dahil değil
        second.Should().BeEquivalentTo(first);
        second.Should().NotContain("menu.b.view");
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsTrue_WhenUserHasKey()
    {
        using var ctx = CreateContext();
        ctx.Roles.Add(new IdentityRole<int> { Id = 10, Name = "Student", NormalizedName = "STUDENT" });
        ctx.Permissions.Add(Perm(1, "menu.alinan-dersler.view"));
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1 });
        await ctx.SaveChangesAsync();

        var user = new ApplicationUser { Id = 5 };
        var userMgr = CreateUserManagerMock(user, new[] { "Student" });
        var svc = new PermissionService(ctx, new MemoryCache(new MemoryCacheOptions()), userMgr.Object);

        (await svc.HasPermissionAsync(user.Id, "menu.alinan-dersler.view")).Should().BeTrue();
        (await svc.HasPermissionAsync(user.Id, "menu.akademik-donem.cancel")).Should().BeFalse();
    }

    [Fact]
    public async Task SetRolePermissionsAsync_ReplacesAllPermissions_AndInvalidatesCache()
    {
        using var ctx = CreateContext();
        ctx.Roles.Add(new IdentityRole<int> { Id = 10, Name = "Dean", NormalizedName = "DEAN" });
        ctx.Permissions.AddRange(
            Perm(1, "menu.a.view"),
            Perm(2, "menu.b.view"),
            Perm(3, "menu.c.view"));
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1 });
        ctx.Set<IdentityUserRole<int>>().Add(new IdentityUserRole<int> { UserId = 99, RoleId = 10 });
        await ctx.SaveChangesAsync();

        var user = new ApplicationUser { Id = 99 };
        var userMgr = CreateUserManagerMock(user, new[] { "Dean" });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new PermissionService(ctx, cache, userMgr.Object);

        // Önce cache'i doldur
        var before = await svc.GetUserPermissionsAsync(99);
        before.Should().BeEquivalentTo("menu.a.view");

        // Permission'ları replace et: a → b,c
        await svc.SetRolePermissionsAsync(roleId: 10, permissionIds: new[] { 2, 3 }, grantedByUserId: 1);

        // Cache invalidate edilmiş olmalı → fresh lookup yapılır
        var after = await svc.GetUserPermissionsAsync(99);
        after.Should().BeEquivalentTo("menu.b.view", "menu.c.view");
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ReturnsEmpty_WhenUserHasNoRoles()
    {
        using var ctx = CreateContext();
        ctx.Permissions.Add(Perm(1, "menu.a.view"));
        await ctx.SaveChangesAsync();

        var user = new ApplicationUser { Id = 100 };
        var userMgr = CreateUserManagerMock(user, Array.Empty<string>());
        var svc = new PermissionService(ctx, new MemoryCache(new MemoryCacheOptions()), userMgr.Object);

        var result = await svc.GetUserPermissionsAsync(100);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SetRolePermissionsAsync_PreservesBundleRows_OnlyReplacesManual()
    {
        // C-2 regresyon testi: matris "Kaydet" paketten gelen (SourceBundleId != null)
        // satırlara dokunmamalı; sadece manuel satırları değiştirmeli.
        using var ctx = CreateContext();
        ctx.Roles.Add(new IdentityRole<int> { Id = 10, Name = "Dean", NormalizedName = "DEAN" });
        ctx.Permissions.AddRange(Perm(1, "menu.a.view"), Perm(2, "menu.b.view"), Perm(3, "menu.c.view"));
        // 1 = manuel, 2 = paketten (bundle 5), 3 = paketten (bundle 5)
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = null });
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 2, SourceBundleId = 5 });
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 3, SourceBundleId = 5 });
        await ctx.SaveChangesAsync();

        var userMgr = CreateUserManagerMock(new ApplicationUser { Id = 1 }, Array.Empty<string>());
        var svc = new PermissionService(ctx, new MemoryCache(new MemoryCacheOptions()), userMgr.Object);

        // Manuel set'i 1 yerine 4 yap (ama 4 yok; sadece manuel davranışı test edelim → 1'i kaldır, manuel boş)
        await svc.SetRolePermissionsAsync(roleId: 10, permissionIds: Array.Empty<int>(), grantedByUserId: 99);

        var rows = await ctx.RolePermissions.Where(rp => rp.RoleId == 10).ToListAsync();
        // Paket satırları (2,3) korunmalı; manuel (1) silinmeli
        rows.Select(r => r.PermissionId).Should().BeEquivalentTo(new[] { 2, 3 });
        rows.Should().OnlyContain(r => r.SourceBundleId == 5);
    }

    [Fact]
    public async Task GetRolePermissionStateAsync_SplitsManualAndBundle()
    {
        using var ctx = CreateContext();
        ctx.Permissions.AddRange(Perm(1, "menu.a.view"), Perm(2, "menu.b.view"), Perm(3, "menu.c.view"));
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = null });
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 2, SourceBundleId = 5 });
        // 3: hem manuel hem paket → ikisinde de görünür
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 3, SourceBundleId = null });
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 3, SourceBundleId = 5 });
        await ctx.SaveChangesAsync();

        var userMgr = CreateUserManagerMock(new ApplicationUser { Id = 1 }, Array.Empty<string>());
        var svc = new PermissionService(ctx, new MemoryCache(new MemoryCacheOptions()), userMgr.Object);

        var state = await svc.GetRolePermissionStateAsync(10);
        state.ManualPermissionIds.Should().BeEquivalentTo(new[] { 1, 3 });
        state.BundlePermissionIds.Should().BeEquivalentTo(new[] { 2, 3 });
    }

    [Fact]
    public async Task GetRolePermissionIdsAsync_IsDistinct_WhenPermissionIsBothManualAndBundle()
    {
        using var ctx = CreateContext();
        ctx.Permissions.Add(Perm(1, "menu.a.view"));
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = null });
        ctx.RolePermissions.Add(new RolePermission { RoleId = 10, PermissionId = 1, SourceBundleId = 5 });
        await ctx.SaveChangesAsync();

        var userMgr = CreateUserManagerMock(new ApplicationUser { Id = 1 }, Array.Empty<string>());
        var svc = new PermissionService(ctx, new MemoryCache(new MemoryCacheOptions()), userMgr.Object);

        var ids = await svc.GetRolePermissionIdsAsync(10);
        ids.Should().BeEquivalentTo(new[] { 1 });
    }
}
