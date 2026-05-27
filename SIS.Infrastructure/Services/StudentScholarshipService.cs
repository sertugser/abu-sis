using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class StudentScholarshipService : IStudentScholarshipService
{
    private readonly AppDbContext _context;

    public StudentScholarshipService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ScholarshipDetailDto>?> GetForStudentAsync(int studentId, CancellationToken ct = default)
    {
        var studentExists = await _context.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId, ct);
        if (!studentExists)
            return null;

        var rows = await _context.StudentScholarshipDetails
            .AsNoTracking()
            .Where(d => d.StudentId == studentId)
            .OrderByDescending(d => d.IsActive)
            .ThenByDescending(d => d.StartDate)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<int> CreateAsync(int studentId, UpsertScholarshipDetailDto dto, CancellationToken ct = default)
    {
        // Validate before any mutation.
        var type = ParseType(dto.Type);
        ValidateRate(dto.RatePercent);
        ValidateDates(dto.StartDate, dto.EndDate);

        var studentExists = await _context.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId, ct);
        if (!studentExists)
            throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");

        var entity = new StudentScholarshipDetail
        {
            StudentId = studentId,
            Type = type,
            RatePercent = dto.RatePercent,
            Source = dto.Source,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive,
            Note = dto.Note,
            CreatedAt = DateTime.UtcNow,
        };
        _context.StudentScholarshipDetails.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(int studentId, int id, UpsertScholarshipDetailDto dto, CancellationToken ct = default)
    {
        // Validate before any mutation.
        var type = ParseType(dto.Type);
        ValidateRate(dto.RatePercent);
        ValidateDates(dto.StartDate, dto.EndDate);

        var entity = await _context.StudentScholarshipDetails
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null || entity.StudentId != studentId)
            throw new KeyNotFoundException($"Burs kaydı bulunamadı: {id}");

        entity.Type = type;
        entity.RatePercent = dto.RatePercent;
        entity.Source = dto.Source;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.IsActive = dto.IsActive;
        entity.Note = dto.Note;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int studentId, int id, CancellationToken ct = default)
    {
        var entity = await _context.StudentScholarshipDetails
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null || entity.StudentId != studentId)
            throw new KeyNotFoundException($"Burs kaydı bulunamadı: {id}");

        _context.StudentScholarshipDetails.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    private static ScholarshipType ParseType(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<ScholarshipType>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Geçersiz burs tipi değeri: {value}");
    }

    private static void ValidateRate(decimal? ratePercent)
    {
        if (ratePercent.HasValue && (ratePercent.Value < 0 || ratePercent.Value > 100))
            throw new InvalidOperationException($"Burs oranı 0 ile 100 arasında olmalıdır: {ratePercent.Value}");
    }

    private static void ValidateDates(DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            throw new InvalidOperationException("Bitiş tarihi başlangıç tarihinden önce olamaz.");
    }

    private static ScholarshipDetailDto ToDto(StudentScholarshipDetail d) => new(
        Id: d.Id,
        Type: d.Type.ToString(),
        TypeLabel: ScholarshipTypeLabels.Of(d.Type),
        RatePercent: d.RatePercent,
        Source: d.Source,
        StartDate: d.StartDate,
        EndDate: d.EndDate,
        IsActive: d.IsActive,
        Note: d.Note);
}
