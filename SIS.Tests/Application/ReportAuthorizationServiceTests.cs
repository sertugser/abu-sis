using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Identity.DTOs;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

/// <summary>
/// Validation sınır testleri: girdi uzunlukları DB kolon limitleriyle (Code 64/100, Name 200,
/// Description 500, Reason 500) hizalı olmalı; aşımlar yakalanmamış 500 yerine temiz
/// InvalidOperationException (→ 400) üretmeli.
/// </summary>
public class ReportAuthorizationServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReportAuthSvc-{Guid.NewGuid()}")
            .Options);

    private static ReportAuthorizationService CreateService(AppDbContext ctx)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userMgr = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var roleStore = new Mock<IRoleStore<IdentityRole<int>>>();
        var roleMgr = new Mock<RoleManager<IdentityRole<int>>>(
            roleStore.Object, null!, null!, null!, null!);
        var resolver = new Mock<SIS.Application.Reports.IReportAccessResolver>();
        return new ReportAuthorizationService(ctx, userMgr.Object, roleMgr.Object, resolver.Object);
    }

    // ── Kategori ──────────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_Throws_WhenCodeExceeds64()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var dto = new CreateReportCategoryDto(new string('a', 65), "Ad");

        var act = () => svc.CreateCategoryAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*64*");
    }

    [Fact]
    public async Task CreateCategory_Throws_WhenNameExceeds200()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var dto = new CreateReportCategoryDto("academic", new string('x', 201));

        var act = () => svc.CreateCategoryAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*200*");
    }

    [Fact]
    public async Task CreateCategory_Succeeds_AtBoundaryLengths()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var dto = new CreateReportCategoryDto(new string('a', 64), new string('x', 200));

        var result = await svc.CreateCategoryAsync(dto);

        result.Code.Should().HaveLength(64);
        result.Name.Should().HaveLength(200);
    }

    [Fact]
    public async Task CreateCategory_Throws_WhenCodeHasUppercase()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var dto = new CreateReportCategoryDto("Academic", "Ad");

        var act = () => svc.CreateCategoryAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*küçük harf*");
    }

    // ── Rapor ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateReport_Throws_WhenDescriptionExceeds500()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var dto = new CreateReportDefinitionDto("ogrenci-listesi", "Öğrenci Listesi", new string('d', 501), 1);

        var act = () => svc.CreateReportAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*500*");
    }

    [Fact]
    public async Task CreateReport_Throws_WhenCodeExceeds100()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var dto = new CreateReportDefinitionDto(new string('a', 101), "Ad", null, 1);

        var act = () => svc.CreateReportAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*100*");
    }
}
