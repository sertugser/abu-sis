using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Services;

public interface IStudentTimetableService
{
    Task<StudentTimetableDto> GetTimetableAsync(
        int studentId,
        int? termId,
        CancellationToken cancellationToken);

    Task<List<StudentTimetableTermDto>> GetEnrolledTermsAsync(
        int studentId,
        CancellationToken cancellationToken);

    Task<byte[]> GeneratePdfAsync(
        int studentId,
        int termId,
        CancellationToken cancellationToken);
}
