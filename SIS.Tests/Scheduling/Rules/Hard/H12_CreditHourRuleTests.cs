using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H12_CreditHourRuleTests
{
    [Fact]
    public void AssignedHoursMatchesExpected_NoConflict()
    {
        var rule = new H12_CreditHourRule();
        // 3T+0L = 3 saat, 3 saat atanmış
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 3));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void AssignedHoursLessThanExpected_NoConflict()
    {
        // Kısmi atama desteği: H12 artık eksik atamayı izinli sayar — section'ın atanmamış
        // bölümü (örn 3T+2L'in Lab parçası) henüz drag edilmemiş olabilir; bu durum section'ı
        // "Partial" status'a düşürür, hard violation üretmez.
        var rule = new H12_CreditHourRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void AssignedHoursMoreThanExpected_Reports()
    {
        // Fazla atama hâlâ HARD fail — kullanıcı yanlışlıkla fazla slot eklediğinde sistem reddeder.
        var rule = new H12_CreditHourRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0,
            blocks: Block(day: 1, startHour: 9, durationHours: 5));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().Contain(v => v.ConflictType == "CreditHourMismatch");
    }

    [Fact]
    public void NoBlocks_SkipsRule()
    {
        var rule = new H12_CreditHourRule();
        var c = Candidate(id: 1, courseId: 100, theory: 3, lab: 0);
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void ParallelSplit_SameWindowCountsOnce()
    {
        var rule = new H12_CreditHourRule();
        // 0T+2L, paralel split iki oda aynı pencerede — 2 saat olarak sayılır
        var c = Candidate(id: 1, courseId: 100, theory: 0, lab: 2, blocks: new[]
        {
            Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5),
            Block(day: 1, startHour: 9, durationHours: 2, classroomId: 6),
        });
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
