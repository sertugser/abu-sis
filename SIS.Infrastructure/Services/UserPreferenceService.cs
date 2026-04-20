using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.UserPreferences.DTOs;
using SIS.Application.UserPreferences.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class UserPreferenceService : IUserPreferenceService
{
    private const int MaxFavoritesCount = 11;
    private const int MaxRouteLength = 200;

    private readonly AppDbContext _context;

    public UserPreferenceService(AppDbContext context)
    {
        _context = context;
    }

    public int MaxFavorites => MaxFavoritesCount;

    public async Task<UserPreferenceDto> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var pref = await _context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (pref is null)
            return new UserPreferenceDto(Array.Empty<string>(), HasCustomized: false);

        return new UserPreferenceDto(DeserializeFavorites(pref.FavoritesJson), HasCustomized: true);
    }

    public async Task<UserPreferenceDto> UpdateAsync(
        int userId,
        UpdateUserPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var favorites = ValidateAndNormalizeFavorites(request.Favorites);
        var payload = JsonSerializer.Serialize(favorites);

        var pref = await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (pref is null)
        {
            pref = new UserPreference
            {
                UserId = userId,
                FavoritesJson = payload,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.UserPreferences.Add(pref);
        }
        else
        {
            pref.FavoritesJson = payload;
            pref.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new UserPreferenceDto(favorites, HasCustomized: true);
    }

    private string[] ValidateAndNormalizeFavorites(string[] favorites)
    {
        if (favorites is null)
            throw new ArgumentException("Favori listesi null olamaz.", nameof(favorites));

        if (favorites.Length > MaxFavoritesCount)
            throw new ArgumentException(
                $"Favori sayisi en fazla {MaxFavoritesCount} olabilir.",
                nameof(favorites));

        var normalized = new List<string>(favorites.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in favorites)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new ArgumentException("Favori route bos olamaz.", nameof(favorites));

            var trimmed = raw.Trim();

            if (trimmed.Length > MaxRouteLength)
                throw new ArgumentException(
                    $"Favori route uzunlugu {MaxRouteLength} karakteri asamaz.",
                    nameof(favorites));

            if (!trimmed.StartsWith('/'))
                throw new ArgumentException(
                    "Favori route '/' ile baslamali.",
                    nameof(favorites));

            if (!seen.Add(trimmed))
                continue;

            normalized.Add(trimmed);
        }

        return normalized.ToArray();
    }

    private static string[] DeserializeFavorites(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            // Corrupt payload — return empty so caller can overwrite safely.
            return Array.Empty<string>();
        }
    }
}
