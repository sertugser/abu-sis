using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class OsymRegistrationCalendarService(AppDbContext ctx, IDateTimeProvider clock) : IOsymRegistrationCalendarService
{
    private readonly AppDbContext _ctx = ctx;
    private readonly IDateTimeProvider _clock = clock;

    public async Task<IReadOnlyList<OsymRegistrationCalendarDto>> GetAllAsync(int? year = null, CancellationToken ct = default)
    {
        var query = _ctx.OsymRegistrationCalendars.AsNoTracking();
        if (year.HasValue) query = query.Where(c => c.Year == year.Value);
        var list = await query
            .OrderByDescending(c => c.Year)
            .ThenBy(c => c.Type)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<OsymRegistrationCalendarDto> CreateAsync(CreateOsymRegistrationCalendarDto dto, CancellationToken ct = default)
    {
        ValidateDates(dto.StartDate, dto.EndDate);
        var type = ParseType(dto.Type);
        var entity = new OsymRegistrationCalendar
        {
            Year = dto.Year,
            Type = type,
            Name = dto.Name,
            StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description,
            IsActive = dto.IsActive,
            CreatedAt = _clock.UtcNow
        };
        _ctx.OsymRegistrationCalendars.Add(entity);
        await _ctx.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<OsymRegistrationCalendarDto> UpdateAsync(int id, UpdateOsymRegistrationCalendarDto dto, CancellationToken ct = default)
    {
        var entity = await _ctx.OsymRegistrationCalendars.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Kayıt bulunamadı: {id}");

        ValidateDates(dto.StartDate, dto.EndDate);
        entity.Year = dto.Year;
        entity.Type = ParseType(dto.Type);
        entity.Name = dto.Name;
        entity.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
        entity.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = _clock.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _ctx.OsymRegistrationCalendars.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Kayıt bulunamadı: {id}");
        _ctx.OsymRegistrationCalendars.Remove(entity);
        await _ctx.SaveChangesAsync(ct);
    }

    private static OsymRegistrationCalendarDto ToDto(OsymRegistrationCalendar e) => new(
        e.Id, e.Year, e.Type.ToString(), Labels.Osym(e.Type),
        e.Name, e.StartDate, e.EndDate, e.Description, e.IsActive,
        e.CreatedAt, e.UpdatedAt);

    private static OsymRegistrationType ParseType(string s) => s switch
    {
        "YksPlacement" => OsymRegistrationType.YksPlacement,
        "HorizontalTransfer" => OsymRegistrationType.HorizontalTransfer,
        "VerticalTransfer" => OsymRegistrationType.VerticalTransfer,
        "ForeignStudent" => OsymRegistrationType.ForeignStudent,
        _ => throw new ArgumentException($"Geçersiz ÖSYM kayıt tipi: {s}")
    };

    private static void ValidateDates(DateTime start, DateTime end)
    {
        if (start >= end)
            throw new InvalidOperationException("Başlangıç tarihi bitiş tarihinden önce olmalıdır.");
    }
}
