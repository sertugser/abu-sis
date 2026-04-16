using Microsoft.Extensions.DependencyInjection;
using SIS.Application.Scheduling.Constraints;
using SIS.Infrastructure.Services.Scheduling.Evaluators;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

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
        services.AddScoped<IConstraintEvaluator, InstructorTimePreferenceEvaluator>();
        services.AddScoped<IConstraintEvaluator, InstructorClassroomPreferenceEvaluator>();

        // Distribution constraint evaluator (hard/soft depending on preference level)
        services.AddScoped<IConstraintEvaluator, DistributionConstraintEvaluator>();

        // Orchestrator
        services.AddScoped<ConstraintEvaluationService>();

        // Auto-schedule engine
        services.AddScoped<AutoScheduleEngine>();

        return services;
    }
}
