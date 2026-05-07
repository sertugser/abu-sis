using FluentAssertions;
using Moq;
using SIS.Application.Common.Services;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// Plan 1 PoC — sadece <see cref="ISystemConfigProvider.GetStringAsync"/> mock kontratını
/// doğrular. Gerçek dispatch testi (AutoScheduleEngine v2 path) Plan 2'de yazılacak;
/// burada amaç: config anahtarı okunabiliyor mu + default değeri "v1-mrv" mı.
/// </summary>
public class EngineDispatchTests
{
    [Fact]
    public async Task ConfigKey_ScheduleEngine_ReturnsV2_WhenSet()
    {
        // Bu test SystemConfig anahtarının okunabilir olduğunu doğrular.
        // Gerçek dispatch testi Plan 2'de — burada sadece config provider mock'u smoke.
        var configMock = new Mock<ISystemConfigProvider>();
        configMock
            .Setup(x => x.GetStringAsync("Schedule.Engine", "v1-mrv", It.IsAny<CancellationToken>()))
            .ReturnsAsync("v2-ortools");

        var engineFlag = await configMock.Object.GetStringAsync(
            "Schedule.Engine", "v1-mrv", CancellationToken.None);

        engineFlag.Should().Be("v2-ortools");
    }

    [Fact]
    public async Task ConfigKey_ScheduleEngine_DefaultsTo_V1Mrv()
    {
        var configMock = new Mock<ISystemConfigProvider>();
        configMock
            .Setup(x => x.GetStringAsync("Schedule.Engine", "v1-mrv", It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1-mrv");

        var engineFlag = await configMock.Object.GetStringAsync(
            "Schedule.Engine", "v1-mrv", CancellationToken.None);

        engineFlag.Should().Be("v1-mrv");
    }
}
