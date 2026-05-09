using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// S1 — Occupancy. Classroom doluluk oranı [S1MinOccupancyPercent, S1MaxOccupancyPercent]
/// hedef aralığında olmalı. Aralık dışına sapma kadar ceza eklenir.
///
/// Penalty formülü (per (section, classroom) presence literal):
///   rate = section.Capacity × 100 / classroom.Capacity   (integer math, v1 OccupancyEvaluator paritesi)
///   deviation = max(0, min - rate, rate - max)
///   penalty_term = deviation × S1Weight × presenceLiteral
/// </summary>
/// <remarks>
/// v1 reference: <c>SIS.Infrastructure/Services/Scheduling/Evaluators/Soft/OccupancyEvaluator.cs</c>.
/// v1 floating-point math + 0.5 multiplier kullanır; v2 (Plan 8) integer math + admin-tunable
/// S1Weight kullanır. Penalty literal'e bağlandığı için solver presence=0 yaparsa cezasız,
/// presence=1 yaparsa deviation kadar ceza objektif fonksiyona yansır.
/// </remarks>
public sealed class OccupancyEncoder : IConstraintEncoder
{
    public string Code => "S1";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        var thresholds = ctx.Request.SoftThresholds;
        if (thresholds.S1Weight <= 0)
            return;

        var min = thresholds.S1MinOccupancyPercent;
        var max = thresholds.S1MaxOccupancyPercent;
        var weight = thresholds.S1Weight;

        var sectionsById = ctx.Request.Sections.ToDictionary(s => s.Id);
        var roomsById = ctx.Request.Classrooms.ToDictionary(c => c.Id);

        // Single-room presence literals (lecture + single-group lab).
        foreach (var ((sectionId, classroomId, _), literal) in ctx.Variables.AllPresenceLiterals)
        {
            AddPenaltyIfDeviated(
                ctx, sectionsById, roomsById, sectionId, classroomId, literal,
                min, max, weight);
        }

        // Plan 4 paralel lab presence literals (per group).
        foreach (var ((sectionId, classroomId, _, _), literal) in ctx.Variables.AllPresenceLiteralsForGroups)
        {
            AddPenaltyIfDeviated(
                ctx, sectionsById, roomsById, sectionId, classroomId, literal,
                min, max, weight);
        }

        // Plan 9 Issue 4a — multi-block lab presence literals (Plan 7b time-split).
        // Per-(section, classroom, block, group) penalty contribution. Each block lands in
        // its own classroom assignment; rate = section.Capacity × 100 / classroom.Capacity
        // is identical for every block of a given (section, classroom) pair, so the
        // accumulated penalty scales with block count (matches v1 OccupancyEvaluator
        // behaviour where each scheduled block contributes its own deviation term).
        foreach (var ((sectionId, classroomId, _, _), literal) in ctx.Variables.AllPresenceLiteralsForLabBlocks)
        {
            AddPenaltyIfDeviated(
                ctx, sectionsById, roomsById, sectionId, classroomId, literal,
                min, max, weight);
        }
    }

    private static void AddPenaltyIfDeviated(
        EncoderContext ctx,
        IReadOnlyDictionary<int, SectionInput> sectionsById,
        IReadOnlyDictionary<int, ClassroomInput> roomsById,
        int sectionId,
        int classroomId,
        ILiteral literal,
        int min,
        int max,
        int weight)
    {
        if (!sectionsById.TryGetValue(sectionId, out var section)) return;
        if (!roomsById.TryGetValue(classroomId, out var room)) return;
        if (room.Capacity <= 0) return;

        // v1 paritesi: integer math. section.Capacity × 100 / room.Capacity.
        var rate = section.Capacity * 100 / room.Capacity;

        var deviation = rate < min ? min - rate
            : rate > max ? rate - max
            : 0;

        if (deviation > 0)
            ctx.Penalties.Add(LinearExpr.Term(literal, (long)deviation * weight));
    }
}
