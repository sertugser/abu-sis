using System.Diagnostics;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H4 — Classroom Type. Lab component yalnızca Lab veya Computer tipi dersliklerde olur.
/// Lecture component için tip kısıtı yok (lecture lab odasında da yapılabilir).
/// CP-SAT model: Lab component'in HER paralel group IntVar'ı için non-lab classroom yasaklanır.
/// Tek (sectionId, classroomId) literal'i N group'un hepsini bloklar — classroom tipi tüm
/// group'lar için aynı şekilde uygunsuz olduğundan tek literal yeterli (H3 ile tutarlı pattern).
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
            // Only sections with a lab component are constrained
            if (section.LabHours == 0)
                continue;

            var labRooms = ctx.Variables.GetLabRooms(section.Id);
            var groupCount = labRooms.Count;
            // Invariant: DecisionVariables creates exactly section.LabGroupCount IntVars.
            Debug.Assert(groupCount == section.LabGroupCount,
                $"labRooms.Count ({groupCount}) != section.LabGroupCount ({section.LabGroupCount}) for section {section.Id}");

            foreach (var classroom in ctx.Request.Classrooms)
            {
                var isLabFriendly = classroom.Type == ClassroomType.Lab
                    || classroom.Type == ClassroomType.Computer;

                if (!isLabFriendly)
                {
                    // Forbid lab component in non-lab classroom — for ALL groups.
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
