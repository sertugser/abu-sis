using FluentAssertions;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Tests.Application.Constraints;

namespace SIS.Tests.Application.Scheduling;

public class SimulatedAnnealingTests : SchedulingTestBase
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

    private static Dictionary<DayOfWeek, List<TimeSlot>> CreateWeekSlots(int slotsPerDay = 8)
    {
        var dict = new Dictionary<DayOfWeek, List<TimeSlot>>();
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            dict[day] = CreateDaySlots(day, slotsPerDay);
        }
        return dict;
    }

    private static SchedulingConstraintConfig DefaultConfig => new()
    {
        LunchBreakIsHard = false,
        LunchBreakStart = new TimeOnly(12, 0),
        LunchBreakEnd = new TimeOnly(13, 0),
        MaxConsecutiveHours = 12,
        BreakDurationMinutes = 5,
        AmphiThreshold = 100,
        TheoryLabSplitThreshold = 4,
        S1Enabled = false,
        S1MinOccupancy = 60,
        S1MaxOccupancy = 85,
        S2Enabled = false,
        S2MaxDailyHours = 6,
        S3Enabled = false,
        S3MaxSectionsPerSlot = 3,
        S4Enabled = false,
        S4MinCoPairThreshold = 2,
        S5Enabled = false,
        S7Enabled = false,
        S7MaxGapSlots = 2,
        S8Enabled = false,
        S9Enabled = false,
        S9MorningStart = new TimeOnly(8, 0),
        S9EveningStart = new TimeOnly(17, 0),
        S12Enabled = false,
        S12MaxImbalancePercent = 20,
    };

    private (MutableScheduleState State, List<Classroom> Classrooms, Dictionary<DayOfWeek, List<TimeSlot>> TimeSlots, TermSnapshot BaseSnapshot, HashSet<int> FacultyClassroomIds) SetupScenario(int sectionCount = 4)
    {
        var state = new MutableScheduleState(TermId);
        var timeSlotsByDay = CreateWeekSlots();

        // Create classrooms
        var classrooms = new List<Classroom>();
        for (var i = 1; i <= 3; i++)
        {
            var cr = AddClassroom(i, $"Room{i}", 50);
            classrooms.Add(cr);
        }

        var facultyClassroomIds = new HashSet<int> { 1, 2, 3 };

        // Create courses and sections
        for (var i = 1; i <= sectionCount; i++)
        {
            var course = AddCourse(i, $"CS10{i}", theoryHours: 2);
            var instructor = AddInstructor(i);

            var snapshot = new SectionSnapshot
            {
                SectionId = i,
                CourseId = course.Id,
                CourseCode = course.Code,
                CourseName = course.Name,
                TermId = TermId,
                TheoryHours = 2,
                LabHours = 0,
                Capacity = 30,
                EnrolledCount = 0,
                ClassroomId = 1,
                ClassroomCapacity = 50,
                ClassroomType = ClassroomType.Lecture,
                TheoryClassroomType = ClassroomType.Lecture,
                InstructorId = instructor.Id,
                InstructorName = $"Inst{i}",
                InstructorMaxWeeklyHours = 20,
                DepartmentId = ComputerEngineeringDepartmentId,
                SuggestedSemester = 1,
                AssignedTimeSlotCount = 2,
                TimeBlocks = new List<TimeBlock>
                {
                    new() { Day = DayOfWeek.Monday, StartTime = new TimeOnly(9 + (i - 1) * 2, 0), EndTime = new TimeOnly(9 + (i - 1) * 2 + 2, 0), ClassroomId = 1 }
                }
            };

            var blocks = new List<BlockAssignment>
            {
                new(BlockType.Theory, 1, DayOfWeek.Monday, new TimeOnly(9 + (i - 1) * 2, 0), new TimeOnly(9 + (i - 1) * 2 + 2, 0))
            };

            state.Assign(i, snapshot, blocks);
        }

        var baseSnapshot = state.ToTermSnapshot(new TermSnapshot
        {
            TermId = TermId,
            TermName = "Test Term",
            ScopeName = "Test",
        });

        return (state, classrooms, timeSlotsByDay, baseSnapshot, facultyClassroomIds);
    }

    [Fact]
    public void Deterministic_with_same_seed()
    {
        // Arrange
        var (state1, classrooms, timeSlots, baseSnapshot, facultyIds) = SetupScenario();
        var constraintState = new ConstraintState(new(), new());
        var config = DefaultConfig;
        var candidateGen = new CandidateGenerator();

        // Clone state for second run
        var state2 = state1.Clone();

        var optimizer1 = new SimulatedAnnealingOptimizer(candidateGen, ConstraintService);
        var optimizer2 = new SimulatedAnnealingOptimizer(candidateGen, ConstraintService);

        // Act
        var result1 = optimizer1.Optimize(
            state1, classrooms, timeSlots, config, constraintState, baseSnapshot, facultyIds,
            splitThreshold: 4, seed: 42, maxIterations: 200, timeoutSeconds: 30,
            stagnationLimit: 100, initialTemperature: 100.0,
            relocateWeight: 5, swapWeight: 3, chainWeight: 2);

        var result2 = optimizer2.Optimize(
            state2, classrooms, timeSlots, config, constraintState, baseSnapshot, facultyIds,
            splitThreshold: 4, seed: 42, maxIterations: 200, timeoutSeconds: 30,
            stagnationLimit: 100, initialTemperature: 100.0,
            relocateWeight: 5, swapWeight: 3, chainWeight: 2);

        // Assert
        result1.FinalPenalty.Should().Be(result2.FinalPenalty);
        result1.Iterations.Should().Be(result2.Iterations);
    }

    [Fact]
    public void Does_not_move_pinned_sections()
    {
        // Arrange
        var state = new MutableScheduleState(TermId);
        var timeSlotsByDay = CreateWeekSlots();

        var classrooms = new List<Classroom>();
        for (var i = 1; i <= 3; i++)
            classrooms.Add(AddClassroom(10 + i, $"PinRoom{i}", 50));

        var facultyClassroomIds = new HashSet<int> { 11, 12, 13 };

        // Pinned section
        var pinnedCourse = AddCourse(50, "PINNED1", theoryHours: 2);
        var pinnedInstructor = AddInstructor(50);
        var pinnedBlocks = new List<BlockAssignment>
        {
            new(BlockType.Theory, 11, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0))
        };
        var pinnedSnapshot = new SectionSnapshot
        {
            SectionId = 50,
            CourseId = pinnedCourse.Id,
            CourseCode = pinnedCourse.Code,
            CourseName = pinnedCourse.Name,
            TermId = TermId,
            TheoryHours = 2,
            LabHours = 0,
            Capacity = 30,
            ClassroomId = 11,
            ClassroomCapacity = 50,
            ClassroomType = ClassroomType.Lecture,
            TheoryClassroomType = ClassroomType.Lecture,
            InstructorId = pinnedInstructor.Id,
            InstructorName = "PinnedInst",
            InstructorMaxWeeklyHours = 20,
            DepartmentId = ComputerEngineeringDepartmentId,
            AssignedTimeSlotCount = 2,
            TimeBlocks = new List<TimeBlock>
            {
                new() { Day = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0), ClassroomId = 11 }
            }
        };
        state.Assign(50, pinnedSnapshot, pinnedBlocks, isPinned: true);

        // Non-pinned sections
        for (var i = 1; i <= 3; i++)
        {
            var course = AddCourse(50 + i, $"MOVE{i}", theoryHours: 2);
            var instructor = AddInstructor(50 + i);
            var blocks = new List<BlockAssignment>
            {
                new(BlockType.Theory, 11 + (i % 3), (DayOfWeek)(1 + i), new TimeOnly(9, 0), new TimeOnly(11, 0))
            };
            var snap = new SectionSnapshot
            {
                SectionId = 50 + i,
                CourseId = course.Id,
                CourseCode = course.Code,
                CourseName = course.Name,
                TermId = TermId,
                TheoryHours = 2,
                LabHours = 0,
                Capacity = 30,
                ClassroomId = 11 + (i % 3),
                ClassroomCapacity = 50,
                ClassroomType = ClassroomType.Lecture,
                TheoryClassroomType = ClassroomType.Lecture,
                InstructorId = instructor.Id,
                InstructorName = $"Inst{50 + i}",
                InstructorMaxWeeklyHours = 20,
                DepartmentId = ComputerEngineeringDepartmentId,
                AssignedTimeSlotCount = 2,
                TimeBlocks = new List<TimeBlock>
                {
                    new() { Day = (DayOfWeek)(1 + i), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0), ClassroomId = 11 + (i % 3) }
                }
            };
            state.Assign(50 + i, snap, blocks);
        }

        var baseSnapshot = state.ToTermSnapshot(new TermSnapshot
        {
            TermId = TermId,
            TermName = "Test Term",
            ScopeName = "Test",
        });

        var constraintState = new ConstraintState(new(), new());
        var config = DefaultConfig;
        var candidateGen = new CandidateGenerator();
        var optimizer = new SimulatedAnnealingOptimizer(candidateGen, ConstraintService);

        // Act
        optimizer.Optimize(
            state, classrooms, timeSlotsByDay, config, constraintState, baseSnapshot, facultyClassroomIds,
            splitThreshold: 4, seed: 42, maxIterations: 200, timeoutSeconds: 30,
            stagnationLimit: 100, initialTemperature: 100.0,
            relocateWeight: 5, swapWeight: 3, chainWeight: 2);

        // Assert — pinned section must remain at original position
        var pinnedPlacement = state.GetPlacement(50);
        pinnedPlacement.Should().NotBeNull();
        pinnedPlacement!.IsPinned.Should().BeTrue();
        pinnedPlacement.Blocks.Should().HaveCount(1);
        pinnedPlacement.Blocks[0].ClassroomId.Should().Be(11);
        pinnedPlacement.Blocks[0].Day.Should().Be(DayOfWeek.Monday);
        pinnedPlacement.Blocks[0].StartTime.Should().Be(new TimeOnly(9, 0));
        pinnedPlacement.Blocks[0].EndTime.Should().Be(new TimeOnly(11, 0));
    }

    [Fact]
    public void Respects_timeout()
    {
        // Arrange
        var (state, classrooms, timeSlots, baseSnapshot, facultyIds) = SetupScenario();
        var constraintState = new ConstraintState(new(), new());
        var config = DefaultConfig;
        var candidateGen = new CandidateGenerator();
        var optimizer = new SimulatedAnnealingOptimizer(candidateGen, ConstraintService);

        // Act — set timeout=2 with huge iteration limit
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = optimizer.Optimize(
            state, classrooms, timeSlots, config, constraintState, baseSnapshot, facultyIds,
            splitThreshold: 4, seed: 42, maxIterations: 1_000_000, timeoutSeconds: 2,
            stagnationLimit: 1_000_000, initialTemperature: 100.0,
            relocateWeight: 5, swapWeight: 3, chainWeight: 2);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(5.0);
        result.Iterations.Should().BeLessThan(1_000_000);
    }
}
