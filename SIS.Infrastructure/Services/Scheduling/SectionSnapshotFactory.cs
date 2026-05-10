using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Tek otorite SectionSnapshot oluşturucu / yenileyici.
///
/// PROBLEM (refactor öncesi): SectionSnapshot 6 farklı yerde manuel field-by-field
/// kopyalanıyordu (AutoScheduleEngine, ConstructionSolver, MutableScheduleState,
/// FailureAnalyzer, SchedulingService.BuildExistingSectionSnapshot, BuildCandidateSnapshot).
/// Yeni bir field eklendiğinde 6 yerin tümünün güncellenmesi gerekiyordu — birini
/// unutursak ilgili kod yolu sessizce default değer kullanıyor → bug.
/// Bu konuşmada düzelttiğimiz 4 ayrı bug (TeachingMethod, ParalelSplit capacity,
/// AssignedTimeSlotCount paralel split sayma, vb) hep bu DRY ihlalinin tezahürüydü.
///
/// ÇÖZÜM: tüm snapshot oluşturma + güncelleme bu factory'den yapılır. Yeni field
/// eklendiğinde sadece bu dosya güncellenir, 6 yer otomatik beneficiates.
/// </summary>
public static class SectionSnapshotFactory
{
    /// <summary>
    /// CourseSection entity'sinden tam snapshot inşa eder.
    /// Auto-schedule preview, manuel atama validate, mevcut section snapshot'ı
    /// (DB'den okunmuş) — hepsinin tek otoritesi.
    /// </summary>
    public static SectionSnapshot FromCourseSection(
        CourseSection section,
        Classroom? classroom,
        Instructor? instructor,
        string instructorName,
        IReadOnlyList<TimeBlock> timeBlocks,
        int? suggestedSemester,
        Dictionary<int, int> suggestedSemestersByDept,
        HashSet<int> accessibleDepts,
        IReadOnlyDictionary<int, Classroom>? classroomLookup = null,
        List<string>? notes = null)
    {
        // Paralel split-aware AssignedTimeSlotCount: aynı (Day, Start, End) penceresinde
        // birden fazla classroom varsa bunlar tek zaman dilimi sayılır.
        // Eski yanlış: blokların düz toplamı (3T+4L için 11 saat çıkarıyordu, beklenen 7).
        var totalHours = ComputeAssignedTimeSlotCount(timeBlocks);

        // Paralel split kapasite: primary window'daki tüm classroom'ların toplamı.
        // Eski yanlış: tek classroom kapasitesi → H3 CapacityEvaluator yanıltıcı fail.
        var (effectiveClassroomId, effectiveClassroomName, effectiveClassroomCapacity, effectiveClassroomType)
            = ResolveClassroomMetadata(classroom, timeBlocks, classroomLookup);

        return new SectionSnapshot
        {
            SectionId = section.Id,
            SectionNumber = section.SectionNumber,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TermId = section.TermId,
            TeachingMethod = section.Course.TeachingMethod,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            ClassroomId = effectiveClassroomId,
            ClassroomName = effectiveClassroomName,
            ClassroomCapacity = effectiveClassroomCapacity,
            ClassroomType = effectiveClassroomType,
            TheoryClassroomType = section.Course.TheoryClassroomType,
            LabClassroomType = section.Course.LabClassroomType,
            InstructorId = instructor?.Id,
            InstructorName = instructorName,
            InstructorMaxWeeklyHours = instructor?.MaxWeeklyHours ?? 0,
            DepartmentId = section.Course.DepartmentId,
            SuggestedSemester = suggestedSemester,
            SuggestedSemestersByDepartment = suggestedSemestersByDept,
            AccessibleDepartmentIds = accessibleDepts,
            AssignedTimeSlotCount = totalHours,
            TimeBlocks = timeBlocks.ToList(),
            Notes = notes ?? new List<string>()
        };
    }

