using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Services;

public interface IExcelScheduleService
{
    Task<byte[]> ExportScheduleAsync(int termId, int facultyId, int? departmentId = null, CancellationToken ct = default);
    Task<byte[]> ExportPdfAsync(int termId, int facultyId, int? departmentId = null, CancellationToken ct = default);
    Task<ImportPreviewDto> PreviewImportAsync(Stream fileStream, int termId, int facultyId, CancellationToken ct = default);
    Task<ImportResultDto> ApplyImportAsync(int termId, List<int> rowNumbers, ImportPreviewDto preview, CancellationToken ct = default);
}
