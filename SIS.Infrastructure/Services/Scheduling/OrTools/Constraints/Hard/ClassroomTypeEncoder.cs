using System.Diagnostics;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H4 — Classroom Type. v1 paritesi:
///   1. Lab component yalnızca Lab/Computer (default) veya Course.LabClassroomType (override) tipinde olur.
///   2. Theory component için Course.TheoryClassroomType set ise yalnızca o tipte oda kabul edilir.
///   3. Capacity ≥ AmphiThreshold ise theory component yalnızca Amphitheater tipinde olabilir
///      (RequiredTheoryClassroomType ile birlikte AND uygulanır).
/// CP-SAT model: section'ın room IntVar'ı için her uyumsuz classroom için ayrı assumption literal'i.
/// </summary>
public sealed class ClassroomTypeEncoder : IConstraintEncoder
{
    public string Code => "H4";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        foreach (var section in ctx.Request.Sections)
        {
            EncodeLabComponent(ctx, section);
            EncodeTheoryComponent(ctx, section);
        }
    }

    // Sub-codes disambiguate UNSAT core entries when the same (section, classroom) pair
    // is forbidden by both branches; otherwise UnsatCoreInterpreter would emit duplicates.
    private const string LabCode = "H4_L";
    private const string TheoryCode = "H4_T";

    private void EncodeLabComponent(EncoderContext ctx, SectionInput section)
    {
        if (section.LabHours == 0) return;
        // Online sections have no room var (Plan 7b Task 4 — ScheduleModelBuilder skips CreateRoomVariable).
        if (section.TeachingMethod == TeachingMethod.Online) return;

        var labRooms = ctx.Variables.GetLabRooms(section.Id);

        Debug.Assert(labRooms.Count == section.LabGroupCount,
            $"labRooms.Count ({labRooms.Count}) != section.LabGroupCount ({section.LabGroupCount}) for section {section.Id}");

        // RequiredLabClassroomType set ise yalnızca o tip izinli; aksi halde v1 fallback (Lab/Computer).
        foreach (var classroom in ctx.Request.Classrooms)
        {
            bool isAllowed;
            if (section.RequiredLabClassroomType.HasValue)
            {
                isAllowed = (int)classroom.Type == section.RequiredLabClassroomType.Value;
            }
            else
            {
                isAllowed = classroom.Type == ClassroomType.Lab
                         || classroom.Type == ClassroomType.Computer;
            }

            if (!isAllowed)
            {
                var literal = ctx.Assumptions.Register(LabCode, section.Id, classroom.Id);
                foreach (var roomVar in labRooms)
                    ctx.Model.Add(roomVar != classroom.Id).OnlyEnforceIf(literal);
            }
        }
    }

    private void EncodeTheoryComponent(EncoderContext ctx, SectionInput section)
    {
        if (section.TheoryHours == 0) return;
        // Online sections have no room var (Plan 7b Task 4 — ScheduleModelBuilder skips CreateRoomVariable).
        if (section.TeachingMethod == TeachingMethod.Online) return;

        var lectureRoom = ctx.Variables.GetLectureRoom(section.Id);

        // Plan 9 Issue 1 — Schedule.AmphiThresholdEnabled flag-gated. False (default) suppresses
        // the capacity-based amphi rule entirely so Term 5 (0 Amphitheater inventory) does not UNSAT.
        // RequiredTheoryClassroomType remains independent of this toggle.
        var amphiRequired = ctx.AmphiThresholdEnabled && section.Capacity >= ctx.AmphiThreshold;
        var typeRequired = section.RequiredTheoryClassroomType.HasValue;

        if (!amphiRequired && !typeRequired) return;

        // AND semantics by design: when both AmphiThreshold and RequiredTheoryClassroomType
        // apply, a classroom must satisfy BOTH. Conflicting pairs (e.g. RequiredType=PhysicsLab
        // + capacity ≥ threshold) yield UNSAT for the section — admin must reconcile data.
        foreach (var classroom in ctx.Request.Classrooms)
        {
            bool meetsAmphi = !amphiRequired || classroom.Type == ClassroomType.Amphitheater;
            bool meetsType = !typeRequired
                          || (int)classroom.Type == section.RequiredTheoryClassroomType!.Value;

            if (meetsAmphi && meetsType) continue;

            var literal = ctx.Assumptions.Register(TheoryCode, section.Id, classroom.Id);
            ctx.Model.Add(lectureRoom != classroom.Id).OnlyEnforceIf(literal);
        }
    }
}
