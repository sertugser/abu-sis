using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using SIS.API.Security;

namespace SIS.Tests.API;

public class StartupSecurityValidatorTests
{
    [Fact]
    public void Validate_Throws_WhenJwtKeyIsPlaceholder()
    {
        var configuration = BuildConfiguration(jwtKey: "CHANGE_ME_USE_AT_LEAST_32_CHARS!!");

        var act = () => StartupSecurityValidator.Validate(configuration, Environment("Development"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Key*");
    }

    [Fact]
    public void Validate_Throws_WhenProductionCorsUsesWildcard()
    {
        var configuration = BuildConfiguration(corsOrigin: "https://*.example.edu");

        var act = () => StartupSecurityValidator.Validate(configuration, Environment("Production"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Wildcard CORS*");
    }

    [Fact]
    public void Validate_Throws_WhenProductionAllowedHostsIsWildcard()
    {
        var configuration = BuildConfiguration(allowedHosts: "*");

        var act = () => StartupSecurityValidator.Validate(configuration, Environment("Production"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AllowedHosts*");
    }

    [Fact]
    public void Validate_Allows_StrongDevelopmentConfiguration()
    {
        var configuration = BuildConfiguration();

        var act = () => StartupSecurityValidator.Validate(configuration, Environment("Development"));

        act.Should().NotThrow();
    }

    private static IConfiguration BuildConfiguration(
        string jwtKey = "0123456789abcdef0123456789abcdef",
        string corsOrigin = "http://localhost:5173",
        string allowedHosts = "localhost") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=sis;Username=postgres;Password=local-only",
                ["Jwt:Key"] = jwtKey,
                ["Jwt:Issuer"] = "StudentInformationSystem",
                ["Jwt:Audience"] = "StudentInformationSystem",
                ["Jwt:ExpiresInMinutes"] = "60",
                ["Cors:AllowedOrigins:0"] = corsOrigin,
                ["AllowedHosts"] = allowedHosts,
            })
            .Build();

    private static IHostEnvironment Environment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
