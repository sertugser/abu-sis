using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

/// <summary>
/// H1 — Derslik Çakışması. Aynı derslikte aynı zaman aralığında 2 farklı section olamaz.
/// </summary>
/// <remarks>
/// Plan 10 — paralel-lab aware. Eski impl section-level <see cref="SectionSnapshot.ClassroomId"/>
/// üzerinden filtreliyordu; paralel-lab section'larında (LabGroupCount>1) aynı section'ın 2-3
/// farklı odası olur, section-level field tek değer döndüğü için ya yanlış oda raporluyor ya
/// da kendi paralel grup TimeBlock'unu "çakışma" sanıyordu (false-positive). Yeni impl her
/// candidate TimeBlock'un kendi <c>ClassroomId</c>'si üzerinden eşleşme arar; aynı section'ın
/// kendi TimeBlock'larıyla self-conflict raporlamaz.
/// </remarks>
public sealed class ClassroomConflictEvaluator : IConstraintEvaluator
{
    public string Code => "H1";
    public string Name => "Derslik Çakışması";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var conflicts = new List<SchedulingConflictDto>();

        foreach (var cBlock in c.TimeBlocks)
        {
            // TimeBlock-level oda yoksa section-level fallback (manuel atama akışı —
            // ScheduleValidationRequestDto.ScheduleSlots her slot için classroomId taşımayabilir).
            var candidateRoom = cBlock.ClassroomId ?? c.ClassroomId;
            if (!candidateRoom.HasValue)
                continue;

            foreach (var existing in context.Snapshot.Sections)
            {
                // Self-skip: aynı section'ın paralel grup veya multi-block TimeBlock'ları
                // farklı odalardadır; bunu çakışma sanmamak için self compare atlanır.
                if (existing.SectionId == c.SectionId)
                    continue;

                foreach (var eBlock in existing.TimeBlocks)
                {
                    var existingRoom = eBlock.ClassroomId ?? existing.ClassroomId;
                    if (existingRoom != candidateRoom)
                        continue; // farklı oda — çakışma yok

                    if (!SchedulingHelpers.TryGetOverlap(cBlock, eBlock, out var overlap))
                        continue; // farklı gün veya zaman çakışmıyor

                    conflicts.Add(SchedulingHelpers.BuildConflict(
                        c,
                        "ClassroomConflict",
                        $"Derslik '{c.ClassroomName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
                        overlap,
                        existing.SectionId));
                }
            }
        }

        return conflicts.Count > 0
            ? ConstraintResult.WithConflicts(conflicts)
            : ConstraintResult.Ok();
    }
}
