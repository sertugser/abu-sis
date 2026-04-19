using SIS.Domain.Enums;

namespace SIS.Application.CourseEvaluation.DTOs;

public record EvaluationPeriodDto(
    int Id,
    int TermId,
    string TermName,
    DateTime OpenedAt,
    DateTime ClosesAt,
    EvaluationPeriodStatus Status,
    bool IsCurrentlyOpen);
