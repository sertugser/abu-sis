using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H10_FacultyClassroomRuleTests
{
    [Fact]
    public void ClassroomNotAssignedToFaculty_Reports()
    {
        var rule = new H10_FacultyClassroomRule();
        var c = Candidate(id: 1, courseId: 100, departmentId: 10,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c },
            DeptFacultyMap = new Dictionary<int, int> { [10] = 99 }, // dept 10 → faculty 99
            ClassroomAssignments = new[] { new ClassroomTermAssignmentInfo(ClassroomId: 5, FacultyId: 88) }, // farklı faculty
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().Contain(v => v.ConflictType == "FacultyClassroomConflict");
    }

    [Fact]
    public void ClassroomMatchesFaculty_NoConflict()
    {
        var rule = new H10_FacultyClassroomRule();
        var c = Candidate(id: 1, courseId: 100, departmentId: 10,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c },
            DeptFacultyMap = new Dictionary<int, int> { [10] = 99 },
            ClassroomAssignments = new[] { new ClassroomTermAssignmentInfo(5, 99) },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoClassroomAssignments_SkipsRule()
    {
        var rule = new H10_FacultyClassroomRule();
        var c = Candidate(id: 1, courseId: 100, departmentId: 10,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 5));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c), Config = DefaultConfig() };
        rule.Evaluate(ctx).Should().BeEmpty();
    }
}
