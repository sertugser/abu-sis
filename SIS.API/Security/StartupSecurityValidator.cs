using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SIS.API.Security;

public static class StartupSecurityValidator
{
    private const int MinimumJwtKeyBytes = 32;
    private static readonly HashSet<string> KnownCompromisedJwtKeyHashes =
    [
        "9054c06d3cf9d9b392e5b2cfecd2ffce0ea6437d88fdc8542b8a55fd54a30d1a"
    ];

    private static readonly HashSet<string> KnownWeakDatabasePasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456",
        "215215",
    };

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

        if (key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || key.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Jwt:Key must be replaced before the API starts.");

        if (Encoding.UTF8.GetByteCount(key) < MinimumJwtKeyBytes)
            throw new InvalidOperationException($"Jwt:Key must be at least {MinimumJwtKeyBytes} bytes.");

        if (KnownCompromisedJwtKeyHashes.Contains(Sha256Hex(key.Trim())))
            throw new InvalidOperationException("Jwt:Key is a known compromised value and must be rotated.");

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

        var password = ExtractConnectionStringPassword(connectionString);
        if (password is not null && KnownWeakDatabasePasswords.Contains(password))
        {
            throw new InvalidOperationException("Default development database password cannot be used.");
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

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string? ExtractConnectionStringPassword(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = part[..separatorIndex].Trim();
            if (!key.Equals("Password", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("Pwd", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return part[(separatorIndex + 1)..].Trim();
        }

        return null;
    }
}
