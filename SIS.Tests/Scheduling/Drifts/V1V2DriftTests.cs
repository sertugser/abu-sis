using FluentAssertions;
using SIS.Domain.Enums;
using SIS.Tests.Scheduling.Rules;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Drifts;

/// <summary>
/// Plan 11 Faz 0.2 (scaffold) → Faz 3 sonu doldurulan failing-first integration test paketi.
/// V1 evaluator ile RuleEngine (V2 mantığı) arasındaki 5 drift senaryosunu kanıtlar:
/// her testte aynı snapshot için RuleEngine V2 mantığını uygular; V1 evaluator'lar farklı
/// davranır (Plan 11 kasıtlı kararı). Faz 5'te V1 silindiğinde V1 karşılaştırması düşer,
/// RuleEngine davranışı baseline olur.
/// </summary>
public sealed class V1V2DriftTests
{
    /// <summary>
    /// H4 Amphi drift. section.Capacity=150, AmphiThreshold=100, AmphiThresholdEnabled=false.
    /// V1 (Plan 11 Faz 1.4 öncesi): her zaman uyguluyordu → conflict.
    /// V1 (Faz 1.4 sonrası — bu commit dahil): flag-gated → no conflict.
    /// RulePredicate H4: flag false → no conflict.
    /// </summary>
    [Fact]
    public async Task H4_Amphi_FlagDisabled_BigCapacity_NoConflict()
    {
        var engine = BuildEngine();
        var candidate = Candidate(id: 1, courseId: 100, capacity: 150, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, classroomCapacity: 200));
        var snapshot = Snapshot(candidate);
        var config = DefaultConfig(c =>
        {
            c.AmphiThreshold = 100;
            c.AmphiThresholdEnabled = false; // KAPALI — V1 paritesi Faz 1.4 sonrası
        });

        var conflicts = await engine.EvaluateCandidateAsync(candidate, snapshot, config, state: null, default);

