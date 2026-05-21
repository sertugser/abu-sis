using FluentAssertions;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools;

namespace SIS.Tests.Scheduling.OrTools;

public class PreflightFeasibilityCheckerTests
{
    private static SectionInput Section(int id, int theory, int lab, int capacity = 40) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = capacity,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static SectionInput OnlineSection(int id, int theory, int lab, int capacity = 40) =>
        Section(id, theory, lab, capacity) with { TeachingMethod = TeachingMethod.Online };

    private static IReadOnlyList<TimeSlotInput> BuildMinimalSlots() => new[]
    {
        new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
            AllowedMode = TimeSlotAllowedMode.Both },
        new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
            StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
            AllowedMode = TimeSlotAllowedMode.Both },
        new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
            StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(11, 50),
            AllowedMode = TimeSlotAllowedMode.Both }
    };

    [Fact]
    public void Check_LabSectionWithNoLabRooms_MarkedInfeasible()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 0, lab: 2, capacity: 40) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Lecture only", Capacity = 50, Type = ClassroomType.Lecture }
        };

        var result = checker.Check(sections, classrooms, BuildMinimalSlots());

        result.FeasibleSections.Should().BeEmpty();
        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Section.Id.Should().Be(1);
        result.InfeasibleSections[0].Reason.Should().Contain("Lab");
    }

    [Fact]
    public void Check_LectureSectionWithCapacityExceedingAllRooms_MarkedInfeasible()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 3, lab: 0, capacity: 200) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Small", Capacity = 50, Type = ClassroomType.Lecture }
        };

        var result = checker.Check(sections, classrooms, BuildMinimalSlots());

        result.FeasibleSections.Should().BeEmpty();
        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Reason.Should().Contain("kapasit");
    }

    [Fact]
    public void Check_ValidSection_MarkedFeasible()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 3, lab: 2, capacity: 40) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Lab", Capacity = 50, Type = ClassroomType.Lab },
            new ClassroomInput { Id = 2, Name = "Lecture", Capacity = 50, Type = ClassroomType.Lecture }
        };

        var result = checker.Check(sections, classrooms, BuildMinimalSlots());

        result.FeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections.Should().BeEmpty();
    }

    [Fact]
    public void Check_ComputerLabAcceptedForLabSection()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 0, lab: 2, capacity: 40) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "PC Lab", Capacity = 50, Type = ClassroomType.Computer }
        };

        var result = checker.Check(sections, classrooms, BuildMinimalSlots());

        // Computer type counts as a valid lab classroom
        result.FeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections.Should().BeEmpty();
    }

    [Fact]
    public void Check_OnlineSectionWithoutClassrooms_MarkedFeasible()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { OnlineSection(1, theory: 2, lab: 0, capacity: 40) };

        var result = checker.Check(sections, Array.Empty<ClassroomInput>(), BuildMinimalSlots());

        result.FeasibleSections.Should().ContainSingle().Which.Id.Should().Be(1);
        result.InfeasibleSections.Should().BeEmpty();
    }

    [Fact]
    public void Check_MixedSectionLabRoomTooSmall_MarkedInfeasible()
    {
        // Section needs both theory + lab. Lab room exists but capacity insufficient.
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 3, lab: 2, capacity: 40) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Lecture", Capacity = 60, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "Tiny Lab", Capacity = 20, Type = ClassroomType.Lab }
        };

        var result = checker.Check(sections, classrooms, BuildMinimalSlots());

        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Reason.Should().Contain("Lab");
    }

    [Fact]
    public void Check_LabSection80Capacity_TwoLab40Rooms_MarkedFeasible_ParallelSplit()
    {
        // Plan 4 Task 6: paralel split — 80-cap section bölünerek 2x40 lab'a sığar.
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 0, lab: 2, capacity: 80) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 40, Type = ClassroomType.Lab },
            new ClassroomInput { Id = 2, Name = "L2", Capacity = 40, Type = ClassroomType.Lab }
        };

        var result = checker.Check(sections, classrooms, BuildMinimalSlots());

        result.FeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections.Should().BeEmpty();
    }

    [Fact]
    public void Check_LabSection80Capacity_OneLab40Room_MarkedInfeasible_NotEnoughRooms()
    {
        // Plan 4 Task 6: 80-cap section, tek 40-cap lab → 2 paralel grup gerekli ama 1 lab var → infeasible.
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 0, lab: 2, capacity: 80) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 40, Type = ClassroomType.Lab }
        };

        var result = checker.Check(sections, classrooms, BuildMinimalSlots());

        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Reason.Should().Contain("paralel grup");
    }

    [Fact]
    public void Check_MultipleSectionsMixed_PartialFeasibility()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[]
        {
            Section(1, theory: 3, lab: 2, capacity: 40),  // feasible
            Section(2, theory: 3, lab: 0, capacity: 200),  // capacity too large
            Section(3, theory: 0, lab: 2, capacity: 40)    // feasible
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Lab", Capacity = 50, Type = ClassroomType.Lab },
            new ClassroomInput { Id = 2, Name = "Lecture", Capacity = 70, Type = ClassroomType.Lecture }
        };

        var result = checker.Check(sections, classrooms, BuildMinimalSlots());

        result.FeasibleSections.Should().HaveCount(2);
        result.FeasibleSections.Select(s => s.Id).Should().BeEquivalentTo(new[] { 1, 3 });
        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Section.Id.Should().Be(2);
    }

    [Fact]
    public void Check_TheoryDurationExceedsDailySlotCount_MarkedInfeasible()
    {
        // 3-slot/gün grid (Mon×3 + Tue×3). 4-saatlik theory → 4 ardışık slot aynı günde lazım,
        // ama günde sadece 3 slot var → no valid block → infeasible.
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 4, lab: 0, capacity: 30) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "C", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var slots = new[]
        {
            new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
                StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(11, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 4, DayOfWeek = DayOfWeek.Tuesday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both }
        };

        var result = checker.Check(sections, classrooms, slots);

        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Reason.Should().Contain("4 saatlik");
    }

    [Fact]
    public void Check_LabDurationFitsInDay_MarkedFeasible()
    {
        // 4-slot/gün grid → 4-saatlik lab fits.
        // Plan 7b Task 8: explicit maxLabBlockHours=4 disables time-split so the lab stays one block.
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 0, lab: 4, capacity: 30) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L", Capacity = 50, Type = ClassroomType.Lab }
        };
        var slots = new[]
        {
            new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
                StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(11, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4,
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(12, 50),
                AllowedMode = TimeSlotAllowedMode.Both }
        };

        var result = checker.Check(sections, classrooms, slots, maxLabBlockHours: 4);

        result.FeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections.Should().BeEmpty();
    }

    [Fact]
    public void Check_LabSplitRequiresMoreDaysThanAvailable_MarkedInfeasible()
    {
        // Plan 7b Task 8 — 4h lab + maxLabBlockHours=2 → split into [2,2]; AddAllDifferent
        // requires 2 distinct days. Single-day grid has only 1 day → preflight infeasible.
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 0, lab: 4, capacity: 30) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L", Capacity = 50, Type = ClassroomType.Lab }
        };
        var slots = new[]
        {
            new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
                StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(11, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4,
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(12, 50),
                AllowedMode = TimeSlotAllowedMode.Both }
        };

        var result = checker.Check(sections, classrooms, slots, maxLabBlockHours: 2);

        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Reason.Should().Contain("farklı gün",
            because: "split into 2 blocks but only 1 distinct day available");
    }
}
