using FluentAssertions;
using Moq;
using SIS.Application.Identity.Services;
using SIS.Application.Reports;
using SIS.Infrastructure.Services.Reports;

namespace SIS.Tests.Reports;

public class ReportExecutionServiceTests
{
    private sealed class FakeHandler : IReportHandler
    {
        public string Code => "fake";
        public ReportParameterSchema GetParameterSchema() =>
            new(new[] { new ReportParameter("q", "Ara", ReportParameterType.Text) });
        public Task<ReportResult> RunAsync(ReportRunRequest r, ReportExecutionContext c, CancellationToken ct) =>
            Task.FromResult(new ReportResult(
                new[] { new ReportColumn("a", "A") },
                new List<IReadOnlyList<object?>> { new object?[] { "x" } }, 1));
    }

    private static ReportExecutionService Build(bool canAccess, out Mock<IReportAuthorizationService> authz)
    {
        authz = new Mock<IReportAuthorizationService>();
        authz.Setup(a => a.CanAccessReportAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(canAccess);

        var scope = new Mock<ICurrentUserScope>();
        scope.SetupGet(s => s.IsActive).Returns(false);
        scope.SetupGet(s => s.FacultyIds).Returns(Array.Empty<int>());

        var roles = new Mock<IUserRoleReader>();
        roles.Setup(r => r.GetRolesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { "Dean" });

        return new ReportExecutionService(new IReportHandler[] { new FakeHandler() }, authz.Object, scope.Object, roles.Object);
    }

    [Fact]
    public async Task Run_Throws_WhenNoAccess()
    {
        var svc = Build(canAccess: false, out _);
        var act = () => svc.RunAsync(1, "fake", new ReportRunRequest(new Dictionary<string, string?>()));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Run_Throws_WhenUnknownCode()
    {
        var svc = Build(canAccess: true, out _);
        var act = () => svc.RunAsync(1, "yok", new ReportRunRequest(new Dictionary<string, string?>()));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Run_ReturnsResult_WhenAuthorized()
    {
        var svc = Build(canAccess: true, out _);
        var res = await svc.RunAsync(1, "fake", new ReportRunRequest(new Dictionary<string, string?>(), Page: 1, PageSize: 10));
        res.Columns.Should().ContainSingle(c => c.Key == "a");
        res.Rows.Should().HaveCount(1);
        res.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetSchema_ReturnsNull_WhenNoAccess()
    {
        var svc = Build(canAccess: false, out _);
        (await svc.GetSchemaAsync(1, "fake")).Should().BeNull();
    }
}
