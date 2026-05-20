using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 11 — Runtime rule değerlendirmesi için section'ın hafif görünümü.
/// V1 <c>SectionSnapshot</c>'ın CP-SAT olmayan saf veri kısmıdır; ScheduleSnapshotFactory'de
/// ya da manuel atama akışında <c>CourseSection</c> + <c>SectionSchedules</c>'ten inşa edilir.
/// SectionInput (V2 CP-SAT input) ile bire-bir alan örtüşmesi vardır ama IntervalVar referansı yok.
/// </summary>
public sealed record RuntimeCandidate
{
    public required int SectionId { get; init; }
    public int SectionNumber { get; init; }
    public required int CourseId { get; init; }
    public required string CourseCode { get; init; }
    public string CourseName { get; init; } = string.Empty;
    public int? DepartmentId { get; init; }
    public int? InstructorId { get; init; }
    public string InstructorName { get; init; } = string.Empty;

    /// <summary>0 ise no override (V2 WeeklyLoadEncoder default fallback uygulanır).</summary>
    public int InstructorMaxWeeklyHours { get; init; }

    public int Capacity { get; init; }
    public int EnrolledCount { get; init; }
    public int TheoryHours { get; init; }
    public int LabHours { get; init; }
    public TeachingMethod TeachingMethod { get; init; } = TeachingMethod.FaceToFace;

    /// <summary>Primary (UI gösterim için) derslik metadata — paralel-split'te ilk pencereden türetilir.
    /// Blocks boş ise (atanmamış section) bu alanlar fallback olur.</summary>
    public int? PrimaryClassroomId { get; init; }
    public string PrimaryClassroomName { get; init; } = string.Empty;
    public int PrimaryClassroomCapacity { get; init; }
    public ClassroomType PrimaryClassroomType { get; init; }

    /// <summary>H4 Theory classroom type override (Course.TheoryClassroomType).</summary>
    public int? RequiredTheoryClassroomType { get; init; }
    /// <summary>H4 Lab classroom type override (Course.LabClassroomType).</summary>
    public int? RequiredLabClassroomType { get; init; }

    /// <summary>H5 cohort/curriculum için (1-8 typical).</summary>
    public int? SuggestedSemester { get; init; }

    /// <summary>H5 cohort mode için; CourseId -> DeptId -> Semester.</summary>
    public IReadOnlyDictionary<int, int> SuggestedSemestersByDepartment { get; init; }
        = new Dictionary<int, int>();

    /// <summary>H5 cohort accessibility; bu CourseId hangi departman müfredatında.</summary>
    public IReadOnlyCollection<int> AccessibleDepartmentIds { get; init; } = Array.Empty<int>();

    /// <summary>H5 zorunlu/seçmeli ayrımı — kursun en az bir müfredatta zorunlu olup
    /// olmadığını gösterir. Curriculum mode'da iki seçmeli ders çakışma uyarısı vermez
    /// (öğrenci ikisini de almak zorunda değil). Default false (seçmeli gibi davran).</summary>
    public bool IsCompulsory { get; init; } = false;

    /// <summary>
    /// Atanmış zaman blokları. Paralel-lab section'larında aynı (Day, Start, End) penceresinde
    /// birden fazla blok (her grup için ayrı ClassroomId) bulunur. H1 paralel-lab self-skip için
    /// her bloğun kendi ClassroomId'si referans alınır.
    /// </summary>
    public IReadOnlyList<RuntimeBlock> Blocks { get; init; } = Array.Empty<RuntimeBlock>();
}

/// <summary>
/// Plan 11 — Runtime block. (Day, Start, End, ClassroomId) tuple'ı. V1 TimeBlock'un yeni isim ile karşılığı.
/// BlockType opsiyoneldir (geri uyumluluk için): RuleEngine'in HSO kuralı bu bilgiyi kullanarak
/// aynı section'ın Theory + Lab bloğunun aynı zaman aralığında çakışmasını yakalar. Eski çağrılar
/// null geçer (kural devreye girmez).
/// </summary>
public sealed record RuntimeBlock(
    int Day,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int? ClassroomId,
    string? ClassroomName,
    ClassroomType ClassroomType,
    int ClassroomCapacity,
    ScheduleBlockType? BlockType = null);
