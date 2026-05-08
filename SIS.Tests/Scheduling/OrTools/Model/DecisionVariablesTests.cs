using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Model;

public class DecisionVariablesTests
{
    private static SectionInput Section(int id, int theory, int lab) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "CENG101",
        CourseName = "Algorithms",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    [Fact]
    public void CreateIntervalForSection_LectureOnly_ReturnsLectureIntervalAndNoLab()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));

        registry.GetLectureInterval(1).Should().NotBeNull();
        registry.GetLabInterval(1).Should().BeNull();
    }

    [Fact]
    public void CreateIntervalForSection_LectureAndLab_ReturnsBothIntervals()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        registry.CreateIntervalsForSection(Section(2, theory: 3, lab: 2));

        registry.GetLectureInterval(2).Should().NotBeNull();
        registry.GetLabInterval(2).Should().NotBeNull();
    }

    [Fact]
    public void CreateRoomVariableForSection_RegistersIntVar()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture },
            new() { Id = 10, Name = "L139", Capacity = 40, Type = ClassroomType.Lab }
        };

        registry.CreateRoomVariable(sectionId: 1, ComponentType.Lecture, classrooms);

        registry.GetLectureRoom(1).Should().NotBeNull();
    }

    [Fact]
    public void GetLectureInterval_UnregisteredSection_ThrowsKeyNotFoundException()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        var act = () => registry.GetLectureInterval(999);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetLectureInterval_LabOnlySection_ReturnsNull()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(5, theory: 0, lab: 2));

        registry.GetLectureInterval(5).Should().BeNull();
    }

    [Fact]
    public void GetLabInterval_LectureOnlySection_ReturnsNull()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(6, theory: 3, lab: 0));

        registry.GetLabInterval(6).Should().BeNull();
    }

    [Fact]
    public void GetLabInterval_UnregisteredSection_ThrowsKeyNotFoundException()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        var act = () => registry.GetLabInterval(999);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void CreateOptionalRoomInterval_RegistersIntervalAndPresenceLiteral()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        var section = Section(1, theory: 3, lab: 0);
        registry.CreateIntervalsForSection(section);
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        registry.CreateRoomVariable(1, ComponentType.Lecture, classrooms);

        var optInterval = registry.CreateOptionalRoomInterval(1, 9, ComponentType.Lecture, duration: 3);

        optInterval.Should().NotBeNull();
        registry.AllOptionalIntervals.Should().ContainKey((1, 9, ComponentType.Lecture));
        registry.AllPresenceLiterals.Should().ContainKey((1, 9, ComponentType.Lecture));
    }

    [Fact]
    public void CreateOptionalRoomInterval_CalledTwice_ReturnsSameInterval()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        var section = Section(1, theory: 3, lab: 0);
        registry.CreateIntervalsForSection(section);
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        registry.CreateRoomVariable(1, ComponentType.Lecture, classrooms);

        var first = registry.CreateOptionalRoomInterval(1, 9, ComponentType.Lecture, duration: 3);
        var second = registry.CreateOptionalRoomInterval(1, 9, ComponentType.Lecture, duration: 3);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void CreateIntervalsForSection_RegistersPresenceLiteral()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));

        var present = registry.GetSectionPresent(1);

        present.Should().NotBeNull();
    }

    [Fact]
    public void GetSectionPresent_UnregisteredSection_ThrowsKeyNotFoundException()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        Action act = () => registry.GetSectionPresent(999);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void AllSectionPresent_AfterRegistering_ReturnsAllSections()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));
        registry.CreateIntervalsForSection(Section(2, theory: 0, lab: 2));
        registry.CreateIntervalsForSection(Section(3, theory: 3, lab: 2));

        registry.AllSectionPresent.Should().HaveCount(3);
        registry.AllSectionPresent.Should().ContainKeys(1, 2, 3);
    }

    // Plan 4 Task 2 — Parallel lab support: N IntVar lab rooms per section
    [Fact]
    public void CreateRoomVariable_LabGroupCount2_RegistersTwoRoomVars()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L1", Capacity = 40, Type = ClassroomType.Lab },
            new() { Id = 10, Name = "L2", Capacity = 40, Type = ClassroomType.Lab }
        };

        registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 2);

        var labRooms = registry.GetLabRooms(1);
        labRooms.Should().HaveCount(2);
        // AllLabRooms accessor surfaces the same list
        registry.AllLabRooms.Should().ContainKey(1);
        registry.AllLabRooms[1].Should().BeSameAs(labRooms);
        // Distinct IntVar instances per group (AddAllDifferent operates on these)
        labRooms[0].Should().NotBeSameAs(labRooms[1]);
    }

    [Fact]
    public void GetLabRoom_BackwardCompat_ReturnsFirstGroupRoom()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L", Capacity = 40, Type = ClassroomType.Lab }
        };

        registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 1);

        // Shim must return same instance as index 0 of the multi-room list
        registry.GetLabRoom(1).Should().BeSameAs(registry.GetLabRooms(1)[0]);
    }

    [Fact]
    public void CreateRoomVariable_GroupCountZero_ThrowsArgumentOutOfRange()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L", Capacity = 40, Type = ClassroomType.Lab }
        };

        Action act = () => registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Where(ex => ex.ParamName == "groupCount");
    }

    // Plan 4 Task 5 — per-group optional room intervals for parallel labs
    [Fact]
    public void CreateOptionalRoomIntervalForGroup_LabMultiGroup_RegistersDistinctIntervalsPerGroup()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L1", Capacity = 40, Type = ClassroomType.Lab },
            new() { Id = 10, Name = "L2", Capacity = 40, Type = ClassroomType.Lab }
        };
        registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 2);

        var iv0 = registry.CreateOptionalRoomIntervalForGroup(1, 9, ComponentType.Lab, duration: 2, groupIndex: 0);
        var iv1 = registry.CreateOptionalRoomIntervalForGroup(1, 9, ComponentType.Lab, duration: 2, groupIndex: 1);

        iv0.Should().NotBeSameAs(iv1);
        registry.AllOptionalIntervalsForGroups.Should()
            .ContainKey((1, 9, ComponentType.Lab, 0))
            .And.ContainKey((1, 9, ComponentType.Lab, 1));
    }

    [Fact]
    public void CreateOptionalRoomIntervalForGroup_Idempotent_SameKeyReturnsSameInterval()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L1", Capacity = 40, Type = ClassroomType.Lab }
        };
        registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 1);

        var first = registry.CreateOptionalRoomIntervalForGroup(1, 9, ComponentType.Lab, duration: 2, groupIndex: 0);
        var second = registry.CreateOptionalRoomIntervalForGroup(1, 9, ComponentType.Lab, duration: 2, groupIndex: 0);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void CreateOptionalRoomIntervalForGroup_LabGroupIndexOutOfRange_Throws()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L1", Capacity = 40, Type = ClassroomType.Lab }
        };
        registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 1);

        Action act = () => registry.CreateOptionalRoomIntervalForGroup(1, 9, ComponentType.Lab, 2, groupIndex: 5);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Where(ex => ex.ParamName == "groupIndex");
    }

    // Plan 5 Task 5 — shared day-of-component helper for H-DL/H-DO/H15/H16
    private static IReadOnlyList<TimeSlotInput> MakeSlots(int count = 20) =>
        Enumerable.Range(0, count).Select(i => new TimeSlotInput
        {
            Id = i + 1,
            DayOfWeek = (DayOfWeek)((i / 4) + 1),
            SlotOrder = (i % 4) + 1,
            StartTime = new TimeOnly(9 + (i % 4), 0),
            EndTime = new TimeOnly(9 + (i % 4), 50),
            AllowedMode = TimeSlotAllowedMode.Both
        }).ToList();

    [Fact]
    public void GetOrCreateComponentDayVar_LectureSection_ReturnsIntVar()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 20);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));

        var dayVar = registry.GetOrCreateComponentDayVar(1, ComponentType.Lecture, MakeSlots());

        dayVar.Should().NotBeNull();
        registry.AllComponentDayVars.Should().ContainKey((1, ComponentType.Lecture));
    }

    [Fact]
    public void GetOrCreateComponentDayVar_CalledTwice_ReturnsSameVar()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 20);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));
        var slots = MakeSlots();

        var first = registry.GetOrCreateComponentDayVar(1, ComponentType.Lecture, slots);
        var second = registry.GetOrCreateComponentDayVar(1, ComponentType.Lecture, slots);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreateComponentDayVar_LabSection_ReturnsDistinctVarUnderLabKey()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 20);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 2));
        var slots = MakeSlots();

        var lectureVar = registry.GetOrCreateComponentDayVar(1, ComponentType.Lecture, slots);
        var labVar = registry.GetOrCreateComponentDayVar(1, ComponentType.Lab, slots);

        lectureVar.Should().NotBeSameAs(labVar);
        registry.AllComponentDayVars.Should().ContainKey((1, ComponentType.Lecture));
        registry.AllComponentDayVars.Should().ContainKey((1, ComponentType.Lab));
    }

    [Fact]
    public void GetOrCreateComponentDayVar_TwoSections_ProducesDistinctVars()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 20);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));
        registry.CreateIntervalsForSection(Section(2, theory: 3, lab: 0));
        var slots = MakeSlots();

        var var1 = registry.GetOrCreateComponentDayVar(1, ComponentType.Lecture, slots);
        var var2 = registry.GetOrCreateComponentDayVar(2, ComponentType.Lecture, slots);

        var1.Should().NotBeSameAs(var2);
        registry.AllComponentDayVars.Should().HaveCount(2);
    }

    [Fact]
    public void GetOrCreateComponentDayVar_EmptyTimeSlots_Throws()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 20);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));

        var act = () => registry.GetOrCreateComponentDayVar(1, ComponentType.Lecture, []);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TimeSlots is empty*");
    }

    // Plan 7 Task 2 — slot validation via optional timeSlots parameter
    [Fact]
    public void CreateIntervalsForSection_WithSlotValidation_RejectsCrossDayStart()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 3);
        // 3-slot grid: Mon 9:00, Mon 10:00, Tue 9:00. Theory=2 slot duration.
        // Valid starts: slot 0 only (Mon-Mon). Slot 1 invalid (Mon→Tue cross day).
        var slots = new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 3, DayOfWeek = DayOfWeek.Tuesday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both }
        };

        registry.CreateIntervalsForSection(Section(1, theory: 2, lab: 0), slots);

        // Force section presence and solve to verify start var domain restriction.
        var present = (BoolVar)registry.GetSectionPresent(1);
        model.Add(present == 1);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        var theoryInterval = registry.GetLectureInterval(1)!;
        var startValue = solver.Value(theoryInterval.StartExpr());
        startValue.Should().Be(0,
            because: "only slot 0 is a valid 2-slot start (slot 1 would cross day boundary)");
    }

    [Fact]
    public void CreateIntervalsForSection_WithSlotValidation_NoValidBlock_PresenceForcedToZero()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 2);
        // 2-slot grid: Mon, Tue (cross day). Theory=2 → no valid block.
        var slots = new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Tuesday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both }
        };

        registry.CreateIntervalsForSection(Section(1, theory: 2, lab: 0), slots);

        // Without forcing presence, solver should still find Feasible (presence=0 = section dropped).
        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        var present = (BoolVar)registry.GetSectionPresent(1);
        solver.Value(present).Should().Be(0,
            because: "no valid block → section forced to drop (presence=0)");
    }

    [Fact]
    public void CreateIntervalsForSection_WithoutSlotValidation_LegacyBehavior()
    {
        // Plan 4-6 backward compat: timeSlots=null → unrestricted start IntVar (0..totalSlots-duration).
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 3);

        // No timeSlots parameter — null branch.
        registry.CreateIntervalsForSection(Section(1, theory: 2, lab: 0));

        var theoryInterval = registry.GetLectureInterval(1)!;
        theoryInterval.Should().NotBeNull();
        // Legacy: start ∈ [0, 1] without slot validation. Cross-day not enforced here.
    }

    // Plan 7b Task 7 — Multi-interval lab support
    [Fact]
    public void CreateLabIntervalsForSection_LabHours4_MaxBlock2_CreatesTwoIntervalsDifferentDays()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 8);
        // 8-slot grid: Mon×4 + Tue×4. LabHours=4, max=2 → 2 interval × 2 saat.
        var slots = new List<TimeSlotInput>();
        for (int d = 0; d < 2; d++)
            for (int s = 0; s < 4; s++)
                slots.Add(new TimeSlotInput
                {
                    Id = d * 4 + s + 1,
                    DayOfWeek = (DayOfWeek)(d + 1),
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(9 + s, 0),
                    EndTime = new TimeOnly(9 + s, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });

        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "X",
            CourseName = "X",
            SectionNumber = 1,
            Capacity = 30,
            TheoryHours = 0,
            LabHours = 4,
            TeachingMethod = TeachingMethod.FaceToFace
        };

        // First call: skip single-interval lab; multi-interval method will create blocks.
        registry.CreateIntervalsForSection(section, slots, createLabInterval: false);
        registry.CreateLabIntervalsForSection(section, slots, maxLabBlockHours: 2);

        var labIntervals = registry.GetLabIntervals(1);
        labIntervals.Should().HaveCount(2,
            because: "LabHours=4 / max=2 → 2 intervals");

        // Force presence + solve → assert different days
        var present = (BoolVar)registry.GetSectionPresent(1);
        model.Add(present == 1);

        var solver = new CpSolver();
        var status = solver.Solve(model);
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });

        var startA = (int)solver.Value(labIntervals[0].StartExpr());
        var startB = (int)solver.Value(labIntervals[1].StartExpr());
        var dayA = slots[startA].DayOfWeek;
        var dayB = slots[startB].DayOfWeek;
        dayA.Should().NotBe(dayB,
            because: "AddAllDifferent over day vars → lab blocks span different days");
    }

    [Fact]
    public void CreateLabIntervalsForSection_LabHoursEqualMaxBlock_SingleInterval()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 4);
        var slots = new List<TimeSlotInput>();
        for (int s = 0; s < 4; s++)
            slots.Add(new TimeSlotInput
            {
                Id = s + 1,
                DayOfWeek = DayOfWeek.Monday,
                SlotOrder = s + 1,
                StartTime = new TimeOnly(9 + s, 0),
                EndTime = new TimeOnly(9 + s, 50),
                AllowedMode = TimeSlotAllowedMode.Both
            });

        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "X",
            CourseName = "X",
            SectionNumber = 1,
            Capacity = 30,
            TheoryHours = 0,
            LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace
        };

        registry.CreateIntervalsForSection(section, slots, createLabInterval: false);
        registry.CreateLabIntervalsForSection(section, slots, maxLabBlockHours: 2);

        registry.GetLabIntervals(1).Should().HaveCount(1,
            because: "LabHours=2 ≤ max=2 → single interval");
    }

    // Plan 7b Task 4 — Online section: room IntVar is NOT created.
    // Defensive regression guard (NOT a TDD red→green test): even before production changes
    // ScheduleModelBuilder skipped CreateRoomVariable for Online, GetLectureRoom would already
    // throw KeyNotFoundException. This test pins that contract so no future change accidentally
    // creates room vars for Online sections, breaking the ClassroomConflict / ClassroomType
    // skip logic that relies on the dictionary absence (or the try/catch in ClassroomTypeEncoder).
    [Fact]
    public void CreateRoomVariable_OnlineSectionNotInvoked_LookupReturnsKeyNotFound()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        var onlineSection = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "CENG101",
            CourseName = "Algorithms",
            SectionNumber = 1,
            Capacity = 40,
            TheoryHours = 3,
            LabHours = 0,
            TeachingMethod = TeachingMethod.Online
        };

        // Online section: only intervals are created, NOT the room var (mirrors ScheduleModelBuilder).
        registry.CreateIntervalsForSection(onlineSection);

        var act = () => registry.GetLectureRoom(1);

        act.Should().Throw<KeyNotFoundException>();
    }
}
