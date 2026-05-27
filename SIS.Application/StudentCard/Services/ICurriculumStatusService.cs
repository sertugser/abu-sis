using SIS.Application.Common.DTOs;

namespace SIS.Application.StudentCard.Services;

public interface ICurriculumStatusService
{
    Task<CurriculumStatusDto?> GetCurriculumStatusAsync(int studentId, CancellationToken ct = default);
}
