using Google.OrTools.Sat;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H1 — Classroom Conflict.
/// PoC implementasyon (Plan 1 Faz 2): Aynı saat aralığında iki lecture interval'ı çakışamaz.
/// Plan 2'de gerçek "per-classroom AddNoOverlap" modeline (optional interval + room IntVar matching) geçilecek.
/// Not: CP-SAT 9.10 NoOverlap üzerinde OnlyEnforceIf desteklemediği için PoC'de
/// AddNoOverlap koşulsuz uygulanır; assumption literal sadece UNSAT core registry'sinde tutulur.
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

        for (int i = 0; i < sections.Count; i++)
        {
            for (int j = i + 1; j < sections.Count; j++)
            {
                var s1 = sections[i];
                var s2 = sections[j];

                if (s1.TheoryHours > 0 && s2.TheoryHours > 0)
                {
                    var iv1 = ctx.Variables.GetLectureInterval(s1.Id);
                    var iv2 = ctx.Variables.GetLectureInterval(s2.Id);
                    if (iv1 != null && iv2 != null)
                    {
                        // Assumption literal kayıtlı ama enforcement yok (Plan 2'de optional interval ile bağlanacak).
                        ctx.Assumptions.Register(Code, s1.Id, s2.Id);
                        ctx.Model.AddNoOverlap(new[] { iv1, iv2 });
                    }
                }
            }
        }
    }
}
