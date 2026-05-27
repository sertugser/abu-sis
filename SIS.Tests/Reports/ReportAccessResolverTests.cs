using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Reports;

namespace SIS.Tests.Reports;

public class ReportAccessResolverTests
{
    private static AppDbContext Ctx() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReportAccess-{Guid.NewGuid()}").Options);

    // userId=1 → roller {Dean(rol id 10)}; gruplar {grup 100}
    private static ReportAccessResolver Build(AppDbContext ctx, bool isAdmin = false)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userMgr = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userMgr.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
               .ReturnsAsync(new ApplicationUser { Id = 1 });
        userMgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(isAdmin ? new[] { "SystemAdministrator" } : new[] { "Dean" });
        return new ReportAccessResolver(ctx, userMgr.Object);
    }

    private static async Task SeedBaseAsync(AppDbContext ctx)
    {
        ctx.Roles.Add(new IdentityRole<int> { Id = 10, Name = "Dean", NormalizedName = "DEAN" });
        ctx.ReportCategories.Add(new ReportCategory { Id = 1, Code = "c1", Name = "Kategori 1" });
        ctx.ReportDefinitions.Add(new ReportDefinition { Id = 5, Code = "r5", Name = "Rapor 5", CategoryId = 1 });
        ctx.ReportUserGroups.Add(new ReportUserGroup { Id = 100, Name = "Grup A" });
        ctx.ReportUserGroupMembers.Add(new ReportUserGroupMember { GroupId = 100, UserId = 1 });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task CategoryAllow_ForRole_GrantsAccess()
    {
        using var ctx = Ctx();
        await SeedBaseAsync(ctx);
        ctx.ReportAccessGrants.Add(new ReportAccessGrant
        { ScopeKind = ReportGrantScopeKind.Category, TargetId = 1, SubjectKind = ReportGrantSubjectKind.Role, SubjectId = 10, Effect = ReportGrantEffect.Allow });
        await ctx.SaveChangesAsync();

        var ids = await Build(ctx).AccessibleReportIdsAsync(1);
        ids.Should().Contain(5);
    }

    [Fact]
    public async Task ReportDeny_OverridesCategoryAllow()
    {
        using var ctx = Ctx();
        await SeedBaseAsync(ctx);
        ctx.ReportAccessGrants.AddRange(
            new ReportAccessGrant { ScopeKind = ReportGrantScopeKind.Category, TargetId = 1, SubjectKind = ReportGrantSubjectKind.Role, SubjectId = 10, Effect = ReportGrantEffect.Allow },
            new ReportAccessGrant { ScopeKind = ReportGrantScopeKind.Report, TargetId = 5, SubjectKind = ReportGrantSubjectKind.Role, SubjectId = 10, Effect = ReportGrantEffect.Deny });
        await ctx.SaveChangesAsync();

        var ids = await Build(ctx).AccessibleReportIdsAsync(1);
        ids.Should().NotContain(5);
    }

    [Fact]
    public async Task GroupAllow_GrantsAccess()
    {
        using var ctx = Ctx();
        await SeedBaseAsync(ctx);
        ctx.ReportAccessGrants.Add(new ReportAccessGrant
        { ScopeKind = ReportGrantScopeKind.Report, TargetId = 5, SubjectKind = ReportGrantSubjectKind.Group, SubjectId = 100, Effect = ReportGrantEffect.Allow });
        await ctx.SaveChangesAsync();

        (await Build(ctx).CanAccessAsync(1, "r5")).Should().BeTrue();
    }

    [Fact]
    public async Task UserDeny_OverridesGroupAllow()
    {
        using var ctx = Ctx();
        await SeedBaseAsync(ctx);
        ctx.ReportAccessGrants.AddRange(
            new ReportAccessGrant { ScopeKind = ReportGrantScopeKind.Report, TargetId = 5, SubjectKind = ReportGrantSubjectKind.Group, SubjectId = 100, Effect = ReportGrantEffect.Allow },
            new ReportAccessGrant { ScopeKind = ReportGrantScopeKind.Report, TargetId = 5, SubjectKind = ReportGrantSubjectKind.User, SubjectId = 1, Effect = ReportGrantEffect.Deny });
        await ctx.SaveChangesAsync();

        (await Build(ctx).CanAccessAsync(1, "r5")).Should().BeFalse();
    }

    [Fact]
    public async Task SystemAdmin_AlwaysHasAccess()
    {
        using var ctx = Ctx();
        await SeedBaseAsync(ctx);
        (await Build(ctx, isAdmin: true).CanAccessAsync(1, "r5")).Should().BeTrue();
        (await Build(ctx, isAdmin: true).AccessibleReportIdsAsync(1)).Should().Contain(5);
    }

    [Fact]
    public async Task NoGrant_DeniesAccess()
    {
        using var ctx = Ctx();
        await SeedBaseAsync(ctx);
        (await Build(ctx).CanAccessAsync(1, "r5")).Should().BeFalse();
    }
}
