using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SIS.API.Security;

public static class StartupSecurityValidator
{
    private const int MinimumJwtKeyBytes = 32;

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ValidateJwt(configuration);
        ValidateCors(configuration, environment);
        ValidateDatabaseConfiguration(configuration, environment);
        ValidateAllowedHosts(configuration, environment);
    }

    public static string GetRequiredJwtKey(IConfiguration configuration) =>
        configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key configuration is required.");

    private static void ValidateJwt(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Jwt:Key configuration is required.");

        if (key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Jwt:Key must be replaced before the API starts.");

        if (Encoding.UTF8.GetByteCount(key) < MinimumJwtKeyBytes)
            throw new InvalidOperationException($"Jwt:Key must be at least {MinimumJwtKeyBytes} bytes.");

        if (string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"]))
            throw new InvalidOperationException("Jwt:Issuer configuration is required.");

        if (string.IsNullOrWhiteSpace(configuration["Jwt:Audience"]))
            throw new InvalidOperationException("Jwt:Audience configuration is required.");

        if (!int.TryParse(configuration["Jwt:ExpiresInMinutes"], out var expiresInMinutes)
            || expiresInMinutes is < 5 or > 1440)
        {
            throw new InvalidOperationException("Jwt:ExpiresInMinutes must be between 5 and 1440 minutes.");
        }
    }

    private static void ValidateCors(IConfiguration configuration, IHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
            throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one explicit origin.");

        if (!environment.IsDevelopment()
            && origins.Any(origin => origin.Contains('*', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Wildcard CORS origins are not allowed outside Development.");
        }
    }

    private static void ValidateDatabaseConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required.");

        if (!environment.IsDevelopment()
            && connectionString.Contains("Password=215215", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Default development database password cannot be used outside Development.");
        }
    }

    private static void ValidateAllowedHosts(IConfiguration configuration, IHostEnvironment environment)
    {
        var allowedHosts = configuration["AllowedHosts"];
        if (!environment.IsDevelopment()
            && (string.IsNullOrWhiteSpace(allowedHosts) || string.Equals(allowedHosts, "*", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("AllowedHosts must be restricted outside Development.");
        }
    }
}
