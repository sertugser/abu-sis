using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Model;

/// <summary>
/// CP-SAT model'i kuran orchestrator. ISolverEngine.SolveAsync içinden çağırılır.
/// Adımlar:
///   1) DecisionVariables: section başına lecture/lab interval + room IntVar
///   2) Hard encoder'ları çağır (H1, H3, ..., H17)
///   3) Soft encoder'ları çağır (objective penalty toplar)
///   4) AssumptionRegistry.RegisterAllAsAssumptions() — UNSAT core için
///   5) Objective (Plan 3 Task 4 — partial scheduling):
///      Maximize(W * sum(present) - sum(penalties))
///      Primary: olabildiği kadar çok section yerleştir (heavy weight).
///      Secondary: soft penalty'leri minimize et (tie-breaker).
/// </summary>
public sealed class ScheduleModelBuilder
{
    private readonly IEnumerable<IConstraintEncoder> _encoders;

    public ScheduleModelBuilder(IEnumerable<IConstraintEncoder> encoders)
    {
        _encoders = encoders;
    }

    public ScheduleModelBuildResult Build(SolverRequest request)
    {
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: ComputeTotalSlots(request));
        var assumptions = new AssumptionRegistry(model);

        // 1) Decision variables
        foreach (var section in request.Sections)
        {
            variables.CreateIntervalsForSection(section);
            if (section.TheoryHours > 0)
                variables.CreateRoomVariable(section.Id, ComponentType.Lecture, request.Classrooms);
            if (section.LabHours > 0)
                variables.CreateRoomVariable(section.Id, ComponentType.Lab, request.Classrooms,
                    groupCount: section.LabGroupCount);
        }

        var penalties = new List<LinearExpr>();
        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = assumptions,
            Request = request,
            ConstraintFlags = request.ConstraintFlags,
            Penalties = penalties
        };

        // 2-3) Encoder'lar — hard önce, soft sonra; her grupta Code'a göre alfabetik
        var ordered = _encoders
            .OrderBy(e => e.IsHard ? 0 : 1)
            .ThenBy(e => e.Code, StringComparer.Ordinal);

        foreach (var encoder in ordered)
        {
            encoder.Encode(ctx);
        }

        // 4) UNSAT core için assumption literal'ları register et
        assumptions.RegisterAllAsAssumptions();

        // 5) Objective (Plan 3 Task 4 — partial scheduling):
        //    Maximize(W * sum(present) - sum(penalties))
        //    Primary: schedule as many sections as possible (W heavy weight).
        //    Secondary: minimize soft penalties (tie-breaker).
        const int presenceWeight = 10000;

        var presenceVars = variables.AllSectionPresent.Values
            .OfType<BoolVar>()
            .ToList();

        if (presenceVars.Count > 0)
        {
            LinearExpr objective = LinearExpr.Sum(presenceVars) * presenceWeight;
            if (penalties.Count > 0)
                objective = objective - LinearExpr.Sum(penalties);
            model.Maximize(objective);
        }
        else if (penalties.Count > 0)
        {
            // No section presence vars (shouldn't happen in normal flow) — only minimize penalties
            model.Minimize(LinearExpr.Sum(penalties));
        }

        return new ScheduleModelBuildResult(model, variables, assumptions);
    }

    private static int ComputeTotalSlots(SolverRequest request)
    {
        if (request.TimeSlots.Count == 0)
            throw new InvalidOperationException(
                "SolverRequest.TimeSlots is empty. CP-SAT model requires at least 1 time slot.");
        return request.TimeSlots.Count;
    }
}

public sealed record ScheduleModelBuildResult(
    CpModel Model,
    DecisionVariables Variables,
    AssumptionRegistry Assumptions);
