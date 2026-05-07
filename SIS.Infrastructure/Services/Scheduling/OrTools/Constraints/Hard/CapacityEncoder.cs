namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H3 — Capacity. Section'ın kapasitesi atanan dersliğin fiziksel kapasitesini aşamaz.
/// CP-SAT model: Her (section, classroom) pair için section.Capacity > classroom.Capacity ise
/// room[section] != classroom.Id constraint'i, assumption literal ile gated.
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
            foreach (var classroom in ctx.Request.Classrooms)
            {
                if (section.Capacity > classroom.Capacity)
                {
                    // Lecture component: forbid this classroom for the section's lecture
                    if (section.TheoryHours > 0)
                    {
                        var roomVar = ctx.Variables.GetLectureRoom(section.Id);
                        var literal = ctx.Assumptions.Register(Code, section.Id, classroom.Id);
                        ctx.Model.Add(roomVar != classroom.Id).OnlyEnforceIf(literal);
                    }

                    // Lab component: forbid this classroom for the section's lab
                    if (section.LabHours > 0)
                    {
                        var roomVar = ctx.Variables.GetLabRoom(section.Id);
                        var literal = ctx.Assumptions.Register(Code, section.Id, classroom.Id);
                        ctx.Model.Add(roomVar != classroom.Id).OnlyEnforceIf(literal);
                    }
                }
            }
        }
    }
}
