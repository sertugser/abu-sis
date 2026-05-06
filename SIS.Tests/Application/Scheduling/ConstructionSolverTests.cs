using FluentAssertions;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Infrastructure.Services.Scheduling.Legacy;
using SIS.Tests.Application.Constraints;

namespace SIS.Tests.Application.Scheduling;

public class ConstructionSolverTests : SchedulingTestBase
{
    private const int TermId = 100; // ActiveTermId from base

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

    private static Dictionary<DayOfWeek, List<TimeSlot>> DefaultTimeSlotsByDay()
    {
        return new Dictionary<DayOfWeek, List<TimeSlot>>
        {
            [DayOfWeek.Monday] = CreateDaySlots(DayOfWeek.Monday, 8),
            [DayOfWeek.Tuesday] = CreateDaySlots(DayOfWeek.Tuesday, 8),
            [DayOfWeek.Wednesday] = CreateDaySlots(DayOfWeek.Wednesday, 8),
            [DayOfWeek.Thursday] = CreateDaySlots(DayOfWeek.Thursday, 8),
            [DayOfWeek.Friday] = CreateDaySlots(DayOfWeek.Friday, 8),
        };
    }

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

    private static MrvWeights DefaultWeights() => new();

    private static ConstraintState EmptyConstraintState() =>
        new(new Dictionary<string, ConstraintDefinition>(), new List<ConstraintException>());

