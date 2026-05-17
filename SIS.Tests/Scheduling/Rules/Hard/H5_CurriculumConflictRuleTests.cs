using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H5_CurriculumConflictRuleTests
{
    [Fact]
    public void CurriculumMode_SharedDeptSameSemester_Reports()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("CurriculumConflict");
    }

    [Fact]
    public void CurriculumMode_DifferentSemester_NoConflict()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [10] = 5 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CurriculumMode_DifferentDept_NoConflict()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [20] = 3 },
            accessibleDepts: new[] { 20 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CurriculumMode_SameCourseId_NoConflict()
    {
        // existing.CourseId == c.CourseId → skip (same course, farklı section'ları H8 yakalar)
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void CohortMode_PairHit_Reports()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c1, c2 },
            CohortConflictPairs = new HashSet<(int, int)> { (1, 2) },
        };
        var ctx = new RuleContext { Candidate = c1, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("CurriculumConflict");
    }

    [Fact]
    public void EnrollmentMode_PairHit_Reports()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200, blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c1, c2 },
            EnrollmentConflicts = new EnrollmentConflictMatrix
            {
                HardConflictPairs = new HashSet<(int, int)> { (1, 2) },
            },
        };
        var ctx = new RuleContext { Candidate = c1, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().ContainSingle().Which.ConflictType.Should().Be("CurriculumConflict");
    }

    [Fact]
    public void NoTimeOverlap_NoConflict()
    {
        var rule = new H5_CurriculumConflictRule();
        var c1 = Candidate(id: 1, courseId: 100,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var c2 = Candidate(id: 2, courseId: 200,
            semestersByDept: new Dictionary<int, int> { [10] = 3 },
            accessibleDepts: new[] { 10 },
            blocks: Block(day: 1, startHour: 13, durationHours: 2));

        var ctx = new RuleContext { Candidate = c1, Snapshot = Snapshot(c1, c2), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
