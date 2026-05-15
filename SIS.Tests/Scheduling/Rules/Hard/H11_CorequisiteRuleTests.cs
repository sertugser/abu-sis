using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H11_CorequisiteRuleTests
{
    [Fact]
    public void AllCombinationsConflict_Reports()
    {
        // Course 100 ↔ Course 200 corequisite. Candidate 100/section1 9-11, paired 200/section2 9-11.
        // Sadece bu iki section var, çakışıyor → çakışmasız kombinasyon yok → violation.
        var rule = new H11_CorequisiteRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var paired = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c, paired },
            Corequisites = new[] { new CorequisitePair(100, 200) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("CorequisiteConflict");
    }

    [Fact]
    public void HasNonConflictingCombo_NoViolation()
    {
        // İki paired section'dan en az biri çakışmıyor → öğrenci aday section kombinasyonu bulabilir.
        var rule = new H11_CorequisiteRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var paired1 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var paired2 = Candidate(id: 3, courseId: 200, blocks: Block(day: 2, startHour: 9, durationHours: 2));

        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c, paired1, paired2 },
            Corequisites = new[] { new CorequisitePair(100, 200) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoMatchingPair_NoViolation()
    {
        // Candidate course 100; corequisite (300, 400) — candidate'ı kapsamıyor.
        var rule = new H11_CorequisiteRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c },
            Corequisites = new[] { new CorequisitePair(300, 400) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoCorequisitesInSnapshot_NoViolation()
    {
        var rule = new H11_CorequisiteRule();
        var c = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
