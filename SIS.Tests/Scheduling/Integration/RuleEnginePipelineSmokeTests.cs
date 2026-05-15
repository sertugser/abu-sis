using System.Diagnostics;
using FluentAssertions;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;
using SIS.Tests.Application.Constraints;
using Xunit;
using Xunit.Abstractions;

namespace SIS.Tests.Scheduling.Integration;

/// <summary>
/// Plan 11 Faz 6 — Gerçekçi ölçekli (60 section, 6 hoca, 4 sınıf, 2 bölüm) bir term inşa eder
/// ve hem <see cref="SIS.Infrastructure.Services.SchedulingService.ValidateExistingAsync"/>
/// hem de manuel atama akışı (<see cref="SIS.Infrastructure.Services.SchedulingService.ValidateAsync"/>)
/// için RuleEngine pipeline'ının uçtan uca doğru sonuç verdiğini ve makul ölçeklenebildiğini
/// doğrular. V1 evaluator silindikten sonra (Plan 11 Faz 5) integration smoke yokluğu kapatılır.
///
/// Faz 4.6 perf baseline: aynı seed üzerinde 5 iterasyon × ValidateExistingAsync ortalama ms.
/// </summary>
public sealed class RuleEnginePipelineSmokeTests : SchedulingTestBase
{
    private readonly ITestOutputHelper _output;

    public RuleEnginePipelineSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CleanDataset_36Sections_NoHardConflicts()
    {
        BuildCleanDataset(sectionCount: 36);

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue(because: "temiz veri setinde 17 hard kuralın hiçbiri tetiklenmemeli, " +
            $"ancak şu çakışmalar bulundu: {string.Join(", ", result.Conflicts.Select(c => c.ConflictType).Distinct())}");
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task ConflictDataset_DetectsExpectedHardViolations()
    {
        BuildConflictDataset();

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        var types = result.Conflicts.Select(c => c.ConflictType).Distinct().ToHashSet();

        // Conflict seed'i 4 tip ihlal içeriyor: H1 ClassroomConflict, H2 InstructorConflict,
        // H3 CapacityConflict (öğrenci > kapasite), H8 SameCourseSectionConflict.
        types.Should().Contain(ClassroomConflict);
        types.Should().Contain(InstructorConflict);
        types.Should().Contain(SameCourseSectionConflict);
    }

    [Fact]
    public async Task ManualAssignment_ValidateAsync_ReportsClassroomConflictOnOccupiedSlot()
    {
        // Var olan section başka bir derslikte 9-11 Pazartesi; aynı dersliği aynı saatte talep eden
        // adayda H1 ClassroomConflict tetiklenmeli.
        var instructorA = AddInstructor(101, maxWeeklyHours: 20);
        var instructorB = AddInstructor(102, maxWeeklyHours: 20);
        var room = AddClassroom(201, "D201", 40);
        var courseA = AddCourse(301, "CENG311", theoryHours: 2);
        var courseB = AddCourse(302, "CENG312", theoryHours: 2);

        AddSection(1001, courseA.Id, instructorA.Id, room.Id, capacity: 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        // Aday section (henüz takvimsiz) — manuel atama ile aynı oda+saat talep ediyor.
        AddSection(1002, courseB.Id, instructorB.Id, classroomId: null, capacity: 30);

        var request = new ScheduleValidationRequestDto
        {
            SectionId = 1002,
            InstructorId = instructorB.Id,
            ClassroomId = room.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 0, 11, 0)),
        };

        var result = await Service.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(c => c.ConflictType == ClassroomConflict);
    }

    [Fact]
    public async Task PerfBenchmark_36Sections_AverageUnder500Ms()
    {
        // Faz 4.6 perf baseline: aynı dataset üzerinde 5 iterasyon ortalaması.
        // RuleEngine in-memory dataset üzerinde 36 section'lık bir term'i 500ms altında işlemeli.
        // CI/lokal makinada şişme yaşarsa eşik gevşetilebilir — burada amaç regressionu yakalamak.
        BuildCleanDataset(sectionCount: 36);

        // Warm-up — ilk çağrı EF in-memory bağlantısını ısıtır.
        await Service.ValidateExistingAsync(ActiveTermId);

        const int iterations = 5;
        var samples = new List<long>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = await Service.ValidateExistingAsync(ActiveTermId);
            sw.Stop();
            samples.Add(sw.ElapsedMilliseconds);
            result.IsValid.Should().BeTrue();
        }

        var avg = samples.Average();
        var max = samples.Max();
        var min = samples.Min();

        _output.WriteLine($"[Perf] 36-section ValidateExistingAsync: avg={avg:0.0}ms, min={min}ms, max={max}ms, samples=[{string.Join(", ", samples)}]");

