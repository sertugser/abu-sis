using SIS.Application.Common.DTOs;

namespace SIS.Application.StudentCard.Services;

public interface IStudentTranscriptService
{
    Task<StudentTranscriptDto?> GetTranscriptAsync(int studentId, CancellationToken ct = default);
}
