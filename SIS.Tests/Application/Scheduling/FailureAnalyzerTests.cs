using FluentAssertions;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Tests.Application.Constraints;

namespace SIS.Tests.Application.Scheduling;

public class FailureAnalyzerTests : SchedulingTestBase
{
    private const int TermId = ActiveTermId;

    private static List<TimeSlot> CreateDaySlots(DayOfWeek day, int count, int startHour = 9)
    {
        var slots = new List<TimeSlot>();
        var start = new TimeOnly(startHour, 0);
        for (var i = 0; i < count; i++)
        {
            var end = start.AddHours(1);
            slots.Add(new TimeSlot
            {
                Id = (int)day * 100 + i + 1,
                TermId = TermId,
                DayOfWeek = day,
                StartTime = start,
                EndTime = end,
                SlotOrder = i + 1,
                DisplayName = $"{day} {i + 1}"
            });
            start = end;
        }
        return slots;
    }

    private static Dictionary<DayOfWeek, List<TimeSlot>> DefaultTimeSlotsByDay() =>
        new()
        {
            [DayOfWeek.Monday] = CreateDaySlots(DayOfWeek.Monday, 8),
            [DayOfWeek.Tuesday] = CreateDaySlots(DayOfWeek.Tuesday, 8),
            [DayOfWeek.Wednesday] = CreateDaySlots(DayOfWeek.Wednesday, 8),
            [DayOfWeek.Thursday] = CreateDaySlots(DayOfWeek.Thursday, 8),
            [DayOfWeek.Friday] = CreateDaySlots(DayOfWeek.Friday, 8),
        };

    private static SchedulingConstraintConfig DefaultConfig() => new()
    {
        LunchBreakIsHard = false,
        LunchBreakStart = new TimeOnly(12, 0),
        LunchBreakEnd = new TimeOnly(13, 0),
        MaxConsecutiveHours = 12,
        BreakDurationMinutes = 5,
        AmphiThreshold = 100,
        TheoryLabSplitThreshold = 4,
        S1Enabled = false,
        S2Enabled = false,
        S3Enabled = false,
        S4Enabled = false,
        S5Enabled = false,
        S7Enabled = false,
        S8Enabled = false,
        S9Enabled = false,
        S12Enabled = false,
    };

    private static ConstraintState EmptyConstraintState() =>
        new(new Dictionary<string, ConstraintDefinition>(), new List<ConstraintException>());

    private static TermSnapshot EmptyBaseSnapshot() => new()
    {
        TermId = TermId,
        TermName = "Test Term",
        ScopeName = "Test",
        Sections = new List<SectionSnapshot>(),
        ClassroomAssignments = new List<ClassroomTermAssignment>(),
        DeptFacultyMap = new Dictionary<int, int> { [1] = 1 },
        Corequisites = new List<CoursePrerequisite>(),
        CoEnrollmentPairs = new List<CoEnrollmentPair>(),
        CourseSuggestedSemestersByDepartment = new Dictionary<int, Dictionary<int, int>>(),
        CourseAccessibleDepartments = new Dictionary<int, HashSet<int>>(),
    };

    [Fact]
    public void Produces_suggestions_for_no_classroom_of_required_type()
    {
        // Arrange: section needs PhysicsLab but no PhysicsLab classroom exists in the faculty.
        // Only a Lecture classroom is available.
        var lectureClassroom = new Classroom
        {
            Id = 701,
            Name = "A101",
            Capacity = 60,
            Type = ClassroomType.Lecture
        };

        var section = new SectionSnapshot
        {
            SectionId = 42,
            CourseId = 10,
            CourseCode = "PHYS101",
            CourseName = "Physics I",
            TermId = TermId,
            TheoryHours = 2,
            LabHours = 2,
            Capacity = 30,
            EnrolledCount = 25,
            TheoryClassroomType = ClassroomType.Lecture,
            LabClassroomType = ClassroomType.PhysicsLab,   // <-- no PhysicsLab in faculty
            DepartmentId = ComputerEngineeringDepartmentId,
            SuggestedSemestersByDepartment = new Dictionary<int, int>(),
            AccessibleDepartmentIds = new HashSet<int> { ComputerEngineeringDepartmentId },
        };

        var sectionSnapshots = new Dictionary<int, SectionSnapshot>
        {
            [section.SectionId] = section
        };

        var state = new MutableScheduleState(TermId);
        var classrooms = new List<Classroom> { lectureClassroom };
        var facultyClassroomIds = new HashSet<int> { 701 };   // only the lecture room
        var timeSlotsByDay = DefaultTimeSlotsByDay();
        var config = DefaultConfig();
        var constraintState = EmptyConstraintState();
        var baseSnapshot = EmptyBaseSnapshot();

        var analyzer = new FailureAnalyzer(new CandidateGenerator(), ConstraintService);

        // Act
        var results = analyzer.Analyze(
            new List<int> { section.SectionId },
            sectionSnapshots,
            state,
            classrooms,
            timeSlotsByDay,
            config,
            constraintState,
            baseSnapshot,
            facultyClassroomIds,
            splitThreshold: 4);

        // Assert
        results.Should().HaveCount(1);

        var result = results[0];
        result.SectionId.Should().Be(section.SectionId);
        result.CourseCode.Should().Be("PHYS101");
        result.Suggestions.Should().NotBeEmpty();

        // Should have a ClassroomAdd suggestion for the missing PhysicsLab
        result.Suggestions.Should().Contain(s =>
            s.SuggestionType == "ClassroomAdd",
            "there is no PhysicsLab classroom in the faculty, so the fix is to add one");

        // Reason should be set
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }
}
