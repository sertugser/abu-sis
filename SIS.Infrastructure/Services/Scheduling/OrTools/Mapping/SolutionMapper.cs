using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;

/// <summary>
/// CP-SAT solver response → SolverResult dönüştürücüsü.
/// Plan 1: MapInfeasible only (PoC).
/// Plan 2 Task 18: MapSuccess + MapTimeout added — gerçek solution variable values'ından
/// Assignment record'larını okur.
/// </summary>
public sealed class SolutionMapper
{
    public SolverResult MapInfeasible(
        IReadOnlyList<UnsatCoreEntry> unsatCore,
        SolverRequest request,
        double elapsedSeconds)
    {
        var failures = request.Sections
            .Select(s => new Failure
            {
                SectionId = s.Id,
                Reason = unsatCore.Count > 0
                    ? unsatCore[0].HumanReason
                    : "Çözüm bulunamadı (kısıtlamalar matematiksel olarak çelişkili).",
                RelatedConstraints = unsatCore
            })
            .ToList();

        return new SolverResult
        {
            Status = SolverStatus.Infeasible,
            Assignments = new List<Assignment>(),
            Failures = failures,
            Diagnostics = new SolverDiagnostics
            {
                EngineVersion = "v2-ortools",
                ElapsedSeconds = elapsedSeconds,
                Branches = 0,
                Conflicts = 0,
                ObjectiveValue = 0,
                UnsatCore = unsatCore
            }
        };
    }

    /// <summary>
    /// CP-SAT Optimal/Feasible çözümü Assignment record listesine dönüştürür.
    /// Solver'dan interval start/end ve room IntVar değerlerini okur, slot index → TimeSlot.Id mapping yapar.
    /// </summary>
    public SolverResult MapSuccess(
        CpSolver solver,
        ScheduleModelBuildResult build,
        SolverRequest request,
        double elapsedSeconds,
        CpSolverStatus status)
    {
        var assignments = new List<Assignment>();

        foreach (var section in request.Sections)
        {
            var slots = new List<AssignedSlot>();

            // Lecture component
            if (section.TheoryHours > 0)
            {
                var lectureInterval = build.Variables.GetLectureInterval(section.Id);
                if (lectureInterval != null)
                {
                    var roomId = (int)solver.Value(build.Variables.GetLectureRoom(section.Id));
                    var startSlotIdx = (int)solver.Value(lectureInterval.StartExpr());
                    var endSlotIdx = (int)solver.Value(lectureInterval.EndExpr());

                    for (int i = startSlotIdx; i < endSlotIdx; i++)
                    {
                        if (i >= 0 && i < request.TimeSlots.Count)
                        {
                            slots.Add(new AssignedSlot
                            {
                                TimeSlotId = request.TimeSlots[i].Id,
                                ClassroomId = roomId,
                                ComponentType = ComponentType.Lecture
                            });
                        }
                    }
                }
            }

            // Lab component
            if (section.LabHours > 0)
            {
                var labInterval = build.Variables.GetLabInterval(section.Id);
                if (labInterval != null)
                {
                    var roomId = (int)solver.Value(build.Variables.GetLabRoom(section.Id));
                    var startSlotIdx = (int)solver.Value(labInterval.StartExpr());
                    var endSlotIdx = (int)solver.Value(labInterval.EndExpr());

                    for (int i = startSlotIdx; i < endSlotIdx; i++)
                    {
                        if (i >= 0 && i < request.TimeSlots.Count)
                        {
                            slots.Add(new AssignedSlot
                            {
                                TimeSlotId = request.TimeSlots[i].Id,
                                ClassroomId = roomId,
                                ComponentType = ComponentType.Lab
                            });
                        }
                    }
                }
            }

            if (slots.Count > 0)
            {
                assignments.Add(new Assignment { SectionId = section.Id, Slots = slots });
            }
        }

        return new SolverResult
        {
            Status = status == CpSolverStatus.Optimal ? SolverStatus.Optimal : SolverStatus.Feasible,
            Assignments = assignments,
            Failures = new List<Failure>(),
            Diagnostics = new SolverDiagnostics
            {
                EngineVersion = "v2-ortools",
                ElapsedSeconds = elapsedSeconds,
                Branches = SafeNumBranches(solver),
                Conflicts = SafeNumConflicts(solver),
                ObjectiveValue = SafeObjectiveValue(solver),
                UnsatCore = new List<UnsatCoreEntry>()
            }
        };
    }

    /// <summary>
    /// CP-SAT timeout/unknown durumunda partial result. Plan 2 simplification:
    /// tüm section'lar failure olarak listelenir; partial assignments Plan 3'te eklenebilir.
    /// </summary>
    public SolverResult MapTimeout(
        CpSolver solver,
        ScheduleModelBuildResult build,
        SolverRequest request,
        double elapsedSeconds)
    {
        var failures = request.Sections.Select(s => new Failure
        {
            SectionId = s.Id,
            Reason = $"Çözücü {elapsedSeconds:F1} saniyede çözüm bulamadı (timeout). Daha uzun süre veya farklı seed ile tekrar deneyin."
        }).ToList();

        return new SolverResult
        {
            Status = SolverStatus.Timeout,
            Assignments = new List<Assignment>(),
            Failures = failures,
            Diagnostics = new SolverDiagnostics
            {
                EngineVersion = "v2-ortools",
                ElapsedSeconds = elapsedSeconds,
                Branches = SafeNumBranches(solver),
                Conflicts = SafeNumConflicts(solver),
                ObjectiveValue = 0,
                UnsatCore = new List<UnsatCoreEntry>()
            }
        };
    }

    // CpSolver stat methods may throw if no solve has run yet — keep mapper defensive.
    private static long SafeNumBranches(CpSolver solver)
    {
        try { return solver.NumBranches(); }
        catch { return 0; }
    }

    private static long SafeNumConflicts(CpSolver solver)
    {
        try { return solver.NumConflicts(); }
        catch { return 0; }
    }

    private static double SafeObjectiveValue(CpSolver solver)
    {
        try { return solver.ObjectiveValue; }
        catch { return 0; }
    }
}
