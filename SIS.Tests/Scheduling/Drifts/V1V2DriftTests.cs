using Xunit;

namespace SIS.Tests.Scheduling.Drifts;

/// <summary>
/// Plan 11 Faz 0.2 — V1 evaluator ile V2 encoder mantığı arasında doğrulanmış / şüpheli
/// drift'ler için failing-first integration test paketi. Faz 3'te ilgili IRulePredicate'ler
/// hazır olunca her test'in [Fact(Skip="...")] flag'i kaldırılır ve testler V2 mantığını
/// doğrular (V1 ile kasten farklı sonuç verebilir — bu drift'in V2 lehine kapandığının
/// kanıtıdır).
///
/// Drift tablosu: <c>docs/CODEMAPS/v1-v2-drift-matrix.md</c>.
/// </summary>
public sealed class V1V2DriftTests
{
    /// <summary>
    /// H4 Amphi flag drift. AmphiThreshold=100, AmphiThresholdEnabled=false (default),
    /// section.Capacity=150, atanmış oda Amphitheater değil.
    /// V2 encoder: kural skip → atama izinli.
    /// V1 evaluator (mevcut): her zaman uyguluyor → conflict.
    /// Faz 1.4 evaluator patch'i flag-gate ekledikten sonra hem V1 hem V2 aynı: izinli.
    /// </summary>
    [Fact(Skip = "Phase 1.4 / 3.4 in progress — predicate not implemented yet")]
    public void H4_Amphi_FlagDisabled_BigCapacity_NoConflict()
    {
        // Arrange: build TermSnapshot with a section.Capacity=150 assigned to a Lecture-type classroom.
        //          Config: AmphiThreshold=100, AmphiThresholdEnabled=false.
        // Act: V1 ClassroomTypeEvaluator.Evaluate(ctx) → currently produces conflict.
        //      RulePredicate H4 (Faz 3.4 sonrası) → no conflict.
        // Assert: After Faz 3.4, no conflict. (V1 path: still conflict — proves drift exists.)
        Assert.Fail("Faz 3.4 implementation required");
    }

    /// <summary>
    /// H-DL DailyLoad hardcoded 9 drift. Admin Schedule.MaxDailyHoursPerInstructor=10 yaptı.
    /// Bir hocaya 10 saat günlük yük: V1 hardcoded "&gt; 9" → conflict. V2 config 10 → OK.
    /// Faz 1.2 patch'i evaluator'ı config-aware yaptı → drift kapanır.
    /// </summary>
    [Fact(Skip = "Phase 1.2 / 3.DL in progress")]
    public void HDL_DailyLoad_ConfigOverride_NoConflict()
    {
        // Arrange: instructor with 10h on Monday, config MaxDailyHoursPerInstructor=10.
        // Act: V1 DailyLoadEvaluator (post Faz 1.2) → no conflict.
        //      RulePredicate H-DL → no conflict.
        // Assert: no conflict; mesaj formatı: "... (maks 10 saat)."
        Assert.Fail("Faz 1.2 / 3.DL implementation required");
    }

    /// <summary>
    /// H-DO NoDayOff 6+ workday drift. Term'de Cumartesi de slot var (workdays=6).
    /// V1 hardcoded "&gt;= 5" → 5 günde ders varsa conflict; V2 "≤ workdays-1=5" → izinli.
    /// </summary>
    [Fact(Skip = "Phase 3.DO in progress")]
    public void HDO_NoDayOff_SixWorkdays_FiveUsed_NoConflict()
    {
        // Arrange: term with 6 workdays (Mon-Sat), instructor active on 5 days.
        // Act: RulePredicate H-DO → no conflict (usedDays=5 ≤ workdays-1=5).
        //      V1 evaluator (kasten — driftin kasıtlı silinmesi): conflict (>= 5).
        // Assert: predicate=OK, V1=conflict.
        Assert.Fail("Faz 3.DO implementation required");
    }

    /// <summary>
    /// H-WL WeeklyLoad default fallback drift. instructor.MaxWeeklyHours=0,
    /// section toplam 25 saat (yüksek). V1: disabled → no conflict. V2: default 30 → no conflict.
    /// Aynı sonuç bu örnekte. Drift: section toplam 35 saat (default'u aşan):
    /// V1 disabled → no conflict. V2: 35 > 30 → conflict.
    /// </summary>
    [Fact(Skip = "Phase 3.WL in progress")]
    public void HWL_WeeklyLoad_InstructorZeroOverride_DefaultFallback_AppliesLimit()
    {
        // Arrange: instructor.MaxWeeklyHours=0, total assigned hours=35.
        //          Config DefaultInstructorMaxWeeklyHours=30.
        // Act: RulePredicate H-WL → conflict (35 > 30).
        //      V1 evaluator → no conflict (disabled).
        // Assert: predicate=conflict, V1=OK.
        Assert.Fail("Faz 3.WL implementation required");
    }

    /// <summary>
    /// H16 TheoryLab same-day drift. 3T+2L ders, theory Pzt 09:00-12:00, lab Pzt 13:00-15:00.
    /// V1 (revize 2026-05): aynı gün OK çünkü lab teoriden sonra. V2: theoryDay != labDay → conflict.
    /// Plan 11 kararı: V2 sıkı tutulur.
    /// </summary>
    [Fact(Skip = "Phase 3.16 in progress")]
    public void H16_TheoryLab_SameDayLabAfter_RuleRejects_V1Accepts()
    {
        // Arrange: 3T+2L section, theory Mon 09:00-12:00, lab Mon 13:00-15:00, total=5 > splitThreshold=4.
        // Act: RulePredicate H16 → conflict (theoryDay == labDay).
        //      V1 evaluator → OK (lab teoriden sonra aynı gün izinli).
        // Assert: predicate=conflict, V1=OK.
        Assert.Fail("Faz 3.16 implementation required");
    }
}
