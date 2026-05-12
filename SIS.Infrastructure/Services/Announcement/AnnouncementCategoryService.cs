using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Announcement.DTOs;
using SIS.Application.Announcement.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Announcement;

public sealed partial class AnnouncementCategoryService : IAnnouncementCategoryService
{
    private readonly AppDbContext _context;

    public AnnouncementCategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AnnouncementCategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _context.AnnouncementCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
        return items.Select(MapToDto).ToList();
    }

    public async Task<AnnouncementCategoryDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.AnnouncementCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<AnnouncementCategoryDto> CreateAsync(CreateAnnouncementCategoryDto dto, CancellationToken ct = default)
    {
        var (name, color, icon) = ValidateAndNormalize(dto.Name, dto.ColorHex, dto.IconName);

        var nameExists = await _context.AnnouncementCategories
            .AnyAsync(c => c.Name == name, ct);
        if (nameExists)
            throw new InvalidOperationException($"'{name}' adında bir kategori zaten var.");

        var entity = new AnnouncementCategory
        {
            Name      = name,
            ColorHex  = color,
            IconName  = icon,
            SortOrder = dto.SortOrder,
            IsSystem  = false,
            CreatedAt = DateTime.UtcNow,
        };

        _context.AnnouncementCategories.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<AnnouncementCategoryDto?> UpdateAsync(int id, UpdateAnnouncementCategoryDto dto, CancellationToken ct = default)
    {
        var entity = await _context.AnnouncementCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return null;

        var (name, color, icon) = ValidateAndNormalize(dto.Name, dto.ColorHex, dto.IconName);

        if (!string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await _context.AnnouncementCategories
                .AnyAsync(c => c.Id != id && c.Name == name, ct);
            if (conflict)
                throw new InvalidOperationException($"'{name}' adında bir kategori zaten var.");
        }

        entity.Name      = name;
        entity.ColorHex  = color;
        entity.IconName  = icon;
        entity.SortOrder = dto.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.AnnouncementCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return false;

        if (entity.IsSystem)
            throw new InvalidOperationException("Sistem kategorileri silinemez.");

        _context.AnnouncementCategories.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Validation ───────────────────────────────────────────────────────────

    private static (string Name, string Color, string Icon) ValidateAndNormalize(
        string? rawName, string? rawColor, string? rawIcon)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            throw new InvalidOperationException("Kategori adı boş olamaz.");

        var name = rawName.Trim();
        if (name.Length > 100)
            throw new InvalidOperationException("Kategori adı en fazla 100 karakter olabilir.");

        var color = string.IsNullOrWhiteSpace(rawColor) ? "#3b82f6" : rawColor.Trim();
        if (!HexColorRegex().IsMatch(color))
            throw new InvalidOperationException("Renk kodu HEX formatında olmalıdır (örn. #3b82f6).");

        var icon = (rawIcon ?? string.Empty).Trim();
        if (icon.Length > 50)
            throw new InvalidOperationException("Icon adı en fazla 50 karakter olabilir.");

        return (name, color, icon);
    }

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    private static AnnouncementCategoryDto MapToDto(AnnouncementCategory e) =>
        new(e.Id, e.Name, e.ColorHex, e.IconName, e.SortOrder, e.IsSystem, e.CreatedAt, e.UpdatedAt);
}
