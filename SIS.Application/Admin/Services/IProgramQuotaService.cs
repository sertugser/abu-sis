using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IProgramQuotaService
{
    Task<IReadOnlyList<ProgramQuotaDto>> GetAllAsync(int? programId = null, int? year = null, CancellationToken ct = default);
    Task<ProgramQuotaDto> CreateAsync(CreateProgramQuotaDto dto, CancellationToken ct = default);
    Task<ProgramQuotaDto> UpdateAsync(int id, UpdateProgramQuotaDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
