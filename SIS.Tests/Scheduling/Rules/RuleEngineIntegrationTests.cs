using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Enums;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules;

/// <summary>
/// Plan 11 Faz 3.18 / 4 — RuleEngine orchestration smoke. 17 IRulePredicate'in tamamı
/// register edilmiş gerçek RuleEngine kullanılır; Plan 10 davranışları (disabled / exempt /
/// category Soft skip) ve bidirectional dedup test edilir.
/// </summary>
public sealed class RuleEngineIntegrationTests
{
    [Fact]
    public async Task NoConflicts_OnIsolatedHealthyCandidate()
    {
        var engine = BuildEngine();
        var candidate = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var snapshot = Snapshot(candidate);

        var conflicts = await engine.EvaluateCandidateAsync(candidate, snapshot, DefaultConfig(), state: null, default);
        conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task H1_DetectsClassroomConflict()
    {
        var engine = BuildEngine();
        var c1 = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var c2 = Candidate(id: 2, courseId: 200, instructorId: 2, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var snapshot = Snapshot(c1, c2);

        var conflicts = await engine.EvaluateCandidateAsync(c1, snapshot, DefaultConfig(), state: null, default);

        conflicts.Should().Contain(c => c.ConflictType == "ClassroomConflict");
    }

    [Fact]
    public async Task H1_ParallelLab_SelfSkip_NoConflict()
    {
        var engine = BuildEngine();
        // Aynı section'ın iki paralel grup bloğu (aynı pencere, farklı classroomId)
        var c = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 0, lab: 2,
            blocks: new[]
            {
                Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5, type: ClassroomType.Lab),
                Block(day: 1, startHour: 9, durationHours: 2, classroomId: 6, type: ClassroomType.Lab),
            });
        var snapshot = Snapshot(c);

        var conflicts = await engine.EvaluateCandidateAsync(c, snapshot, DefaultConfig(), state: null, default);

        conflicts.Should().NotContain(x => x.ConflictType == "ClassroomConflict",
            "Aynı section'ın paralel grup blokları self-skip (Plan 10 fix korundu)");
    }

    [Fact]
    public async Task H2_InstructorConflict_Detected()
    {
        var engine = BuildEngine();
        var c1 = Candidate(id: 1, courseId: 100, instructorId: 10, theory: 2, lab: 0,
            blocks: Block(day: 2, startHour: 13, durationHours: 2, classroomId: 1));
        var c2 = Candidate(id: 2, courseId: 200, instructorId: 10, theory: 2, lab: 0,
            blocks: Block(day: 2, startHour: 13, durationHours: 2, classroomId: 2));
        var snapshot = Snapshot(c1, c2);

        var conflicts = await engine.EvaluateCandidateAsync(c1, snapshot, DefaultConfig(), state: null, default);
        conflicts.Should().Contain(c => c.ConflictType == "InstructorConflict");
    }

    [Fact]
    public async Task H13_LunchBreak_DefaultHard_Detected()
    {
        // H13 DefaultCategory=Hard (state null → predicate.DefaultCategory). RuleEngine
        // hard path'e gider, predicate öğle arası bloğunu hard ihlal olarak rapor eder.
        var engine = BuildEngine();
        var c = Candidate(id: 1, courseId: 100, theory: 1, lab: 0,
            blocks: Block(day: 1, startHour: 12, durationHours: 1)); // 12:00-13:00 öğle arasıyla çakışır
        var snapshot = Snapshot(c);
        var config = DefaultConfig();

        var conflicts = await engine.EvaluateCandidateAsync(c, snapshot, config, state: null, default);
        conflicts.Should().Contain(c => c.ConflictType == "LunchBreakConflict");
    }

    [Fact]
    public async Task EvaluateAllExisting_DeduplicatesBidirectional()
    {
        var engine = BuildEngine();
        // İki section aynı classroom'da aynı slot'ta çakışıyor → H1 her iki yönde de violation
        // üretebilir; RuleEngine dedup ile tek satır olarak döner.
        var c1 = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var c2 = Candidate(id: 2, courseId: 200, instructorId: 2, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var snapshot = Snapshot(c1, c2);

        var conflicts = await engine.EvaluateAllExistingAsync(snapshot, DefaultConfig(), state: null, default);
        var classroomConflicts = conflicts.Where(c => c.ConflictType == "ClassroomConflict").ToList();
        classroomConflicts.Should().HaveCount(1, "Bidirectional dedup → tek conflict kaydı");
    }

    [Fact]
    public async Task Plan10_DisabledConstraint_Skipped()
    {
        // Plan 10 — ConstraintDefinitions.IsEnabled=false ise predicate çağrılmaz.
        var engine = BuildEngine();
        var c1 = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var c2 = Candidate(id: 2, courseId: 200, instructorId: 2, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var snapshot = Snapshot(c1, c2);

        // H1'i disable et
        var definitions = new Dictionary<string, ConstraintDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["H1"] = new ConstraintDefinition
            {
                Code = "H1", Name = "ClassroomConflict", Category = "Hard",
                Scope = "Scheduling", IsEnabled = false,
            },
        };
        var state = new ConstraintState(definitions, exceptions: new List<ConstraintException>());

        var conflicts = await engine.EvaluateCandidateAsync(c1, snapshot, DefaultConfig(), state, default);

        conflicts.Should().NotContain(c => c.ConflictType == "ClassroomConflict",
            "H1 IsEnabled=false → predicate skip");
    }

    [Fact]
    public async Task Plan10_SoftCategory_HardPredicateSkipped()
    {
        // Plan 10 — DB Category="Soft" ise hard predicate çalışmaz (effectiveCategory != Hard skip).
        var engine = BuildEngine();
        var c1 = Candidate(id: 1, courseId: 100, instructorId: 1, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var c2 = Candidate(id: 2, courseId: 200, instructorId: 2, theory: 2, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var snapshot = Snapshot(c1, c2);

        var definitions = new Dictionary<string, ConstraintDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["H1"] = new ConstraintDefinition
            {
                Code = "H1", Name = "ClassroomConflict", Category = "Soft",
                Scope = "Scheduling", IsEnabled = true,
            },
        };
        var state = new ConstraintState(definitions, exceptions: new List<ConstraintException>());

        var conflicts = await engine.EvaluateCandidateAsync(c1, snapshot, DefaultConfig(), state, default);

        conflicts.Should().NotContain(c => c.ConflictType == "ClassroomConflict",
            "H1 Soft → hard path skip");
    }
}
