using SIS.Domain.Entities;

namespace SIS.Application.Scheduling.Constraints;

/// <summary>
/// Holds loaded constraint definitions and exceptions for efficient evaluation.
/// Loaded once per evaluation batch, then queried per-evaluator.
/// </summary>
public sealed class ConstraintState
{
    public Dictionary<string, ConstraintDefinition> Definitions { get; }
    public List<ConstraintException> Exceptions { get; }

    public ConstraintState(
        Dictionary<string, ConstraintDefinition> definitions,
        List<ConstraintException> exceptions)
    {
        Definitions = definitions;
        Exceptions = exceptions;
    }

    /// <summary>
    /// Returns true if the given constraint code is disabled in DB.
    /// If the code is not found in definitions, the constraint is considered enabled (not skipped).
    /// </summary>
    public bool IsDisabled(string code)
    {
        return Definitions.TryGetValue(code, out var def) && !def.IsEnabled;
    }

    /// <summary>
    /// Checks if a constraint is exempt for the given entity combination.
    /// Uses wildcard matching: null fields match ALL values (e.g., InstructorId=null means "all instructors").
    /// This is intentional — an exception with only DepartmentId set applies to all instructors in that department.
    /// </summary>
    public bool IsExempt(string code, int? instructorId, int? departmentId, int? sectionId)
    {
        return Exceptions.Any(e =>
            e.ConstraintDefinition?.Code == code &&
            e.IsExempt &&
            (e.InstructorId == null || e.InstructorId == instructorId) &&
            (e.DepartmentId == null || e.DepartmentId == departmentId) &&
            (e.CourseSectionId == null || e.CourseSectionId == sectionId));
    }

    /// <summary>
    /// Returns the parameter override JSON for a matching exception, if any.
    /// </summary>
    public string? GetParameterOverride(string code, int? instructorId, int? departmentId, int? sectionId)
    {
        return Exceptions
            .Where(e =>
                e.ConstraintDefinition?.Code == code &&
                e.ParameterOverride != null &&
                (e.InstructorId == null || e.InstructorId == instructorId) &&
                (e.DepartmentId == null || e.DepartmentId == departmentId) &&
                (e.CourseSectionId == null || e.CourseSectionId == sectionId))
            .Select(e => e.ParameterOverride)
            .FirstOrDefault();
    }
}
