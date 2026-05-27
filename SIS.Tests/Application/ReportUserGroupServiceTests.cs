using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Identity.DTOs;
using SIS.Application.Reports;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class ReportUserGroupServiceTests
{
    private static AppDbContext Ctx() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReportGrpSvc-{Guid.NewGuid()}").Options);

    private static ReportAuthorizationService Svc(AppDbContext ctx)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userMgr = new Mock<UserManager<ApplicationUser>>(userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var roleStore = new Mock<IRoleStore<IdentityRole<int>>>();
        var roleMgr = new Mock<RoleManager<IdentityRole<int>>>(roleStore.Object, null!, null!, null!, null!);
        var resolver = new Mock<IReportAccessResolver>();
        return new ReportAuthorizationService(ctx, userMgr.Object, roleMgr.Object, resolver.Object);
    }

    [Fact]
    public async Task CreateGroup_Then_AddAndRemoveMember()
    {
        using var ctx = Ctx();
        var svc = Svc(ctx);

        var group = await svc.CreateGroupAsync(new CreateReportUserGroupDto("Fakülte Sekreterleri", null));
        group.Id.Should().BeGreaterThan(0);

        await svc.AddGroupMemberAsync(group.Id, userId: 42);
        (await ctx.ReportUserGroupMembers.CountAsync(m => m.GroupId == group.Id)).Should().Be(1);

        // Idempotent: aynı üye tekrar eklenmez
        await svc.AddGroupMemberAsync(group.Id, userId: 42);
        (await ctx.ReportUserGroupMembers.CountAsync(m => m.GroupId == group.Id)).Should().Be(1);

        await svc.RemoveGroupMemberAsync(group.Id, userId: 42);
        (await ctx.ReportUserGroupMembers.CountAsync(m => m.GroupId == group.Id)).Should().Be(0);
    }

    [Fact]
    public async Task CreateGroup_Throws_OnDuplicateName()
    {
        using var ctx = Ctx();
        var svc = Svc(ctx);
        await svc.CreateGroupAsync(new CreateReportUserGroupDto("Grup A", null));
        var act = () => svc.CreateGroupAsync(new CreateReportUserGroupDto("Grup A", null));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetGrant_Upserts_OnSameTargetSubject()
    {
        using var ctx = Ctx();
        ctx.Roles.Add(new IdentityRole<int> { Id = 10, Name = "Dean", NormalizedName = "DEAN" });
        ctx.ReportCategories.Add(new ReportCategory { Id = 1, Code = "c1", Name = "Kategori 1" });
        await ctx.SaveChangesAsync();
        var svc = Svc(ctx);

        await svc.SetGrantAsync(new SetReportAccessGrantDto(ScopeKind: 0, TargetId: 1, SubjectKind: 0, SubjectId: 10, Effect: 0, Reason: null), grantedByUserId: 1);
        await svc.SetGrantAsync(new SetReportAccessGrantDto(ScopeKind: 0, TargetId: 1, SubjectKind: 0, SubjectId: 10, Effect: 1, Reason: "değişti"), grantedByUserId: 1);

        var grants = await ctx.ReportAccessGrants.Where(g => g.TargetId == 1 && g.SubjectId == 10).ToListAsync();
        grants.Should().HaveCount(1);
        grants[0].Effect.Should().Be(ReportGrantEffect.Deny);
    }
}
