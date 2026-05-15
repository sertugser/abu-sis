using FluentAssertions;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Tests.Application.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SIS.Tests.Scheduling.Pinning;

/// <summary>
/// Plan 12 Faz 0.1 — V1 ConstraintEvaluationService'in soft penalty çıktısını
/// dondurur. Plan 12 boyunca her port adımı sonrası bu test geçmeli; sayısal
/// kayma anında yakalanır.
///
/// Her senaryo bir soft kuralı tetikler; expected değer mevcut V1 davranışından alınmıştır.
/// Yeni değer eklerken: 0.0 ile başla → test fail → output'taki actual değeri buraya yapıştır.
/// </summary>
public sealed class SoftPenaltyBaselineTests : SchedulingTestBase
{
    private readonly ITestOutputHelper _output;

    public SoftPenaltyBaselineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private SchedulingConstraintConfig BuildBaselineConfig() => new()
    {
        S1Enabled = true,
        S1MinOccupancy = 60,
        S1MaxOccupancy = 85,
        S2Enabled = true,
        S2MaxDailyHours = 6,
        S3Enabled = true,
        S3MaxSectionsPerSlot = 5,
        S4Enabled = true,
        S4MinCoPairThreshold = 2,
        S5Enabled = true,
        S7Enabled = true,
        S7MaxGapSlots = 2,
        S8Enabled = true,
        S12Enabled = true,
        S12MaxImbalancePercent = 20,
        S20Enabled = true,
        S20PenaltyPerPair = 5.0,
        LunchBreakIsHard = false,
        LunchBreakStart = new TimeOnly(11, 40),
        LunchBreakEnd = new TimeOnly(12, 20),
        MaxConsecutiveHours = 12,
        BreakDurationMinutes = 5,
        AmphiThreshold = 100,
        AmphiThresholdEnabled = false,
        TheoryLabSplitThreshold = 4,
        MaxDailyHours = 9,
        CurriculumConflictMode = "curriculum",
    };

    private static SectionSnapshot Section(
        int id, string code, int? instructorId = null, int? classroomId = null,
        int capacity = 30, int classroomCapacity = 40,
        int theoryHours = 2, int labHours = 0,
        int? departmentId = null, int? suggestedSemester = null,
        params (DayOfWeek Day, int StartHour, int EndHour)[] blocks)
        => new()
        {
            SectionId = id, SectionNumber = id, CourseId = id, CourseCode = code, CourseName = code,
            TermId = 100, TheoryHours = theoryHours, LabHours = labHours,
            Capacity = capacity, ClassroomId = classroomId, ClassroomName = $"D{classroomId}",
            ClassroomCapacity = classroomCapacity, ClassroomType = ClassroomType.Lecture,
            InstructorId = instructorId, InstructorName = $"Hoca{instructorId}",
            DepartmentId = departmentId, SuggestedSemester = suggestedSemester,
            TimeBlocks = blocks.Select(b => new TimeBlock
            {
                Day = b.Day,
                StartTime = new TimeOnly(b.StartHour, 0),
                EndTime = new TimeOnly(b.EndHour, 0),
                ClassroomId = classroomId,
            }).ToList(),
            AssignedTimeSlotCount = blocks.Sum(b => b.EndHour - b.StartHour),
        };

    private static TermSnapshot TermWith(params SectionSnapshot[] sections) => new()
    {
        TermId = 100, TermName = "Test", ScopeName = "Test",
        Sections = sections.ToList(),
    };

    [Fact]
    public void S1_Occupancy_LowFill_PinnedPenalty()
    {
        // Capacity 10 / ClassroomCapacity 40 = %25 occupancy < S1MinOccupancy 60
        // Beklenen: (60 - 25) × 0.5 = 17.5
        var candidate = Section(1, "TEST101", classroomId: 1, capacity: 10, classroomCapacity: 40,
            blocks: (DayOfWeek.Monday, 9, 11));
        var snapshot = TermWith(candidate);

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S1 LowFill: {penalty}");
        penalty.Should().BeApproximately(17.5, 0.01);
    }

    [Fact]
    public void S1_Occupancy_HighFill_PinnedPenalty()
    {
        // Capacity 40 / ClassroomCapacity 40 = %100 > S1MaxOccupancy 85
        // Beklenen: (100 - 85) × 0.5 = 7.5
        var candidate = Section(1, "TEST101", classroomId: 1, capacity: 40, classroomCapacity: 40,
            blocks: (DayOfWeek.Monday, 9, 11));
        var snapshot = TermWith(candidate);

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S1 HighFill: {penalty}");
        penalty.Should().BeApproximately(7.5, 0.01);
    }

