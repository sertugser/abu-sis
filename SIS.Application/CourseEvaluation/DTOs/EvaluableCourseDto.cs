namespace SIS.Application.CourseEvaluation.DTOs;

public record EvaluableCourseDto(
    int CourseSectionId,
    string CourseCode,
    string CourseName,
    string InstructorName,
    bool IsCompleted,
    DateTime? SubmittedAt);
