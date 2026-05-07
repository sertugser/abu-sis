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
///   5) Objective: model.Minimize(LinearExpr.Sum(penalties))
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
                variables.CreateRoomVariable(section.Id, ComponentType.Lab, request.Classrooms);
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

        // 5) Objective: penalty toplam minimize
        if (penalties.Count > 0)
            model.Minimize(LinearExpr.Sum(penalties));

        return new ScheduleModelBuildResult(model, variables, assumptions);
    }

    private static int ComputeTotalSlots(SolverRequest request)
    {
        return request.TimeSlots.Count > 0 ? request.TimeSlots.Count : 50;
    }
}

public sealed record ScheduleModelBuildResult(
    CpModel Model,
    DecisionVariables Variables,
    AssumptionRegistry Assumptions);