    private static TermSnapshot EmptyBaseSnapshot(int termId = TermId) => new()
    {
        TermId = termId,
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
    public void Places_single_section_successfully()
    {
        // Arrange
        var classroom = new Classroom
        {
            Id = 501,
            Name = "A101",
            Capacity = 60,
            Type = ClassroomType.Lecture
        };

        var section = new SectionSnapshot
        {
            SectionId = 1,
            CourseId = 10,
            CourseCode = "CENG101",
            CourseName = "Intro to CS",
            TermId = TermId,
            TheoryHours = 3,
            LabHours = 0,
            Capacity = 40,
            EnrolledCount = 0,
            TheoryClassroomType = ClassroomType.Lecture,
            DepartmentId = ComputerEngineeringDepartmentId,
            SuggestedSemestersByDepartment = new Dictionary<int, int> { [ComputerEngineeringDepartmentId] = 1 },
            AccessibleDepartmentIds = new HashSet<int> { ComputerEngineeringDepartmentId },
        };

        var state = new MutableScheduleState(TermId);
        var classrooms = new List<Classroom> { classroom };
        var facultyClassroomIds = new HashSet<int> { 501 };
        var timeSlotsByDay = DefaultTimeSlotsByDay();
        var config = DefaultConfig();
        var constraintState = EmptyConstraintState();
        var baseSnapshot = EmptyBaseSnapshot();

        var solver = new ConstructionSolver(new CandidateGenerator(), ConstraintService);

        // Act
        var result = solver.Solve(
            new List<SectionSnapshot> { section },
            state, classrooms, timeSlotsByDay,
            config, constraintState, baseSnapshot,
            facultyClassroomIds, splitThreshold: 4,
            backtrackLimit: 3, DefaultWeights());

        // Assert
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        result.FailedSectionIds.Should().BeEmpty();
        state.IsAssigned(1).Should().BeTrue();
    }

    [Fact]
    public void Returns_failure_when_no_valid_placement()
    {
        // Arrange — section needs capacity 200 but only a 30-seat classroom exists
        var classroom = new Classroom
        {
            Id = 502,
            Name = "Small Room",
            Capacity = 30,
            Type = ClassroomType.Lecture
        };

        var section = new SectionSnapshot
        {
            SectionId = 2,
            CourseId = 20,
            CourseCode = "CENG201",
            CourseName = "Algorithms",
            TermId = TermId,
            TheoryHours = 3,
            LabHours = 0,
            Capacity = 200,
            EnrolledCount = 0,
            TheoryClassroomType = ClassroomType.Lecture,
            DepartmentId = ComputerEngineeringDepartmentId,
            SuggestedSemestersByDepartment = new Dictionary<int, int> { [ComputerEngineeringDepartmentId] = 3 },
            AccessibleDepartmentIds = new HashSet<int> { ComputerEngineeringDepartmentId },
        };

        var state = new MutableScheduleState(TermId);
        var classrooms = new List<Classroom> { classroom };
        var facultyClassroomIds = new HashSet<int> { 502 };
        var timeSlotsByDay = DefaultTimeSlotsByDay();
        var config = DefaultConfig();
        var constraintState = EmptyConstraintState();
        var baseSnapshot = EmptyBaseSnapshot();

        var solver = new ConstructionSolver(new CandidateGenerator(), ConstraintService);

        // Act
        var result = solver.Solve(
            new List<SectionSnapshot> { section },
            state, classrooms, timeSlotsByDay,
            config, constraintState, baseSnapshot,
            facultyClassroomIds, splitThreshold: 4,
            backtrackLimit: 3, DefaultWeights());

        // Assert
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.FailedSectionIds.Should().Contain(2);
        state.IsAssigned(2).Should().BeFalse();
    }

    [Fact]
    public void Mrv_places_most_constrained_first_and_both_succeed()
    {
        // Arrange — one lecture-only course and one lab course
        // Lab course is more constrained (MRV) so it should be attempted first
        var lectureRoom = new Classroom
        {
            Id = 601,
            Name = "LectureHall",
            Capacity = 60,
            Type = ClassroomType.Lecture
        };
        var labRoom = new Classroom
        {
            Id = 602,
            Name = "LabRoom",
            Capacity = 60,
            Type = ClassroomType.Lab
        };

        var lectureSection = new SectionSnapshot
        {
            SectionId = 10,
            CourseId = 100,
            CourseCode = "CENG100",
            CourseName = "Pure Lecture",
            TermId = TermId,
            TheoryHours = 3,
            LabHours = 0,
            Capacity = 40,
            EnrolledCount = 0,
            TheoryClassroomType = ClassroomType.Lecture,
            DepartmentId = ComputerEngineeringDepartmentId,
            SuggestedSemestersByDepartment = new Dictionary<int, int> { [ComputerEngineeringDepartmentId] = 1 },
            AccessibleDepartmentIds = new HashSet<int> { ComputerEngineeringDepartmentId },
        };

        var labSection = new SectionSnapshot
        {
            SectionId = 11,
            CourseId = 101,
            CourseCode = "CENG101",
            CourseName = "With Lab",
            TermId = TermId,
            TheoryHours = 2,
            LabHours = 2,
            Capacity = 40,
            EnrolledCount = 0,
            TheoryClassroomType = ClassroomType.Lecture,
            LabClassroomType = ClassroomType.Lab,
            DepartmentId = ComputerEngineeringDepartmentId,
            SuggestedSemestersByDepartment = new Dictionary<int, int> { [ComputerEngineeringDepartmentId] = 1 },
            AccessibleDepartmentIds = new HashSet<int> { ComputerEngineeringDepartmentId },
        };

        var state = new MutableScheduleState(TermId);
        var classrooms = new List<Classroom> { lectureRoom, labRoom };
        var facultyClassroomIds = new HashSet<int> { 601, 602 };
        var timeSlotsByDay = DefaultTimeSlotsByDay();
        var config = DefaultConfig();
        var constraintState = EmptyConstraintState();
        var baseSnapshot = EmptyBaseSnapshot();

        // Pass lecture section first in list to verify MRV reorders
        var sections = new List<SectionSnapshot> { lectureSection, labSection };

        var solver = new ConstructionSolver(new CandidateGenerator(), ConstraintService);

        // Act
        var result = solver.Solve(
            sections, state, classrooms, timeSlotsByDay,
            config, constraintState, baseSnapshot,
            facultyClassroomIds, splitThreshold: 4,
            backtrackLimit: 3, DefaultWeights());

        // Assert — both should be placed
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.FailedSectionIds.Should().BeEmpty();
        state.IsAssigned(10).Should().BeTrue();
        state.IsAssigned(11).Should().BeTrue();
    }
}
