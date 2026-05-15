using Microsoft.Extensions.DependencyInjection;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.Evaluators;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Infrastructure.Services.Scheduling;

public static class SchedulingServiceCollectionExtensions
{
    /// <summary>
    /// Registers all scheduling constraint evaluators and the orchestrator service.
    /// </summary>
    public static IServiceCollection AddSchedulingConstraints(this IServiceCollection services)
    {
        // Hard constraint evaluators
        services.AddScoped<IConstraintEvaluator, CapacityEvaluator>();
        services.AddScoped<IConstraintEvaluator, ClassroomTypeEvaluator>();
        services.AddScoped<IConstraintEvaluator, FacultyClassroomEvaluator>();
        services.AddScoped<IConstraintEvaluator, ClassroomConflictEvaluator>();
        services.AddScoped<IConstraintEvaluator, InstructorConflictEvaluator>();
        services.AddScoped<IConstraintEvaluator, CurriculumConflictEvaluator>();
        services.AddScoped<IConstraintEvaluator, CourseComponentEvaluator>();
        services.AddScoped<IConstraintEvaluator, SameCourseSectionEvaluator>();
        services.AddScoped<IConstraintEvaluator, WeeklyLoadEvaluator>();
        services.AddScoped<IConstraintEvaluator, DailyLoadEvaluator>();
        services.AddScoped<IConstraintEvaluator, NoDayOffEvaluator>();
        services.AddScoped<IConstraintEvaluator, CorequisiteEvaluator>();
        services.AddScoped<IConstraintEvaluator, CreditHourEvaluator>();
        services.AddScoped<IConstraintEvaluator, LunchBreakEvaluator>();
        services.AddScoped<IConstraintEvaluator, MaxConsecutiveHoursEvaluator>();
        services.AddScoped<IConstraintEvaluator, TheoryLabDaySplitEvaluator>();
        services.AddScoped<IConstraintEvaluator, FaceToFaceSlotRestrictionEvaluator>();

        // Soft constraint evaluators
        services.AddScoped<IConstraintEvaluator, OccupancyEvaluator>();
        services.AddScoped<IConstraintEvaluator, DailyLoadSoftEvaluator>();
        services.AddScoped<IConstraintEvaluator, SlotCongestionEvaluator>();
        services.AddScoped<IConstraintEvaluator, CoEnrollmentEvaluator>();
        services.AddScoped<IConstraintEvaluator, LunchBreakSoftEvaluator>();
        services.AddScoped<IConstraintEvaluator, StudentGapEvaluator>();
        services.AddScoped<IConstraintEvaluator, InstructorDaySpreadEvaluator>();
        services.AddScoped<IConstraintEvaluator, WeekdayBalanceEvaluator>();
        services.AddScoped<IConstraintEvaluator, AdjacentYearConflictEvaluator>();
        // Distribution constraint evaluator (hard/soft depending on preference level)
        services.AddScoped<IConstraintEvaluator, DistributionConstraintEvaluator>();

        // Orchestrator
        services.AddScoped<ConstraintEvaluationService>();

        // Auto-schedule engine
        services.AddScoped<AutoScheduleEngine>();

        // Single canonical SectionSchedule writer (replaces 2 duplicate impls — 2026-05-10)
        services.AddScoped<IScheduleWriter, ScheduleWriter>();

        // Approval workflow
        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();

        // What-if simulation engine
        services.AddScoped<ISimulationService, SimulationService>();

        // Constraint snapshot / analytics
        services.AddScoped<IConstraintSnapshotService, ConstraintSnapshotService>();

        // ── OR-Tools v2 Engine ───────────────────────────────────────────
        // Encoder'lar (Faz 2 PoC: sadece H1)
        services.AddScoped<IConstraintEncoder, ClassroomConflictEncoder>();
        services.AddScoped<IConstraintEncoder, InstructorConflictEncoder>();
        services.AddScoped<IConstraintEncoder, WeeklyLoadEncoder>();
        services.AddScoped<IConstraintEncoder>(sp =>
        {
            // H-DL — config-driven max daily hours per instructor (sync DI factory: 5min cache → DB call rare).
            var config = sp.GetRequiredService<ISystemConfigProvider>();
            var maxHours = config.GetIntAsync("Schedule.MaxDailyHoursPerInstructor", 9, default).GetAwaiter().GetResult();
            return new DailyLoadEncoder(maxHours);
        });
        services.AddScoped<IConstraintEncoder, NoDayOffEncoder>();
        services.AddScoped<IConstraintEncoder, CapacityEncoder>();
        services.AddScoped<IConstraintEncoder, ClassroomTypeEncoder>();
        services.AddScoped<IConstraintEncoder, CurriculumConflictEncoder>();
        services.AddScoped<IConstraintEncoder, TheoryLabComponentEncoder>();
        services.AddScoped<IConstraintEncoder, SameCourseSectionEncoder>();
        services.AddScoped<IConstraintEncoder, FacultyClassroomEncoder>();
        services.AddScoped<IConstraintEncoder, CorequisiteEncoder>();
        services.AddScoped<IConstraintEncoder>(sp =>
        {
            // H16 — config-driven splitThreshold (sync DI factory: heavy 5min cache → DB call rare).
            var config = sp.GetRequiredService<ISystemConfigProvider>();
            var threshold = config.GetIntAsync("Schedule.TheoryLabSplitThreshold", 4, default).GetAwaiter().GetResult();
            return new TheoryLabDaySplitEncoder(threshold);
        });
        services.AddScoped<IConstraintEncoder>(sp =>
        {
            // H13 — config-driven lunch window (sync DI factory: 5min cache → DB call rare).
            // Admin DB'den 11:40→11:45 değiştirebilir; cache yenilenince effective olur.
            var config = sp.GetRequiredService<ISystemConfigProvider>();
            var startStr = config.GetStringAsync("Schedule.LunchBreakStart", "11:40", default).GetAwaiter().GetResult();
            var endStr = config.GetStringAsync("Schedule.LunchBreakEnd", "12:20", default).GetAwaiter().GetResult();
            // Defansif fallback: malformed config → defaults (admin UI validation HH:mm formatını zorlar).
            var start = TimeOnly.TryParse(startStr, out var s) ? s : new TimeOnly(11, 40);
            var end = TimeOnly.TryParse(endStr, out var e) ? e : new TimeOnly(12, 20);
            return new LunchBreakEncoder(start, end);
        });
        services.AddScoped<IConstraintEncoder>(sp =>
        {
            // H15 — Plan 5 Task 8b: slot coverage matrix sliding-window cumulative constraint.
            // Reads max consecutive hours + break duration (between-slots gap threshold) from SystemConfig.
            var config = sp.GetRequiredService<ISystemConfigProvider>();
            var maxHours = config.GetIntAsync("Schedule.MaxConsecutiveHoursPerDay", 4, default).GetAwaiter().GetResult();
            var breakMinutes = config.GetIntAsync("Schedule.BreakBetweenSlotsMinutes", 5, default).GetAwaiter().GetResult();
            return new MaxConsecutiveHoursEncoder(maxHours, breakMinutes);
        });
        services.AddScoped<IConstraintEncoder, FaceToFaceSlotEncoder>();
        services.AddScoped<IConstraintEncoder, DistributionConstraintEncoder>();

        // Soft constraint encoders (Plan 3 diagnostic stubs; real penalty in Plan 4)
        services.AddScoped<IConstraintEncoder, OccupancyEncoder>();
        services.AddScoped<IConstraintEncoder, SlotCongestionEncoder>();
        services.AddScoped<IConstraintEncoder, LunchBreakSoftEncoder>();
        services.AddScoped<IConstraintEncoder, DailyLoadSoftEncoder>();
        services.AddScoped<IConstraintEncoder, InstructorDaySpreadEncoder>();
        services.AddScoped<IConstraintEncoder, CoEnrollmentEncoder>();
        services.AddScoped<IConstraintEncoder, StudentGapEncoder>();
        services.AddScoped<IConstraintEncoder, WeekdayBalanceEncoder>();
        services.AddScoped<IConstraintEncoder, AdjacentYearEncoder>();

        // Plan 10 Phase 3 — Soft variants for Hard→Soft toggleable rules.
        // Default Category="Hard" → these stay no-op; admin "Tercihli" toggle activates penalty mode.
        services.AddScoped<IConstraintEncoder, ClassroomTypeSoftEncoder>();        // H4 STUB
        services.AddScoped<IConstraintEncoder, CurriculumConflictSoftEncoder>();   // H5
        services.AddScoped<IConstraintEncoder, SameCourseSectionSoftEncoder>();    // H8
        services.AddScoped<IConstraintEncoder, FacultyClassroomSoftEncoder>();     // H10 STUB
        services.AddScoped<IConstraintEncoder>(sp =>
        {
            // H15 soft uses same MaxConsecutiveHours config as hard encoder.
            var config = sp.GetRequiredService<ISystemConfigProvider>();
            var maxHours = config.GetIntAsync("Schedule.MaxConsecutiveHoursPerDay", 4, default).GetAwaiter().GetResult();
            return new MaxConsecutiveHoursSoftEncoder(maxHours);
        });
        services.AddScoped<IConstraintEncoder, TheoryLabDaySplitSoftEncoder>();    // H16
        services.AddScoped<IConstraintEncoder, WeeklyLoadSoftEncoder>();           // H-WL
        services.AddScoped<IConstraintEncoder, NoDayOffSoftEncoder>();             // H-DO

        // Diagnostics
        services.AddScoped<UnsatCoreInterpreter>();

        // Pre-flight feasibility checker (Plan 3 Task 1+2 — partial scheduling)
        services.AddScoped<PreflightFeasibilityChecker>();

        // DB → SolverRequest builder (Plan 2 Task 17 — AutoScheduleEngine ve Run 303 testi kullanır)
        services.AddScoped<SolverRequestBuilder>();

        // Model builder + mapper
        services.AddScoped<ScheduleModelBuilder>();
        services.AddScoped<SolutionMapper>();

        // Engine
        services.AddScoped<ISolverEngine, CpSatSolverEngine>();

        // ── Plan 11 — RuleEngine (Faz 2.4) ─────────────────────────────
        // IRulePredicate'lar Faz 3'te per-rule eklendikçe DI'a register edilir.
        // RuleEngine boş predicate seti ile bile boş violation döner — safe scaffold.
        services.AddScoped<SIS.Application.Scheduling.Rules.IRuleEngine, Rules.RuleEngine>();

        // Faz 3 — IRulePredicate kayıtları (17 hard kural)
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H1_ClassroomConflictRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H2_InstructorConflictRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H3_CapacityRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H4_ClassroomTypeRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H5_CurriculumConflictRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H6_TheoryLabComponentRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H8_SameCourseSectionRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H10_FacultyClassroomRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H11_CorequisiteRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H12_CreditHourRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H13_LunchBreakRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H15_MaxConsecutiveHoursRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H16_TheoryLabDaySplitRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.H17_FaceToFaceSlotRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.HDL_DailyLoadRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.HDO_NoDayOffRule>();
        services.AddScoped<SIS.Application.Scheduling.Rules.IRulePredicate, Rules.Hard.HWL_WeeklyLoadRule>();

        return services;
    }
}