    [Fact]
    public void S2_DailyLoad_Exceeds_PinnedPenalty()
    {
        // Hoca 1: existing 5h + candidate 2h aynı gün = 7h > S2MaxDailyHours 6
        // V1 mantığı: (totalHours - cap) × 10 = (7-6) × 10 = 10
        var existing = Section(1, "OTHER", instructorId: 1, classroomId: 1, capacity: 20, classroomCapacity: 40,
            blocks: (DayOfWeek.Monday, 9, 14));
        var candidate = Section(2, "TEST", instructorId: 1, classroomId: 2, capacity: 20, classroomCapacity: 40,
            blocks: (DayOfWeek.Monday, 15, 17));
        var snapshot = TermWith(existing, candidate);

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S2 Exceeds: {penalty}");
        penalty.Should().BeApproximately(35.0, 0.01); // V1 baseline (Plan 12 Faz 0.1 pinning)
    }

    [Fact]
    public void S3_SlotCongestion_Exceeds_PinnedPenalty()
    {
        // 6 section aynı (Monday, 9-11) slotunda → cap 5 → (6-5) × 5 = 5
        var sections = Enumerable.Range(1, 5).Select(i => Section(
            i, $"S{i}", instructorId: i, classroomId: i, capacity: 20, classroomCapacity: 40,
            blocks: (DayOfWeek.Monday, 9, 11))).ToArray();
        var candidate = Section(99, "CAND", instructorId: 99, classroomId: 99, capacity: 20, classroomCapacity: 40,
            blocks: (DayOfWeek.Monday, 9, 11));
        var snapshot = TermWith(sections.Append(candidate).ToArray());

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S3 Exceeds: {penalty}");
        penalty.Should().BeApproximately(15.0, 0.01); // V1 baseline
    }

    [Fact]
    public void S4_CoEnrollment_Overlap_PinnedPenalty()
    {
        // Course 1 ↔ Course 2 co-enrollment pair, student count 10
        // Aday Course 1 + Course 2 section'ı aynı slotta → 10 × 0.5 = 5.0
        var pair = new CoEnrollmentPair { CourseId1 = 1, CourseId2 = 2, StudentCount = 10 };
        // SectionSnapshot class olduğu için CourseId override edilemez — manuel inşa ediyoruz.
        var otherCourse2 = new SectionSnapshot
        {
            SectionId = 10, SectionNumber = 10, CourseId = 2, CourseCode = "C2", CourseName = "C2",
            TermId = 100, TheoryHours = 2, Capacity = 20, ClassroomId = 2,
            ClassroomName = "D2", ClassroomCapacity = 40, ClassroomType = ClassroomType.Lecture,
            InstructorId = 2, InstructorName = "Hoca2",
            TimeBlocks = new() { new TimeBlock { Day = DayOfWeek.Monday, StartTime = new(9, 0), EndTime = new(11, 0), ClassroomId = 2 } },
            AssignedTimeSlotCount = 2,
        };
        var candidate = new SectionSnapshot
        {
            SectionId = 1, SectionNumber = 1, CourseId = 1, CourseCode = "C1", CourseName = "C1",
            TermId = 100, TheoryHours = 2, Capacity = 20, ClassroomId = 1,
            ClassroomName = "D1", ClassroomCapacity = 40, ClassroomType = ClassroomType.Lecture,
            InstructorId = 1, InstructorName = "Hoca1",
            TimeBlocks = new() { new TimeBlock { Day = DayOfWeek.Monday, StartTime = new(9, 0), EndTime = new(11, 0), ClassroomId = 1 } },
            AssignedTimeSlotCount = 2,
        };
        var snapshot = new TermSnapshot
        {
            TermId = 100, TermName = "Test", ScopeName = "Test",
            Sections = new() { candidate, otherCourse2 },
            CoEnrollmentPairs = new() { pair },
        };

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S4 Overlap: {penalty}");
        penalty.Should().BeApproximately(10.0, 0.01); // V1 baseline
    }

    [Fact]
    public void S5_LunchBreak_Overlap_PinnedPenalty()
    {
        // Block 11:30-13:00 lunch 11:40-12:20 ile çakışıyor → 8 puan
        var candidate = new SectionSnapshot
        {
            SectionId = 1, SectionNumber = 1, CourseId = 1, CourseCode = "C1", CourseName = "C1",
            TermId = 100, TheoryHours = 2, Capacity = 20, ClassroomId = 1,
            ClassroomName = "D1", ClassroomCapacity = 40, ClassroomType = ClassroomType.Lecture,
            InstructorId = 1, InstructorName = "Hoca1",
            TimeBlocks = new() { new TimeBlock { Day = DayOfWeek.Monday, StartTime = new(11, 30), EndTime = new(13, 0), ClassroomId = 1 } },
            AssignedTimeSlotCount = 1,
        };
        var snapshot = TermWith(candidate);

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S5 Overlap: {penalty}");
        penalty.Should().BeApproximately(13.0, 0.01); // V1 baseline
    }

