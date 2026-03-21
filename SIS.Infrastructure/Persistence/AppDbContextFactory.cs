using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SIS.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var connectionString = ResolveConnectionString();

        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString()
    {
        var environmentConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            return environmentConnectionString;
        }

        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        var appSettingsPathCandidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            appSettingsPathCandidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "SIS.API", $"appsettings.{environmentName}.json"));
            appSettingsPathCandidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "SIS.API", $"appsettings.{environmentName}.json"));
            appSettingsPathCandidates.Add(Path.Combine(AppContext.BaseDirectory, $"appsettings.{environmentName}.json"));
        }

        appSettingsPathCandidates.AddRange(
        [
            Path.Combine(Directory.GetCurrentDirectory(), "SIS.API", "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "SIS.API", "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        ]);

        foreach (var path in appSettingsPathCandidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                && connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection)
                && !string.IsNullOrWhiteSpace(defaultConnection.GetString()))
            {
                return defaultConnection.GetString()!;
            }
        }

        throw new InvalidOperationException("DefaultConnection bulunamadi. Design-time migration icin appsettings.json veya ConnectionStrings__DefaultConnection gerekli.");
    }
}
