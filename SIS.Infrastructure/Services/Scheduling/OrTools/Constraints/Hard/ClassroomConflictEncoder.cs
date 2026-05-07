using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H1 — Classroom Conflict (Plan 2 Task 4: real implementation, replacing Plan 1 PoC).
/// Aynı derslikte aynı zaman aralığında iki section olamaz.
/// CP-SAT model: per-classroom optional intervals + AddNoOverlap.
/// Presence literal per (section, classroom): room[sectionId] == classroomId.
/// UNSAT core: per-classroom literal registered as assumption (aggregate).
/// </summary>
public sealed class ClassroomConflictEncoder : IConstraintEncoder
{
    public string Code => "H1";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        var sections = ctx.Request.Sections;
        var classrooms = ctx.Request.Classrooms;

        // Group optional intervals per classroom
        var perClassroomIntervals = new Dictionary<int, List<IntervalVar>>();

        foreach (var section in sections)
        {
            // Lecture component: create optional interval for each candidate classroom
            if (section.TheoryHours > 0)
            {
                foreach (var classroom in classrooms)
                {
                    var optInterval = ctx.Variables.CreateOptionalRoomInterval(
                        section.Id, classroom.Id, ComponentType.Lecture, section.TheoryHours);

                    if (!perClassroomIntervals.TryGetValue(classroom.Id, out var list))
                    {
                        list = new List<IntervalVar>();
                        perClassroomIntervals[classroom.Id] = list;
                    }
                    list.Add(optInterval);
                }
            }

            // Lab component: create optional interval for each candidate classroom
            if (section.LabHours > 0)
            {
                foreach (var classroom in classrooms)
                {
                    var optInterval = ctx.Variables.CreateOptionalRoomInterval(
                        section.Id, classroom.Id, ComponentType.Lab, section.LabHours);

                    if (!perClassroomIntervals.TryGetValue(classroom.Id, out var list))
                    {
                        list = new List<IntervalVar>();
                        perClassroomIntervals[classroom.Id] = list;
                    }
                    list.Add(optInterval);
                }
            }
        }

        // Per classroom: AddNoOverlap on all optional intervals for that classroom
        foreach (var (classroomId, intervals) in perClassroomIntervals)
        {
            if (intervals.Count >= 2)
            {
                ctx.Model.AddNoOverlap(intervals);
                // Register a per-classroom assumption literal for diagnostic UNSAT core mapping
                ctx.Assumptions.Register(Code, primarySectionId: classroomId, secondarySectionId: null);
            }
        }
    }
}
