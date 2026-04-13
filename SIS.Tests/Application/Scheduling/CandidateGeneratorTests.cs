using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Tests.Application.Scheduling;

public class CandidateGeneratorTests
{
    private const int TermId = 1;

    private static readonly List<TimeSlot> MondaySlots = CreateDaySlots(DayOfWeek.Monday, 8);
    private static readonly List<TimeSlot> TuesdaySlots = CreateDaySlots(DayOfWeek.Tuesday, 8);
    private static readonly List<TimeSlot> WednesdaySlots = CreateDaySlots(DayOfWeek.Wednesday, 8);

    private static List<TimeSlot> CreateDaySlots(DayOfWeek day, int count)
    {
        var slots = new List<TimeSlot>();
        var start = new TimeOnly(9, 0);
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
            [DayOfWeek.Monday] = MondaySlots,
            [DayOfWeek.Tuesday] = TuesdaySlots,
            [DayOfWeek.Wednesday] = WednesdaySlots
        };
    }

    private static List<Classroom> DefaultClassrooms()
    {
        return new List<Classroom>
        {
            new() { Id = 1, Name = "A101", Capacity = 60, Type = ClassroomType.Lecture },
            new() { Id = 2, Name = "A102", Capacity = 80, Type = ClassroomType.Lecture },
            new() { Id = 3, Name = "LAB1", Capacity = 40, Type = ClassroomType.PhysicsLab },
            new() { Id = 4, Name = "LAB2", Capacity = 50, Type = ClassroomType.PhysicsLab },
            new() { Id = 5, Name = "LAB3", Capacity = 30, Type = ClassroomType.Lab },
        };
    }

    private static HashSet<int> AllClassroomIds() => new() { 1, 2, 3, 4, 5 };

    private static SectionSnapshot CreateSection(
        int sectionId = 1,
        int theoryHours = 3,
        int labHours = 0,
        int capacity = 40,
        ClassroomType? theoryClassroomType = null,
        ClassroomType? labClassroomType = null)
    {
        return new SectionSnapshot
        {
            SectionId = sectionId,
            CourseId = 100,
            CourseCode = "PHYS101",
            CourseName = "Physics I",
            TermId = TermId,
            TheoryHours = theoryHours,
            LabHours = labHours,
            Capacity = capacity,
            EnrolledCount = 30,
            InstructorId = 10,
            InstructorName = "Dr. Test",
            TheoryClassroomType = theoryClassroomType,
            LabClassroomType = labClassroomType
        };
    }

    [Fact]
    public void TheoryOnly_GeneratesSingleBlockCombinations()
    {
        // Arrange: 3T+0L, Lecture classroom
        var section = CreateSection(theoryHours: 3, labHours: 0, capacity: 40);
        var state = new MutableScheduleState(TermId);
        var generator = new CandidateGenerator();

        // Act
        var results = generator.Generate(
            section, state, DefaultClassrooms(), DefaultTimeSlotsByDay(),
            splitThreshold: 4, facultyClassroomIds: AllClassroomIds());

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(c =>
        {
            c.TheoryBlock.Should().NotBeNull();
            c.LabBlocks.Should().BeEmpty();
            c.TheoryBlock!.Type.Should().Be(BlockType.Theory);
        });
    }

    [Fact]
    public void MixedCourse_AboveThreshold_GeneratesDifferentDayCombinations()
    {
        // Arrange: 3T+2L (total 5 > threshold 4)
        var section = CreateSection(
            theoryHours: 3, labHours: 2, capacity: 40,
            theoryClassroomType: ClassroomType.Lecture,
            labClassroomType: ClassroomType.PhysicsLab);
        var state = new MutableScheduleState(TermId);
        var generator = new CandidateGenerator();

        // Act
        var results = generator.Generate(
            section, state, DefaultClassrooms(), DefaultTimeSlotsByDay(),
            splitThreshold: 4, facultyClassroomIds: AllClassroomIds());

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(c =>
        {
            c.TheoryBlock.Should().NotBeNull();
            c.LabBlocks.Should().NotBeEmpty();
            c.TheoryBlock!.Day.Should().NotBe(c.LabBlocks[0].Day,
                "total hours exceed split threshold, so theory and lab must be on different days");
        });
    }

    [Fact]
    public void MixedCourse_AtThreshold_AllowsSameDay()
    {
        // Arrange: 2T+2L (total 4 <= threshold 4)
        var section = CreateSection(
            theoryHours: 2, labHours: 2, capacity: 40,
            theoryClassroomType: ClassroomType.Lecture,
            labClassroomType: ClassroomType.PhysicsLab);
        var state = new MutableScheduleState(TermId);
        var generator = new CandidateGenerator();

        // Act
        var results = generator.Generate(
            section, state, DefaultClassrooms(), DefaultTimeSlotsByDay(),
            splitThreshold: 4, facultyClassroomIds: AllClassroomIds());

        // Assert
        results.Should().NotBeEmpty();
        var sameDayCombinations = results.Where(c =>
            c.TheoryBlock != null && c.LabBlocks.Count > 0 &&
            c.TheoryBlock.Day == c.LabBlocks[0].Day).ToList();
        sameDayCombinations.Should().NotBeEmpty(
            "total hours equal to threshold should allow same-day combinations");
    }

    [Fact]
    public void ExcludesOccupiedSlots()
    {
        // Arrange: occupy all Monday slots for classroom 1
        var section = CreateSection(theoryHours: 3, labHours: 0, capacity: 40);
        var state = new MutableScheduleState(TermId);

        // Place a section occupying Monday 09:00-17:00 in classroom 1
        var occupyingSnapshot = new SectionSnapshot
        {
            SectionId = 999, CourseId = 200, CourseCode = "OCC", CourseName = "Occupier",
            TermId = TermId, TheoryHours = 8, LabHours = 0, Capacity = 40,
            InstructorId = 99
        };
        state.Assign(999, occupyingSnapshot, new List<BlockAssignment>
        {
            new(BlockType.Theory, 1, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))
        });

        // Only provide Monday slots and only classroom 1
        var timeSlotsByDay = new Dictionary<DayOfWeek, List<TimeSlot>>
        {
            [DayOfWeek.Monday] = MondaySlots
        };
        var classrooms = new List<Classroom>
        {
            new() { Id = 1, Name = "A101", Capacity = 60, Type = ClassroomType.Lecture }
        };
        var generator = new CandidateGenerator();

        // Act
        var results = generator.Generate(
            section, state, classrooms, timeSlotsByDay,
            splitThreshold: 4, facultyClassroomIds: new HashSet<int> { 1 });

        // Assert: no combinations since the only classroom on the only day is fully occupied
        results.Should().BeEmpty();
    }

    [Fact]
    public void RespectsFacultyClassroomFilter()
    {
        // Arrange: two Lecture classrooms but only one in faculty set
        var section = CreateSection(theoryHours: 2, labHours: 0, capacity: 40);
        var state = new MutableScheduleState(TermId);
        var classrooms = new List<Classroom>
        {
            new() { Id = 1, Name = "A101", Capacity = 60, Type = ClassroomType.Lecture },
            new() { Id = 2, Name = "B201", Capacity = 80, Type = ClassroomType.Lecture }
        };
        // Only classroom 1 belongs to faculty
        var facultyIds = new HashSet<int> { 1 };
        var generator = new CandidateGenerator();

        // Act
        var results = generator.Generate(
            section, state, classrooms, DefaultTimeSlotsByDay(),
            splitThreshold: 4, facultyClassroomIds: facultyIds);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(c =>
        {
            c.TheoryBlock.Should().NotBeNull();
            c.TheoryBlock!.ClassroomId.Should().Be(1,
                "only classroom 1 is in the faculty set");
        });
    }
}