        conflicts.Should().NotContain(c => c.ConflictType == "ClassroomTypeConflict",
            "AmphiThresholdEnabled=false → kural skip");
    }

    [Fact]
    public async Task H4_Amphi_FlagEnabled_BigCapacity_NonAmphiClassroom_Reports()
    {
        var engine = BuildEngine();
        var candidate = Candidate(id: 1, courseId: 100, capacity: 150, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3, classroomCapacity: 200, type: ClassroomType.Lecture));
        var snapshot = Snapshot(candidate);
        var config = DefaultConfig(c =>
        {
            c.AmphiThreshold = 100;
            c.AmphiThresholdEnabled = true; // AÇIK
        });

        var conflicts = await engine.EvaluateCandidateAsync(candidate, snapshot, config, state: null, default);

        conflicts.Should().Contain(c => c.ConflictType == "ClassroomTypeConflict"
            && c.Message.Contains("Amfi"));
    }

    /// <summary>
    /// H-DL DailyLoad config drift. Admin Schedule.MaxDailyHoursPerInstructor=10 yaptı.
    /// V1 (Plan 11 Faz 1.2 öncesi): hardcoded > 9 → conflict.
    /// V1 (Faz 1.2 sonrası): config.MaxDailyHours → no conflict.
    /// RulePredicate H-DL: config-driven → no conflict.
    /// </summary>
    [Fact]
    public async Task HDL_DailyLoad_ConfigOverride_NoConflict()
    {
        var engine = BuildEngine();
        // 10 saat tek section, instructor=1
        var candidate = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 10, lab: 0,
            blocks: Block(day: 1, startHour: 8, durationHours: 10));
        var snapshot = Snapshot(candidate);
        var config = DefaultConfig(c => c.MaxDailyHours = 10);

        var conflicts = await engine.EvaluateCandidateAsync(candidate, snapshot, config, state: null, default);

        conflicts.Should().NotContain(c => c.ConflictType == "DailyLoadConflict",
            "config 10 saat → 10 ≤ 10 OK");
    }

    [Fact]
    public async Task HDL_DailyLoad_HardcodedDefault_TenHours_Reports()
    {
        var engine = BuildEngine();
        var candidate = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 10, lab: 0,
            blocks: Block(day: 1, startHour: 8, durationHours: 10));
        var snapshot = Snapshot(candidate);
        var config = DefaultConfig(); // MaxDailyHours = 9 (default)

        var conflicts = await engine.EvaluateCandidateAsync(candidate, snapshot, config, state: null, default);

        conflicts.Should().Contain(c => c.ConflictType == "DailyLoadConflict");
    }

    /// <summary>
    /// H-DO drift. 6-günlük takvim (Cumartesi slot eklenince). Instructor 5 günde ders veriyor.
    /// V1: usedDays ≥ 5 → conflict (hardcoded).
    /// V2/Predicate: usedDays(5) ≤ workdays(6)-1=5 → no conflict.
    /// </summary>
    [Fact]
    public async Task HDO_NoDayOff_SixWorkdays_FiveUsed_NoConflict()
    {
        var engine = BuildEngine();
        // 5 farklı günde 1 saatlik bloklar — toplam 5 workday kullanılmış
        var candidate = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 1, lab: 0,
            blocks: new[]
            {
                Block(day: 1, startHour: 9, durationHours: 1),
            });
        // Diğer 4 günü dolduran başka section'lar (aynı instructor)
        var other2 = Candidate(id: 2, courseId: 200, instructorId: 1, theory: 1, lab: 0,
            blocks: Block(day: 2, startHour: 9, durationHours: 1));
        var other3 = Candidate(id: 3, courseId: 300, instructorId: 1, theory: 1, lab: 0,
            blocks: Block(day: 3, startHour: 9, durationHours: 1));
        var other4 = Candidate(id: 4, courseId: 400, instructorId: 1, theory: 1, lab: 0,
            blocks: Block(day: 4, startHour: 9, durationHours: 1));
        var other5 = Candidate(id: 5, courseId: 500, instructorId: 1, theory: 1, lab: 0,
            blocks: Block(day: 5, startHour: 9, durationHours: 1));
        // 6. günde başka instructor'ın section'ı (workdays.Count=6 olsun)
        var sat = Candidate(id: 6, courseId: 600, instructorId: 999, theory: 1, lab: 0,
            blocks: Block(day: 6, startHour: 9, durationHours: 1));

        var snapshot = Snapshot(candidate, other2, other3, other4, other5, sat);
        var config = DefaultConfig();

        var conflicts = await engine.EvaluateCandidateAsync(candidate, snapshot, config, state: null, default);

        conflicts.Should().NotContain(c => c.ConflictType == "NoDayOffConflict",
            "6 workday × 5 used → ≤ 6-1=5 OK (V2 generalize)");
    }

    /// <summary>
    /// H16 drift. 3T+2L = 5 saat > 4 splitThreshold. Theory ve lab AYNI gün ama lab sonra.
    /// V1 (revize 2026-05): aynı gün OK çünkü lab teoriden sonra → no conflict.
    /// V2/Predicate: theoryDay != labDay zorunlu → conflict (Plan 11 R7 kararı).
    /// </summary>
    [Fact]
    public async Task H16_TheoryLab_SameDay_RuleRejects()
    {
        var engine = BuildEngine();
        var candidate = Candidate(id: 1, courseId: 100, theory: 3, lab: 2,
            blocks: new[]
            {
                Block(day: 1, startHour: 9, durationHours: 3),   // theory Mon 09-12
                Block(day: 1, startHour: 13, durationHours: 2),  // lab Mon 13-15 (aynı gün, sonra)
            });
        var snapshot = Snapshot(candidate);
        var config = DefaultConfig(c => c.TheoryLabSplitThreshold = 4);

        var conflicts = await engine.EvaluateCandidateAsync(candidate, snapshot, config, state: null, default);

        conflicts.Should().Contain(c => c.ConflictType == "TheoryLabDaySplitConflict",
            "5 saat > 4 eşiği + aynı gün → V2 sıkı reddeder");
    }

    [Fact]
    public async Task H16_TheoryLab_DifferentDays_NoConflict()
    {
        var engine = BuildEngine();
        var candidate = Candidate(id: 1, courseId: 100, theory: 3, lab: 2,
            blocks: new[]
            {
                Block(day: 1, startHour: 9, durationHours: 3),   // theory Mon
                Block(day: 5, startHour: 9, durationHours: 2),   // lab Fri
            });
        var snapshot = Snapshot(candidate);
        var config = DefaultConfig(c => c.TheoryLabSplitThreshold = 4);

        var conflicts = await engine.EvaluateCandidateAsync(candidate, snapshot, config, state: null, default);

        conflicts.Should().NotContain(c => c.ConflictType == "TheoryLabDaySplitConflict");
    }
}
