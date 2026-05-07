using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H4 — Classroom Type. Lab component yalnızca Lab veya Computer tipi dersliklerde olur.
/// Lecture component için tip kısıtı yok (lecture lab odasında da yapılabilir).
/// CP-SAT model: Lab component için section.LabRoom != non-lab classroom constraint, assumption gated.
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

            foreach (var classroom in ctx.Request.Classrooms)
            {
                var isLabFriendly = classroom.Type == ClassroomType.Lab
                    || classroom.Type == ClassroomType.Computer;

                if (!isLabFriendly)
                {
                    // Forbid lab component in non-lab classroom
                    var roomVar = ctx.Variables.GetLabRoom(section.Id);
                    var literal = ctx.Assumptions.Register(Code, section.Id, classroom.Id);
                    ctx.Model.Add(roomVar != classroom.Id).OnlyEnforceIf(literal);
                }
            }
        }
    }
}
