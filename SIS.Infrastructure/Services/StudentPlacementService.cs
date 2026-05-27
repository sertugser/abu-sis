using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class StudentPlacementService : IStudentPlacementService
{
    private const int MinPlacementYear = 1980;

    private readonly AppDbContext _context;

    public StudentPlacementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<StudentPlacementDto?> GetAsync(int studentId, CancellationToken ct = default)
    {
        var studentExists = await _context.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId, ct);
        if (!studentExists)
            return null;

        var placement = await _context.StudentPlacements
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId, ct);

        // No row yet → return an "empty" but valid DTO.
        if (placement is null)
            return EmptyDto();

        return ToDto(placement);
    }

    public async Task UpsertAsync(int studentId, UpsertStudentPlacementDto dto, CancellationToken ct = default)
    {
        var studentExists = await _context.Students
            .AnyAsync(s => s.Id == studentId, ct);
        if (!studentExists)
            throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");

        // Validate before any mutation so an invalid value aborts cleanly.
        var entranceType = ParseEnum<StudentEntranceType>(dto.EntranceType, "giriş türü");
        ValidatePlacementYear(dto.PlacementYear);

        var placement = await _context.StudentPlacements
            .FirstOrDefaultAsync(p => p.StudentId == studentId, ct);

        var now = DateTime.UtcNow;
        if (placement is null)
        {
            // New row: stamp CreatedAt on insert, leave UpdatedAt null.
            placement = new StudentPlacement
            {
                StudentId = studentId,
                CreatedAt = now,
            };
            _context.StudentPlacements.Add(placement);
        }
        else
        {
            placement.UpdatedAt = now;
        }

        placement.EntranceType = entranceType;
        placement.ScoreType = dto.ScoreType;
        placement.Score = dto.Score;
        placement.RankOrder = dto.RankOrder;
        placement.QuotaType = dto.QuotaType;
        placement.PlacementYear = dto.PlacementYear;
        placement.PreferenceOrder = dto.PreferenceOrder;
        placement.PlacedProgramName = dto.PlacedProgramName;
        placement.Note = dto.Note;

        await _context.SaveChangesAsync(ct);
    }

    private static void ValidatePlacementYear(int? placementYear)
    {
        if (!placementYear.HasValue)
            return;

        var maxYear = DateTime.UtcNow.Year + 1;
        if (placementYear.Value < MinPlacementYear || placementYear.Value > maxYear)
            throw new InvalidOperationException(
                $"Yerleştirme yılı {MinPlacementYear} ile {maxYear} arasında olmalıdır: {placementYear.Value}");
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        throw new InvalidOperationException($"Geçersiz {fieldName} değeri: {value}");
    }

    private static StudentPlacementDto EmptyDto() => new(
        Id: null,
        EntranceType: null,
        EntranceTypeLabel: null,
        ScoreType: null,
        Score: null,
        RankOrder: null,
        QuotaType: null,
        PlacementYear: null,
        PreferenceOrder: null,
        PlacedProgramName: null,
        Note: null);

    private static StudentPlacementDto ToDto(StudentPlacement p) => new(
        Id: p.Id,
        EntranceType: p.EntranceType?.ToString(),
        EntranceTypeLabel: p.EntranceType.HasValue ? EntranceTypeLabels.Of(p.EntranceType.Value) : null,
        ScoreType: p.ScoreType,
        Score: p.Score,
        RankOrder: p.RankOrder,
        QuotaType: p.QuotaType,
        PlacementYear: p.PlacementYear,
        PreferenceOrder: p.PreferenceOrder,
        PlacedProgramName: p.PlacedProgramName,
        Note: p.Note);
}
