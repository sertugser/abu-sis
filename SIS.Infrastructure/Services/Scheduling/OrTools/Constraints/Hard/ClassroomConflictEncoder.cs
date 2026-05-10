using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

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
            // Plan 7b — Online section'ın room IntVar yok; classroom conflict tanımsız, atla.
            if (section.TeachingMethod == TeachingMethod.Online)
                continue;

            // Lecture component: create optional interval for each candidate classroom (groupIndex=0).
            if (section.TheoryHours > 0)
            {
                foreach (var classroom in classrooms)
                {
                    var optInterval = ctx.Variables.CreateOptionalRoomIntervalForGroup(
                        section.Id, classroom.Id, ComponentType.Lecture, section.TheoryHours, groupIndex: 0);

                    // Plan 9 review polish: defense-in-depth null guard. Lecture branch never triggers
                    // the helper's multi-interval × parallel-rooms safety net (component is Lecture, not Lab),
                    // but if a future caller passes ComponentType.Lab here the helper returns null! and we
                    // must not corrupt perClassroomIntervals.
                    if (optInterval == null) continue;

                    if (!perClassroomIntervals.TryGetValue(classroom.Id, out var list))
                    {
                        list = new List<IntervalVar>();
                        perClassroomIntervals[classroom.Id] = list;
                    }
                    list.Add(optInterval);
                }
            }

            // Lab component: create optional interval per (block, group, classroom).
            // Plan 4 Task 5 paralel lab support × Plan 7b Task 8 multi-interval lab time-split:
            //   - Single-block (LabHours ≤ MaxLabBlockHours): GetLabIntervals returns 1-elem list,
            //     blockSizes = [LabHours]; loop runs once × LabGroupCount × Classrooms.
            //   - Multi-block × LabGroupCount=1: each block gets its own per-classroom interval.
            //   - Multi-block × LabGroupCount>1: Plan 10 Task 1 — full per (block, group, classroom)
            //     iteration supported. CreateOptionalRoomIntervalForLabBlock + AddAllDifferent
            //     (CreateRoomVariable Lab branch) zaten composable; eski Plan 9 Issue 2 guard kalktı.
            if (section.LabHours > 0)
            {
                var labRooms = ctx.Variables.GetLabRooms(section.Id);
                var labIntervals = ctx.Variables.GetLabIntervals(section.Id);

                var blockSizes = LabBlockSplitter.SplitLabIntoBlocks(
                    section.LabHours, ctx.Request.MaxLabBlockHours);

                for (int ivIdx = 0; ivIdx < labIntervals.Count; ivIdx++)
                {
                    var labIv = labIntervals[ivIdx];
                    var blockDuration = blockSizes[ivIdx];

                    for (int g = 0; g < labRooms.Count; g++)
                    {
                        foreach (var classroom in classrooms)
                        {
                            var optInterval = ctx.Variables.CreateOptionalRoomIntervalForLabBlock(
                                section.Id, classroom.Id, blockIndex: ivIdx, groupIndex: g,
                                sourceInterval: labIv, duration: blockDuration);

                            if (!perClassroomIntervals.TryGetValue(classroom.Id, out var list))
                            {
                                list = new List<IntervalVar>();
                                perClassroomIntervals[classroom.Id] = list;
                            }
                            list.Add(optInterval);
                        }
                    }
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
