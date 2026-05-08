using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
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
    // Plan 7b — Online section sentinel: real classroom Ids start at 1 (DB convention).
    private const int NoRoomSentinel = 0;

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
    /// Plan 3 Task 4: presence == 0 olan section'ları Failures listesine ekler (partial scheduling).
    /// </summary>
    public SolverResult MapSuccess(
        CpSolver solver,
        ScheduleModelBuildResult build,
        SolverRequest request,
        double elapsedSeconds,
        CpSolverStatus status)
    {
        var assignments = new List<Assignment>();
        var failures = new List<Failure>();

        foreach (var section in request.Sections)
        {
            // Plan 3 Task 4: Solver bu section'ı drop ettiyse (present == 0) failures'a ekle
            bool isPresent = ReadPresenceValue(solver, build, section.Id);
            if (!isPresent)
            {
                failures.Add(new Failure
                {
                    SectionId = section.Id,
                    Reason = "Solver bu section'ı yerleştiremedi; kısıtlamalar nedeniyle çıkarıldı, diğer section'lar yerleşti."
                });
                continue;
            }

            var slots = new List<AssignedSlot>();

            // Plan 7b — Online section'lar için room IntVar yok; ClassroomId=0 sentinel emit edilir.
            // Real classroom Id'ler 1'den başlar (DB convention) → 0 ile çakışmaz.
            bool isOnline = section.TeachingMethod == TeachingMethod.Online;

            // Lecture component
            if (section.TheoryHours > 0)
            {
                var lectureInterval = build.Variables.GetLectureInterval(section.Id);
                if (lectureInterval != null)
                {
                    var roomId = isOnline
                        ? NoRoomSentinel
                        : (int)solver.Value(build.Variables.GetLectureRoom(section.Id));
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

            // Lab component (Plan 4 Task 7 — paralel split: emit one AssignedSlot per group×slot).
            // groupCount=1 (default) → outer foreach runs once → semantically identical to old shim path.
            // groupCount>1 → N entries per slot, each with a different ClassroomId (parallel labs).
            if (section.LabHours > 0)
            {
                var labInterval = build.Variables.GetLabInterval(section.Id);
                if (labInterval != null)
                {
                    var startSlotIdx = (int)solver.Value(labInterval.StartExpr());
                    var endSlotIdx = (int)solver.Value(labInterval.EndExpr());

                    if (isOnline)
                    {
                        // Online: room IntVar yok; ClassroomId=0 sentinel ile LabGroupCount kez emit et.
                        for (int g = 0; g < section.LabGroupCount; g++)
                        {
                            for (int i = startSlotIdx; i < endSlotIdx; i++)
                            {
                                if (i >= 0 && i < request.TimeSlots.Count)
                                {
                                    slots.Add(new AssignedSlot
                                    {
                                        TimeSlotId = request.TimeSlots[i].Id,
                                        ClassroomId = NoRoomSentinel,
                                        ComponentType = ComponentType.Lab
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        var labRooms = build.Variables.GetLabRooms(section.Id);

                        foreach (var roomVar in labRooms)
                        {
                            var roomId = (int)solver.Value(roomVar);
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
            Failures = failures,
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
    /// Section'ın presence boolean'ını solver'dan okur. Plan 3 Task 4 — partial scheduling.
    /// Defensive: if presence can't be read, assume present (preserve old all-or-nothing behavior).
    /// </summary>
    private static bool ReadPresenceValue(CpSolver solver, ScheduleModelBuildResult build, int sectionId)
    {
        try
        {
            var presence = build.Variables.GetSectionPresent(sectionId);
            // CP-SAT C# binding: presence literals created via NewBoolVar; BoolVar : IntVar
            if (presence is IntVar intVar)
                return solver.Value(intVar) != 0L;
            // Fallback: ILiteral may not be IntVar (e.g. negation); skip and assume present
            return true;
        }
        catch
        {
            return true;
        }
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
