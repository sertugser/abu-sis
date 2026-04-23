using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class ProgramQuotaService(AppDbContext ctx, IDateTimeProvider clock) : IProgramQuotaService
{
    private readonly AppDbContext _ctx = ctx;
    private readonly IDateTimeProvider _clock = clock;

    private async Task<ProgramQuotaDto> ReloadAsync(int id, CancellationToken ct) =>
        ToDto(await _ctx.ProgramQuotas
            .AsNoTracking()
            .Include(q => q.Program).ThenInclude(p => p.Department).ThenInclude(d => d.Faculty)
            .FirstAsync(q => q.Id == id, ct));

    public async Task<IReadOnlyList<ProgramQuotaDto>> GetAllAsync(int? programId = null, int? year = null, CancellationToken ct = default)
    {
        var query = _ctx.ProgramQuotas
            .AsNoTracking()
            .Include(q => q.Program).ThenInclude(p => p.Department).ThenInclude(d => d.Faculty)
            .AsQueryable();

        if (programId.HasValue) query = query.Where(q => q.ProgramId == programId.Value);
        if (year.HasValue) query = query.Where(q => q.Year == year.Value);

        var list = await query
            .OrderByDescending(q => q.Year)
            .ThenBy(q => q.Program.Department.Faculty.Name)
            .ThenBy(q => q.Program.Department.Name)
            .ThenBy(q => q.Program.Name)
            .ThenBy(q => q.Type)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ProgramQuotaDto> CreateAsync(CreateProgramQuotaDto dto, CancellationToken ct = default)
    {
        var type = ParseType(dto.Type);

        if (dto.Quantity < 0)
            throw new InvalidOperationException("Kontenjan negatif olamaz.");

        var programExists = await _ctx.AcademicPrograms.AnyAsync(p => p.Id == dto.ProgramId, ct);
        if (!programExists)
            throw new KeyNotFoundException($"Program bulunamadı: {dto.ProgramId}");

        var duplicateExists = await _ctx.ProgramQuotas.AnyAsync(
            q => q.ProgramId == dto.ProgramId && q.Year == dto.Year && q.Type == type, ct);
        if (duplicateExists)
            throw new InvalidOperationException("Bu program için bu yıl + tip kombinasyonu zaten tanımlı.");

        var entity = new ProgramQuota
        {
            ProgramId = dto.ProgramId,
            Year = dto.Year,
            Type = type,
            Quantity = dto.Quantity,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description,
            CreatedAt = _clock.UtcNow
        };
        _ctx.ProgramQuotas.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return await ReloadAsync(entity.Id, ct);
    }

    public async Task<ProgramQuotaDto> UpdateAsync(int id, UpdateProgramQuotaDto dto, CancellationToken ct = default)
    {
        var entity = await _ctx.ProgramQuotas.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Kontenjan bulunamadı: {id}");

        if (dto.Quantity < 0)
            throw new InvalidOperationException("Kontenjan negatif olamaz.");

        var newType = ParseType(dto.Type);

        // Unique constraint guard (tip veya yıl değiştiyse)
        if (entity.Year != dto.Year || entity.Type != newType)
        {
            var duplicateExists = await _ctx.ProgramQuotas.AnyAsync(
                q => q.Id != id && q.ProgramId == entity.ProgramId && q.Year == dto.Year && q.Type == newType, ct);
            if (duplicateExists)
                throw new InvalidOperationException("Bu program için bu yıl + tip kombinasyonu zaten tanımlı.");
        }

        entity.Year = dto.Year;
        entity.Type = newType;
        entity.Quantity = dto.Quantity;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description;
        entity.UpdatedAt = _clock.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        return await ReloadAsync(entity.Id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _ctx.ProgramQuotas.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Kontenjan bulunamadı: {id}");
        _ctx.ProgramQuotas.Remove(entity);
        await _ctx.SaveChangesAsync(ct);
    }

    private static ProgramQuotaDto ToDto(ProgramQuota q) => new(
        q.Id,
        q.ProgramId,
        q.Program.Name,
        q.Program.Department.Name,
        q.Program.Department.Faculty.Name,
        q.Year,
        q.Type.ToString(),
        Labels.Quota(q.Type),
        q.Quantity,
        q.Description,
        q.CreatedAt,
        q.UpdatedAt);

    private static QuotaType ParseType(string s) => s switch
    {
        "Yks" => QuotaType.Yks,
        "LateralTransfer" => QuotaType.LateralTransfer,
        "Postgraduate" => QuotaType.Postgraduate,
        "ForeignStudent" => QuotaType.ForeignStudent,
        _ => throw new ArgumentException($"Geçersiz kontenjan tipi: {s}")
    };
}