    /// <summary>
    /// Mevcut snapshot'ı yeni TimeBlock listesi ile clone'lar; kapasite ve toplam saat
    /// otomatik yeniden hesaplanır. Block'ların ClassroomId'si üzerinden classroomLookup'a
    /// bakarak primary window paralel-split kapasitesini doğru bulur.
    /// ConstructionSolver, MutableScheduleState, FailureAnalyzer için.
    /// </summary>
    public static SectionSnapshot WithBlocks(
        SectionSnapshot baseSnap,
        IReadOnlyList<TimeBlock> timeBlocks,
        IReadOnlyDictionary<int, Classroom>? classroomLookup = null)
    {
        var totalHours = ComputeAssignedTimeSlotCount(timeBlocks);

        var (effectiveClassroomId, effectiveClassroomName, effectiveClassroomCapacity, effectiveClassroomType)
            = ResolveClassroomMetadata(
                primaryClassroom: null,
                timeBlocks,
                classroomLookup,
                fallbackName: baseSnap.ClassroomName,
                fallbackCapacity: baseSnap.ClassroomCapacity,
                fallbackType: baseSnap.ClassroomType,
                fallbackId: baseSnap.ClassroomId);

        return new SectionSnapshot
        {
            SectionId = baseSnap.SectionId,
            SectionNumber = baseSnap.SectionNumber,
            CourseId = baseSnap.CourseId,
            CourseCode = baseSnap.CourseCode,
            CourseName = baseSnap.CourseName,
            TermId = baseSnap.TermId,
            TeachingMethod = baseSnap.TeachingMethod,
            TheoryHours = baseSnap.TheoryHours,
            LabHours = baseSnap.LabHours,
            Capacity = baseSnap.Capacity,
            EnrolledCount = baseSnap.EnrolledCount,
            ClassroomId = effectiveClassroomId,
            ClassroomName = effectiveClassroomName,
            ClassroomCapacity = effectiveClassroomCapacity,
            ClassroomType = effectiveClassroomType,
            TheoryClassroomType = baseSnap.TheoryClassroomType,
            LabClassroomType = baseSnap.LabClassroomType,
            InstructorId = baseSnap.InstructorId,
            InstructorName = baseSnap.InstructorName,
            InstructorMaxWeeklyHours = baseSnap.InstructorMaxWeeklyHours,
            DepartmentId = baseSnap.DepartmentId,
            SuggestedSemester = baseSnap.SuggestedSemester,
            SuggestedSemestersByDepartment = baseSnap.SuggestedSemestersByDepartment,
            AccessibleDepartmentIds = baseSnap.AccessibleDepartmentIds,
            AssignedTimeSlotCount = totalHours,
            TimeBlocks = timeBlocks.ToList(),
            Notes = baseSnap.Notes
        };
    }

    /// <summary>
    /// Paralel split-aware AssignedTimeSlotCount.
    /// Aynı (Day, Start, End) penceresinde birden fazla classroom = tek zaman dilimi.
    /// </summary>
    public static int ComputeAssignedTimeSlotCount(IReadOnlyList<TimeBlock> blocks)
        => blocks
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .Sum(g => (int)Math.Ceiling((g.Key.EndTime - g.Key.StartTime).TotalHours));

    /// <summary>
    /// Aynı varsayılan paralel-split capacity hesabı; BlockAssignment listesi üzerinden.
    /// </summary>
    public static int ComputeAssignedTimeSlotCountFromBlockAssignments(IReadOnlyList<BlockAssignment> blocks)
        => blocks
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .Sum(g => (int)Math.Ceiling((g.Key.EndTime - g.Key.StartTime).TotalHours));

    /// <summary>
    /// Primary window'daki tüm classroom kapasitelerinin toplamı.
    /// Paralel split (aynı pencere, birden fazla oda) kapasitesini doğru hesaplar.
    /// Block listesi boşsa primaryClassroom + fallbackler kullanılır.
    /// </summary>
    private static (int? Id, string Name, int Capacity, ClassroomType Type) ResolveClassroomMetadata(
        Classroom? primaryClassroom,
        IReadOnlyList<TimeBlock> blocks,
        IReadOnlyDictionary<int, Classroom>? classroomLookup,
        string? fallbackName = null,
        int? fallbackCapacity = null,
        ClassroomType? fallbackType = null,
        int? fallbackId = null)
    {
        // Bloklar boş → fallback değerleri (atanmamış section)
        if (blocks.Count == 0)
        {
            return (
                primaryClassroom?.Id ?? fallbackId,
                primaryClassroom?.Name ?? fallbackName ?? string.Empty,
                primaryClassroom?.Capacity ?? fallbackCapacity ?? 0,
                primaryClassroom?.Type ?? fallbackType ?? ClassroomType.Lecture
            );
        }

        // Primary classroom bloklardan ilkinin classroom'u (fallback: primaryClassroom param)
        var firstBlock = blocks[0];
        var primaryId = firstBlock.ClassroomId ?? primaryClassroom?.Id ?? fallbackId;

        // Primary classroom info için lookup'tan veya parametreden çek
        Classroom? resolvedPrimary = primaryClassroom;
        if (resolvedPrimary == null && primaryId.HasValue && classroomLookup != null)
            classroomLookup.TryGetValue(primaryId.Value, out resolvedPrimary);

        var name = resolvedPrimary?.Name ?? fallbackName ?? string.Empty;
        var type = resolvedPrimary?.Type ?? fallbackType ?? ClassroomType.Lecture;

        // Paralel split capacity: ilk bloğun penceresinde olan tüm bloklar = paralel
        var primaryWindowBlocks = blocks
            .Where(b => b.Day == firstBlock.Day
                && b.StartTime == firstBlock.StartTime
                && b.EndTime == firstBlock.EndTime)
            .ToList();

        var combinedCapacity = 0;
        foreach (var b in primaryWindowBlocks)
        {
            if (b.ClassroomId.HasValue && classroomLookup != null
                && classroomLookup.TryGetValue(b.ClassroomId.Value, out var cr))
            {
                combinedCapacity += cr.Capacity;
            }
        }

        // Lookup yoksa primary classroom + fallback kullan
        var capacity = combinedCapacity > 0
            ? combinedCapacity
            : (resolvedPrimary?.Capacity ?? fallbackCapacity ?? 0);

        return (primaryId, name, capacity, type);
    }
}
