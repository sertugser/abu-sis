using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using SIS.Application.Auth.DTOs;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WhenPasswordRotationExpired_ReturnsPasswordChangeRequired()
    {
        var user = new ApplicationUser
        {
            Id = 42,
            Email = "instructor@ankarabilim.edu.tr",
            UserName = "instructor@ankarabilim.edu.tr",
            FirstName = "Ada",
            LastName = "Lovelace",
            PasswordChangedAtUtc = DateTime.UtcNow.AddMonths(-8)
        };

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
        userManager.Setup(x => x.CheckPasswordAsync(user, "Valid123!")).ReturnsAsync(true);
        userManager.Setup(x => x.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Instructor" });

        var config = CreateSystemConfigMock();
        config.Setup(x => x.GetBoolAsync("Security.PasswordRotationEnabled", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        config.Setup(x => x.GetIntAsync("Security.PasswordRotationMonths", 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);

        var service = new AuthService(userManager.Object, CreateConfiguration(), config.Object);

        var response = await service.LoginAsync(new LoginDto
        {
            Email = user.Email,
            Password = "Valid123!"
        });

        response.Success.Should().BeTrue();
        response.PasswordChangeRequired.Should().BeTrue();
        response.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePasswordAsync_UpdatesTimestamp_AndClearsPasswordChangeRequirement()
    {
        var user = new ApplicationUser
        {
            Id = 42,
            Email = "instructor@ankarabilim.edu.tr",
            UserName = "instructor@ankarabilim.edu.tr",
            FirstName = "Ada",
            LastName = "Lovelace",
            PasswordChangedAtUtc = DateTime.UtcNow.AddMonths(-8)
        };

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByIdAsync("42")).ReturnsAsync(user);
        userManager.Setup(x => x.ChangePasswordAsync(user, "Old123!", "NewPassword123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Instructor" });

        var service = new AuthService(userManager.Object, CreateConfiguration(), CreateSystemConfigMock().Object);

        var response = await service.ChangePasswordAsync(42, new ChangePasswordDto
        {
            CurrentPassword = "Old123!",
            NewPassword = "NewPassword123!"
        });

        response.Success.Should().BeTrue();
        response.PasswordChangeRequired.Should().BeFalse();
        user.PasswordChangedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserPasswordStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-test-key-that-is-long-enough",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiresInMinutes"] = "60"
            })
            .Build();

    private static Mock<ISystemConfigProvider> CreateSystemConfigMock()
    {
        var mock = new Mock<ISystemConfigProvider>();
        mock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);
        return mock;
    }
}