    [Fact]
    public void S7_StudentGap_Exceeds_PinnedPenalty()
    {
        // Aynı dept+year iki section; 9-11 ile 14-16 → gap 3h > S7MaxGapSlots 2
        // V1: (gap - cap + 1) × 3 = (3-2+1) × 3 = 6
        var existing = Section(1, "EARLY", instructorId: 1, classroomId: 1,
            capacity: 20, classroomCapacity: 40, departmentId: 1, suggestedSemester: 3,
            blocks: (DayOfWeek.Monday, 9, 11));
        var candidate = Section(2, "LATE", instructorId: 2, classroomId: 2,
            capacity: 20, classroomCapacity: 40, departmentId: 1, suggestedSemester: 3,
            blocks: (DayOfWeek.Monday, 14, 16));
        var snapshot = TermWith(existing, candidate);

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S7 Exceeds: {penalty}");
        penalty.Should().BeApproximately(74.0, 0.01); // V1 baseline
    }

    [Fact]
    public void S8_InstructorDaySpread_Exceeds_PinnedPenalty()
    {
        // Hoca 3 farklı günde 2h ders. totalHours=6, S2MaxDailyHours=6 → minDays=1, actual=3
        // V1: (3-1) × 4 = 8
        var d1 = Section(1, "D1", instructorId: 1, classroomId: 1, capacity: 20, classroomCapacity: 40,
            blocks: (DayOfWeek.Monday, 9, 11));
        var d2 = Section(2, "D2", instructorId: 1, classroomId: 1, capacity: 20, classroomCapacity: 40,
            blocks: (DayOfWeek.Tuesday, 9, 11));
        var candidate = Section(3, "D3", instructorId: 1, classroomId: 1, capacity: 20, classroomCapacity: 40,
            blocks: (DayOfWeek.Wednesday, 9, 11));
        var snapshot = TermWith(d1, d2, candidate);

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S8 Exceeds: {penalty}");
        penalty.Should().BeApproximately(11.0, 0.01); // V1 baseline
    }

    [Fact]
    public void S12_WeekdayBalance_Imbalance_PinnedPenalty()
    {
        // Tüm slot'lar Pazartesi → max=N, min=0; diff büyük; threshold guard'a uğrar mı görelim
        var monSections = Enumerable.Range(1, 10).Select(i => Section(
            i, $"M{i}", instructorId: i, classroomId: i, capacity: 20, classroomCapacity: 40,
            blocks: (DayOfWeek.Monday, 9 + i % 4 * 2, 11 + i % 4 * 2))).ToArray();
        var candidate = monSections[0];
        var snapshot = TermWith(monSections);

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S12 Imbalance: {penalty}");
        penalty.Should().BeApproximately(13.0, 0.01); // V1 baseline
    }

    [Fact]
    public void S20_AdjacentYear_Overlap_PinnedPenalty()
    {
        // Adjacent year soft pair (sec1, sec2), block overlap → S20PenaltyPerPair = 5.0
        var candidate = new SectionSnapshot
        {
            SectionId = 1, SectionNumber = 1, CourseId = 1, CourseCode = "C1", CourseName = "C1",
            TermId = 100, TheoryHours = 2, Capacity = 20, ClassroomId = 1,
            ClassroomName = "D1", ClassroomCapacity = 40, ClassroomType = ClassroomType.Lecture,
            InstructorId = 1, InstructorName = "Hoca1", DepartmentId = 1,
            TimeBlocks = new() { new TimeBlock { Day = DayOfWeek.Monday, StartTime = new(9, 0), EndTime = new(11, 0), ClassroomId = 1 } },
            AssignedTimeSlotCount = 2,
        };
        var other = new SectionSnapshot
        {
            SectionId = 2, SectionNumber = 2, CourseId = 2, CourseCode = "C2", CourseName = "C2",
            TermId = 100, TheoryHours = 2, Capacity = 20, ClassroomId = 2,
            ClassroomName = "D2", ClassroomCapacity = 40, ClassroomType = ClassroomType.Lecture,
            InstructorId = 2, InstructorName = "Hoca2", DepartmentId = 1,
            TimeBlocks = new() { new TimeBlock { Day = DayOfWeek.Monday, StartTime = new(9, 0), EndTime = new(11, 0), ClassroomId = 2 } },
            AssignedTimeSlotCount = 2,
        };
        var snapshot = new TermSnapshot
        {
            TermId = 100, TermName = "Test", ScopeName = "Test",
            Sections = new() { candidate, other },
            AdjacentYearSoftPairs = new() { (1, 2) },
        };

        var (_, _, penalty) = ConstraintService.EvaluateCandidate(candidate, snapshot, BuildBaselineConfig());

        _output.WriteLine($"S20 Overlap: {penalty}");
        penalty.Should().BeApproximately(5.0, 0.01); // V1 baseline
    }
}
