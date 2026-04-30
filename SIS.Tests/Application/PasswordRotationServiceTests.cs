using FluentAssertions;
using Moq;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class PasswordRotationServiceTests
{
    [Fact]
    public async Task GetStatusAsync_WhenRotationDisabled_DoesNotRequirePasswordChange()
    {
        var configMock = new Mock<ISystemConfigProvider>(MockBehavior.Strict);
        configMock
            .Setup(service => service.GetBoolAsync("Security.PasswordRotationEnabled", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        configMock
            .Setup(service => service.GetIntAsync("Security.PasswordRotationMonths", 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);

        var clock = new FakeClock { UtcNow = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc) };
        var service = new PasswordRotationService(configMock.Object, clock);

        var status = await service.GetStatusAsync(new ApplicationUser
        {
            PasswordChangedAtUtc = clock.UtcNow.AddMonths(-12)
        });

        status.IsEnabled.Should().BeFalse();
        status.RequiresPasswordChange.Should().BeFalse();
        status.PasswordExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task GetStatusAsync_WhenPasswordExpired_RequiresPasswordChange()
    {
        var configMock = new Mock<ISystemConfigProvider>(MockBehavior.Strict);
        configMock
            .Setup(service => service.GetBoolAsync("Security.PasswordRotationEnabled", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        configMock
            .Setup(service => service.GetIntAsync("Security.PasswordRotationMonths", 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);

        var clock = new FakeClock { UtcNow = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc) };
        var service = new PasswordRotationService(configMock.Object, clock);

        var status = await service.GetStatusAsync(new ApplicationUser
        {
            PasswordChangedAtUtc = new DateTime(2025, 10, 29, 0, 0, 0, DateTimeKind.Utc)
        });

        status.IsEnabled.Should().BeTrue();
        status.RequiresPasswordChange.Should().BeTrue();
        status.PasswordExpiresAtUtc.Should().Be(new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc));
    }
}
