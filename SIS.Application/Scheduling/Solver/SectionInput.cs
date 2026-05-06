namespace SIS.Application.Scheduling.Solver;

public sealed record SectionInput
{
    public required int Id { get; init; }
    public required int CourseId { get; init; }
    public required string CourseCode { get; init; }
    public required string CourseName { get; init; }
    public required int SectionNumber { get; init; }
    public required int Capacity { get; init; }
    public required int TheoryHours { get; init; }
    public required int LabHours { get; init; }
    public int? InstructorId { get; init; }
    public int? DepartmentId { get; init; }
    public required string TeachingMethod { get; init; }   // "FaceToFace" | "Online" | "Hybrid"
    public int? RequiredTheoryClassroomType { get; init; }
    public int? RequiredLabClassroomType { get; init; }
    public bool IsPinned { get; init; }
    public bool IsScheduleLocked { get; init; }
}
