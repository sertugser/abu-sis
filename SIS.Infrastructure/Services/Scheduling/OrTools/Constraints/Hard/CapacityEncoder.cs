using System.Diagnostics;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H3 — Capacity. Section'ın kapasitesi atanan dersliğin fiziksel kapasitesini aşamaz.
/// CP-SAT model: Lecture tek oda, lab paralel split desteği (Plan 4 Task 3):
/// - Lecture: section.Capacity > classroom.Capacity → roomVar != classroom.Id (assumption-gated).
/// - Lab: groupCount per-section; minCapPerGroup = ⌈Capacity/groupCount⌉. Her group'un IntVar'ı için
///   minCapPerGroup > classroom.Capacity ise roomVar != classroom.Id (assumption-gated).
///   Tek (sectionId, classroomId) literal'i N group'un hepsini bloklar — minCapPerGroup hesabı tüm
///   group'lar için aynı olduğundan tek literal yeterli.
/// </summary>
public sealed class CapacityEncoder : IConstraintEncoder
{
    public string Code => "H3";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        foreach (var section in ctx.Request.Sections)
        {
            // Online section'lar için ScheduleModelBuilder room IntVar oluşturmaz; Kapasite kısıtının
            // anlamı yoktur. Guard atlanırsa GetLectureRoom/GetLabRooms KeyNotFoundException atar.
            // (Diğer 3 hard room encoder — Distribution / ClassroomType / ClassroomConflict — aynı
            // guard'a zaten sahip; CapacityEncoder'da eksikti.)
            if (section.TeachingMethod == TeachingMethod.Online)
                continue;

            // Lecture component (single room — unchanged)
            if (section.TheoryHours > 0)
            {
                var roomVar = ctx.Variables.GetLectureRoom(section.Id);
                foreach (var classroom in ctx.Request.Classrooms)
                {
                    if (section.Capacity > classroom.Capacity)
                    {
                        var literal = ctx.Assumptions.Register(Code, section.Id, classroom.Id);
                        ctx.Model.Add(roomVar != classroom.Id).OnlyEnforceIf(literal);
                    }
                }
            }

            // Lab component (multi-room paralel split desteği — Plan 4 Task 3)
            if (section.LabHours > 0)
            {
                var labRooms = ctx.Variables.GetLabRooms(section.Id);
                var groupCount = labRooms.Count;
                // Invariant: DecisionVariables creates exactly section.LabGroupCount IntVars.
                Debug.Assert(groupCount == section.LabGroupCount,
                    $"labRooms.Count ({groupCount}) != section.LabGroupCount ({section.LabGroupCount}) for section {section.Id}");
                // Per-group capacity threshold (ceil division). groupCount >= 1 garantili (DecisionVariables).
                var minCapPerGroup = (section.Capacity + groupCount - 1) / groupCount;

                foreach (var classroom in ctx.Request.Classrooms)
                {
                    if (minCapPerGroup > classroom.Capacity)
                    {
                        var literal = ctx.Assumptions.Register(Code, section.Id, classroom.Id);
                        foreach (var roomVar in labRooms)
                        {
                            ctx.Model.Add(roomVar != classroom.Id).OnlyEnforceIf(literal);
                        }
                    }
                }
            }
        }
    }
}
