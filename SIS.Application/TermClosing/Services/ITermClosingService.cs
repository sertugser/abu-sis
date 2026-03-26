using SIS.Application.AcademicProgression.DTOs;
using SIS.Application.TermClosing.DTOs;

namespace SIS.Application.TermClosing.Services;

public interface ITermClosingService
{
    Task<TermClosingStatusDto> GetStatusAsync(int departmentId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> LockGradesAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> UnlockGradesAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<GpaRecalculationResultDto> CalculateGpaAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<BatchProgressionResultDto> PreviewPromotionAsync(int departmentId, CancellationToken ct = default);
    Task<BatchProgressionResultDto> PromoteAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> RollbackPromotionAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<NewTermResultDto> OpenNewTermAsync(int departmentId, int userId, NewTermRequestDto request, CancellationToken ct = default);
}
