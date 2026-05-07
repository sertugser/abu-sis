using Microsoft.Extensions.DependencyInjection;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.Evaluators;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
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
        services.AddScoped<IConstraintEvaluator, EarlyLateEvaluator>();
        services.AddScoped<IConstraintEvaluator, WeekdayBalanceEvaluator>();
        services.AddScoped<IConstraintEvaluator, AdjacentYearConflictEvaluator>();
        // Distribution constraint evaluator (hard/soft depending on preference level)
        services.AddScoped<IConstraintEvaluator, DistributionConstraintEvaluator>();

        // Orchestrator
        services.AddScoped<ConstraintEvaluationService>();

        // Auto-schedule engine
        services.AddScoped<AutoScheduleEngine>();

        // Approval workflow
        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();

        // What-if simulation engine
        services.AddScoped<ISimulationService, SimulationService>();

        // Constraint snapshot / analytics
        services.AddScoped<IConstraintSnapshotService, ConstraintSnapshotService>();

        // ── OR-Tools v2 Engine ───────────────────────────────────────────
        // Encoder'lar (Faz 2 PoC: sadece H1)
        services.AddScoped<IConstraintEncoder, ClassroomConflictEncoder>();
        services.AddScoped<IConstraintEncoder, CapacityEncoder>();

        // Model builder + mapper
        services.AddScoped<ScheduleModelBuilder>();
        services.AddScoped<SolutionMapper>();

        // Engine
        services.AddScoped<ISolverEngine, CpSatSolverEngine>();

        return services;
    }
}
