using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class MenuServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MenuSvc-{Guid.NewGuid()}")
            .Options);

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(IList<string>? roles = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
           .ReturnsAsync((string id) => new ApplicationUser { Id = int.Parse(id) });
        mgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
           .ReturnsAsync(roles ?? new List<string>());
        return mgr;
    }

    private static MenuService CreateService(AppDbContext ctx, HashSet<string> userPerms, IList<string>? userRoles = null)
    {
        var permSvc = new Mock<IPermissionService>();
        permSvc.Setup(p => p.GetUserPermissionsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(userPerms);
        var delSvc = new Mock<IPermissionDelegationService>();
        delSvc.Setup(d => d.GetActiveDelegatedRoleNamesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<string>());
        var userMgr = CreateUserManagerMock(userRoles);
        return new MenuService(ctx, permSvc.Object, delSvc.Object, userMgr.Object);
    }

    private static (MenuModule mod, MenuItem item, Permission perm) AddSimpleModuleWithItem(
        AppDbContext ctx, string label, string route, string permKey, int sortOrder = 1)
    {
        var perm = new Permission { Key = permKey };
        ctx.Permissions.Add(perm);
        ctx.SaveChanges();

        var mod = new MenuModule { Label = label, Icon = "X", SortOrder = sortOrder };
        ctx.MenuModules.Add(mod);
        ctx.SaveChanges();

        var item = new MenuItem
        {
            ModuleId = mod.Id,
            Label = label,
            Route = route,
            Slug = route.TrimStart('/'),
            SortOrder = 1,
            MenuItemPermissions = new List<MenuItemPermission>
            {
                new() { PermissionId = perm.Id }
            }
        };
        ctx.MenuItems.Add(item);
        ctx.SaveChanges();

        return (mod, item, perm);
    }

    [Fact]
    public async Task GetUserMenuAsync_ReturnsOnlyPermittedItems()
    {
        using var ctx = CreateContext();
        AddSimpleModuleWithItem(ctx, "Allowed", "/a", "menu.a.view", 1);
        AddSimpleModuleWithItem(ctx, "Forbidden", "/b", "menu.b.view", 2);

        var svc = CreateService(ctx, new HashSet<string> { "menu.a.view" });

        var menu = await svc.GetUserMenuAsync(1);

        menu.Should().HaveCount(1);
        menu[0].Label.Should().Be("Allowed");
    }

    [Fact]
    public async Task GetUserMenuAsync_EliminatesEmptyModules()
    {
        using var ctx = CreateContext();
        AddSimpleModuleWithItem(ctx, "Empty", "/x", "menu.x.view", 1);

        var svc = CreateService(ctx, new HashSet<string>()); // user'ın hiç permission'ı yok

        var menu = await svc.GetUserMenuAsync(1);

        menu.Should().BeEmpty(); // boş modül elenmeli
    }

    [Fact]
    public async Task GetUserMenuAsync_RecursesNestedModules()
    {
        using var ctx = CreateContext();

        var perm = new Permission { Key = "menu.kullanici-tanimlama.view" };
        ctx.Permissions.Add(perm);
        ctx.SaveChanges();

        var yonetim = new MenuModule { Label = "Yönetim", Icon = "Shield", SortOrder = 1 };
        ctx.MenuModules.Add(yonetim);
        ctx.SaveChanges();

        var kullaniciIslemleri = new MenuModule
        {
            ParentId = yonetim.Id,
            Label = "Kullanıcı İşlemleri",
            SortOrder = 1,
        };
        ctx.MenuModules.Add(kullaniciIslemleri);
        ctx.SaveChanges();

        var item = new MenuItem
        {
            ModuleId = kullaniciIslemleri.Id,
            Label = "Kullanıcı Tanımlama",
            Route = "/kullanici-yonetimi",
            Slug = "kullanici-tanimlama",
            SortOrder = 1,
            MenuItemPermissions = new List<MenuItemPermission>
            {
                new() { PermissionId = perm.Id }
            }
        };
        ctx.MenuItems.Add(item);
        ctx.SaveChanges();

        var svc = CreateService(ctx, new HashSet<string> { "menu.kullanici-tanimlama.view" });

        var menu = await svc.GetUserMenuAsync(1);

        menu.Should().HaveCount(1);
        menu[0].Label.Should().Be("Yönetim");
        menu[0].Children.Should().HaveCount(1);
        menu[0].Children[0].Label.Should().Be("Kullanıcı İşlemleri");
        menu[0].Children[0].Items.Should().HaveCount(1);
        menu[0].Children[0].Items[0].Route.Should().Be("/kullanici-yonetimi");
    }

    [Fact]
    public async Task GetUserMenuAsync_HidesNestedParent_WhenAllChildrenEmpty()
    {
        using var ctx = CreateContext();

        var perm = new Permission { Key = "menu.x.view" };
        ctx.Permissions.Add(perm);
        ctx.SaveChanges();

        var yonetim = new MenuModule { Label = "Yönetim", SortOrder = 1 };
        ctx.MenuModules.Add(yonetim);
        ctx.SaveChanges();

        var alt = new MenuModule { ParentId = yonetim.Id, Label = "Alt", SortOrder = 1 };
        ctx.MenuModules.Add(alt);
        ctx.SaveChanges();

        ctx.MenuItems.Add(new MenuItem
        {
            ModuleId = alt.Id,
            Label = "Item",
            Route = "/x",
            Slug = "x",
            MenuItemPermissions = new List<MenuItemPermission>
            {
                new() { PermissionId = perm.Id }
            }
        });
        ctx.SaveChanges();

        // User'ın permission'ı YOK → alt modül boş → yönetim de boş → ikisi de elenmeli
        var svc = CreateService(ctx, new HashSet<string>());

        var menu = await svc.GetUserMenuAsync(1);
        menu.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserMenuAsync_RespectsSortOrder()
    {
        using var ctx = CreateContext();
        AddSimpleModuleWithItem(ctx, "Second", "/2", "menu.s.view", sortOrder: 5);
        AddSimpleModuleWithItem(ctx, "First", "/1", "menu.f.view", sortOrder: 1);

        var svc = CreateService(ctx, new HashSet<string> { "menu.s.view", "menu.f.view" });

        var menu = await svc.GetUserMenuAsync(1);
        menu.Select(m => m.Label).Should().Equal("First", "Second");
    }

    [Fact]
    public async Task GetUserMenuAsync_HidesModule_WhenAllowedRolesCsvDoesNotMatch()
    {
        using var ctx = CreateContext();
        // Patch 2C: modül AllowedRolesCsv ile filtrelenir
        var perm = new Permission { Key = "menu.x.view" };
        ctx.Permissions.Add(perm);
        ctx.SaveChanges();

        var mod = new MenuModule { Slug = "test", Label = "Test", SortOrder = 1, AllowedRolesCsv = "Student" };
        ctx.MenuModules.Add(mod);
        ctx.SaveChanges();

        ctx.MenuItems.Add(new MenuItem
        {
            ModuleId = mod.Id,
            Label = "Item",
            Route = "/x",
            Slug = "x",
            MenuItemPermissions = new List<MenuItemPermission> { new() { PermissionId = perm.Id } }
        });
        ctx.SaveChanges();

        // Kullanıcı Instructor rolünde, permission'ı VAR ama modül AllowedRolesCsv "Student" → modül gizlenir
        var svc = CreateService(ctx, new HashSet<string> { "menu.x.view" }, userRoles: new[] { "Instructor" });

        var menu = await svc.GetUserMenuAsync(1);
        menu.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserMenuAsync_ShowsModule_WhenAllowedRolesCsvMatches()
    {
        using var ctx = CreateContext();
        var perm = new Permission { Key = "menu.x.view" };
        ctx.Permissions.Add(perm);
        ctx.SaveChanges();

        var mod = new MenuModule { Slug = "test", Label = "Test", SortOrder = 1, AllowedRolesCsv = "Student,Instructor" };
        ctx.MenuModules.Add(mod);
        ctx.SaveChanges();

        ctx.MenuItems.Add(new MenuItem
        {
            ModuleId = mod.Id,
            Label = "Item",
            Route = "/x",
            Slug = "x",
            MenuItemPermissions = new List<MenuItemPermission> { new() { PermissionId = perm.Id } }
        });
        ctx.SaveChanges();

        var svc = CreateService(ctx, new HashSet<string> { "menu.x.view" }, userRoles: new[] { "Instructor" });

        var menu = await svc.GetUserMenuAsync(1);
        menu.Should().HaveCount(1);
    }
}
