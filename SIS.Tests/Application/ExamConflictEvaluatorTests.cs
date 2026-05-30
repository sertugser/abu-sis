using FluentAssertions;
using SIS.Application.Grading;

namespace SIS.Tests.Application;

public class ExamConflictEvaluatorTests
{
    private static ExamConflictInput Input(
        int id, int startHour, int durationMin,
        int[] rooms, (int program, int year)[] cohorts, string label = "X") =>
        new(id,
            new DateTime(2026, 1, 10, startHour, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 10, startHour, 0, 0, DateTimeKind.Utc).AddMinutes(durationMin),
            rooms,
            cohorts.Select(c => new ExamCohort(c.program, c.year)).ToList(),
            label);

    [Fact]
    public void Evaluate_NoConflict_WhenNoOverlap()
    {
        var candidate = Input(0, 9, 60, new[] { 1 }, new[] { (1, 1) });
        var existing = new[] { Input(5, 11, 60, new[] { 1 }, new[] { (1, 1) }) };
        ExamConflictEvaluator.Evaluate(candidate, existing).Kind.Should().Be(ExamConflictKind.None);
    }

    [Fact]
    public void Evaluate_RoomConflict_WhenSameRoomAndOverlap()
    {
        var candidate = Input(0, 9, 60, new[] { 1 }, new[] { (1, 1) });
        var existing = new[] { Input(5, 9, 60, new[] { 1 }, new[] { (2, 4) }) }; // farklı kohort ama aynı oda
        ExamConflictEvaluator.Evaluate(candidate, existing).Kind.Should().Be(ExamConflictKind.Classroom);
    }

    [Fact]
    public void Evaluate_CohortConflict_SameProgramSameYear()
    {
        var candidate = Input(0, 9, 60, new[] { 1 }, new[] { (1, 1) });
        var existing = new[] { Input(5, 9, 60, new[] { 2 }, new[] { (1, 1) }) }; // farklı oda, aynı program+yıl
        ExamConflictEvaluator.Evaluate(candidate, existing).Kind.Should().Be(ExamConflictKind.Cohort);
    }

    [Fact]
    public void Evaluate_CohortConflict_AdjacentYear()
    {
        var candidate = Input(0, 9, 60, new[] { 1 }, new[] { (1, 2) });
        var existing = new[] { Input(5, 9, 60, new[] { 2 }, new[] { (1, 3) }) }; // 2 ↔ 3 komşu
        ExamConflictEvaluator.Evaluate(candidate, existing).Kind.Should().Be(ExamConflictKind.Cohort);
    }

    [Fact]
    public void Evaluate_NoCohortConflict_WhenYearGapTwo()
    {
        var candidate = Input(0, 9, 60, new[] { 1 }, new[] { (1, 1) });
        var existing = new[] { Input(5, 9, 60, new[] { 2 }, new[] { (1, 3) }) }; // 1 ↔ 3 (gap 2), farklı oda
        ExamConflictEvaluator.Evaluate(candidate, existing).Kind.Should().Be(ExamConflictKind.None);
    }

    [Fact]
    public void Evaluate_NoCohortConflict_WhenDifferentProgram()
    {
        var candidate = Input(0, 9, 60, new[] { 1 }, new[] { (1, 1) });
        var existing = new[] { Input(5, 9, 60, new[] { 2 }, new[] { (2, 1) }) }; // farklı program, farklı oda
        ExamConflictEvaluator.Evaluate(candidate, existing).Kind.Should().Be(ExamConflictKind.None);
    }

    [Fact]
    public void Evaluate_ExcludesSelf_ByExamId()
    {
        var candidate = Input(7, 9, 60, new[] { 1 }, new[] { (1, 1) });
        var existing = new[] { Input(7, 9, 60, new[] { 1 }, new[] { (1, 1) }) }; // aynı id = kendisi
        ExamConflictEvaluator.Evaluate(candidate, existing).Kind.Should().Be(ExamConflictKind.None);
    }
}
