using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Services;

public interface IExcelScheduleService
{
    Task<byte[]> ExportScheduleAsync(int termId, int facultyId, int? departmentId = null, CancellationToken ct = default);
    Task<byte[]> ExportPdfAsync(int termId, int facultyId, int? departmentId = null, CancellationToken ct = default);
    Task<ImportPreviewDto> PreviewImportAsync(int userId, Stream fileStream, int termId, int facultyId, CancellationToken ct = default);
    Task<ImportResultDto> ApplyImportAsync(int userId, int facultyId, int termId, Guid importSessionId, List<int> rowNumbers, CancellationToken ct = default);
}
