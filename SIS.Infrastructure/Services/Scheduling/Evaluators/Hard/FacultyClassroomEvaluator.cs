using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

public sealed class FacultyClassroomEvaluator : IConstraintEvaluator
{
    public string Code => "H10";
    public string Name => "Fakülte Derslik Ataması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var snapshot = context.Snapshot;

        if (!c.ClassroomId.HasValue || !c.DepartmentId.HasValue || snapshot.ClassroomAssignments.Count == 0)
            return ConstraintResult.Ok();

        var candidateFacultyId = snapshot.DeptFacultyMap.GetValueOrDefault(c.DepartmentId.Value);
        if (candidateFacultyId <= 0)
            return ConstraintResult.Ok();

        var hasAssignment = snapshot.ClassroomAssignments
            .Any(cta => cta.ClassroomId == c.ClassroomId.Value && cta.FacultyId == candidateFacultyId);

        if (!hasAssignment)
        {
            return ConstraintResult.WithConflict(SchedulingHelpers.BuildConflict(
                c,
                "FacultyClassroomConflict",
                $"Derslik '{c.ClassroomName}' bu dönemde ilgili fakülteye atanmamış.",
                null,
                null));
        }

        return ConstraintResult.Ok();
    }
}
