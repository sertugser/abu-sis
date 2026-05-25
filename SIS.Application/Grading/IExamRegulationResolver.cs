using SIS.Domain.Entities;

namespace SIS.Application.Grading;

public interface IExamRegulationResolver
{
    Task<ExamRegulation?> ResolveForStudentAsync(int studentId, int academicTermId, CancellationToken ct);
    Task<WeightBounds?> ResolveCourseWeightBoundsAsync(int courseId, int academicTermId, CancellationToken ct);
}

public sealed record WeightBounds(
    decimal MinMidterm, decimal MaxMidterm,
    decimal MinFinal, decimal MaxFinal,
    bool IsEmpty);