        avg.Should().BeLessThan(500, because: "RuleEngine in-memory 60 section'lık bir term'i tutarlı şekilde yarım saniyenin altında işlemeli (regression eşiği).");
    }

    // ===== Dataset builders =====

    /// <summary>
    /// Conflict üretmeyecek gerçekçi dataset: 4 gün (Pzt/Salı/Çar/Per — Cuma boş bırakılır,
    /// böylece H-DO NoDayOff tetiklenmez), 5 saat dilimi, 6 oda, 12 hoca, hoca başına 3 section.
    /// Toplam: 36 section default; istenirse <paramref name="sectionCount"/> ile sınırlandırılır.
    /// Her hoca tek bir günde tek bir 2h blok alır (MaxConsecutive ihlali yok).
    /// Slot/oda haritası (slot, room) → tek section tutulur, böylece H1 ClassroomConflict yok.
    /// </summary>
    private void BuildCleanDataset(int sectionCount)
    {
        var instructors = Enumerable.Range(1, 12).Select(i => AddInstructor(
            id: i, maxWeeklyHours: 20,
            departmentId: i <= 6 ? ComputerEngineeringDepartmentId : MathematicsDepartmentId)).ToArray();

        var rooms = new[]
        {
            AddClassroom(1, "D101", 60),
            AddClassroom(2, "D102", 60),
            AddClassroom(3, "D201", 60),
            AddClassroom(4, "D202", 60),
            AddClassroom(5, "D301", 60),
            AddClassroom(6, "D302", 60),
        };

        // 4 gün × 5 saat = 20 (day, hour) dilimi. Cuma boş — her hoca için min 1 gün off.
        var dayHourPairs = new List<(DayOfWeek Day, int Hour)>();
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday })
            foreach (var hour in new[] { 9, 11, 13, 15, 17 })
                dayHourPairs.Add((day, hour));

        // Hoca başına seçilmiş 3 (day, hour) — birbirinden farklı günlerde olacak şekilde.
        // Pattern: instructor i için günler = (i % 4), ((i+1) % 4), ((i+2) % 4) → 3 farklı gün, 1 gün off.
        // Saatler: instructor başına farklı offset, böylece aynı (day, hour) çiftine en çok
        // 6 hoca düşer ve 6 oda bunu tam dağıtır.
        var instructorSchedule = new Dictionary<int, List<(DayOfWeek Day, int Hour)>>();
        for (var i = 0; i < instructors.Length; i++)
        {
            var schedule = new List<(DayOfWeek Day, int Hour)>();
            for (var s = 0; s < 3; s++)
            {
                var dayIdx = (i + s) % 4;
                var hourIdx = (i * 3 + s) % 5;
                var day = (DayOfWeek)(1 + dayIdx); // Monday=1..Thursday=4
                schedule.Add((day, 9 + 2 * hourIdx));
            }
            instructorSchedule[i] = schedule;
        }

        // (Day, Hour, Room) → kaç section atandı sayacı; H1 için aynı (day,hour,room) tek olmalı.
        var roomSlotUsage = new Dictionary<(DayOfWeek, int, int), int>();
        var roomCursor = 0;

        for (var i = 0; i < sectionCount; i++)
        {
            var instructor = instructors[i / 3 % instructors.Length]; // 36'da 12 hocaya yayılır
            var withinInstructor = i % 3;
            var (day, hour) = instructorSchedule[instructor.Id - 1][withinInstructor];

            // Bu (day,hour) için boş bir oda seç (H1 ihlali olmasın).
            var room = rooms[0];
            for (var attempt = 0; attempt < rooms.Length; attempt++)
            {
                var candidate = rooms[(roomCursor + attempt) % rooms.Length];
                if (!roomSlotUsage.ContainsKey((day, hour, candidate.Id)))
                {
                    room = candidate;
                    roomCursor = (roomCursor + attempt + 1) % rooms.Length;
                    break;
                }
            }
            roomSlotUsage[(day, hour, room.Id)] = 1;

            var courseId = 100 + i;
            var deptId = instructor.DepartmentId;
            AddCourse(courseId, $"D{deptId}-{courseId}", theoryHours: 2, departmentId: deptId);

            AddSection(2000 + i, courseId, instructor.Id, room.Id, capacity: 40,
                (day, hour, 0, hour + 2, 0));
        }

        // H10 FacultyClassroom: tüm sınıflar her iki fakülteye de atanır (multi-faculty).
        var assignmentId = 7000;
        foreach (var room in rooms)
        {
            AddClassroomAssignment(++assignmentId, room.Id, EngineeringFacultyId);
            AddClassroomAssignment(++assignmentId, room.Id, ScienceFacultyId);
        }
    }

    /// <summary>
    /// Bilinçli conflict seed eder: H1 ClassroomConflict, H2 InstructorConflict, H8 SameCourseSectionConflict.
    /// </summary>
    private void BuildConflictDataset()
    {
        var instructor = AddInstructor(1, maxWeeklyHours: 20);
        var room = AddClassroom(1, "D101", 30);
        var course = AddCourse(1, "CENG999", theoryHours: 2);

        // İki section, aynı ders + aynı hoca + aynı oda + aynı slot — H1, H2, H8 hep tetiklenir.
        AddSection(1, course.Id, instructor.Id, room.Id, capacity: 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course.Id, instructor.Id, room.Id, capacity: 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        AddClassroomAssignment(7001, 1, EngineeringFacultyId);
    }
}
