namespace SIS.Application.CourseOpening.DTOs;

// ── Response DTOs ──

public class OpenedCourseSectionDto
{
    public int SectionId { get; set; }
    public int SectionNumber { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int Credits { get; set; }
    public int Ects { get; set; }
    public int SuggestedSemester { get; set; }
    public int Capacity { get; set; }
    public int? InternalQuota { get; set; }
    public int? ExternalQuota { get; set; }
    public int EnrolledCount { get; set; }
    public int RegisteredCount { get; set; }
    public int DraftOrPendingCount { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public bool IsActive { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>Cross-list grup ID'si (null = tek başına).</summary>
    public int? CrossListGroupId { get; set; }
    /// <summary>Z (Zorunlu) / S (Seçmeli) — bölümün aktif müfredatından alınır.</summary>
    public bool IsCompulsory { get; set; }
}

public class OpenedCourseListDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public int TermId { get; set; }
    public string TermName { get; set; } = "";
    public int TotalSections { get; set; }
    public List<OpenedCourseSectionDto> Sections { get; set; } = [];
}

public class CurriculumCourseForOpeningDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int NationalCredit { get; set; }
    public int Ects { get; set; }
    public int SuggestedSemester { get; set; }
    public bool IsCompulsory { get; set; }
    public bool AlreadyOpened { get; set; }
    public int ExistingSectionCount { get; set; }

    // Forecast data
    public int PredictedDemand { get; set; }
    public int RetakeCount { get; set; }  // FF+NA+U
    public int RecommendedSectionCount { get; set; }
    public int RecommendedCapacity { get; set; }

    // Cross-department talep
    public List<CrossDepartmentDemandDto> CrossDepartmentDemand { get; set; } = new();

    // Önceki dönem verisi
    public int? PreviousTermEnrollment { get; set; }

    public bool CanOpen { get; set; }

    /// <summary>
    /// CanOpen = false olduğunda sebep: "TermNotInPlanning" | "NotOwner" | "CourseUnassigned".
    /// Null ise açılabilir (CanOpen = true).
    /// </summary>
    public string? BlockReason { get; set; }

    // ── Seçmeli havuz bilgileri (null ise zorunlu ders veya müfredatta tekil tanım) ──
    /// <summary>CurriculumElectivePool Id (null = zorunlu veya tekil seçmeli).</summary>
    public int? ElectivePoolId { get; set; }
    /// <summary>ElectiveGroup kodu (ör. "NDE", "TE", "OC"). Null = havuza bağlı değil.</summary>
    public string? ElectiveGroupCode { get; set; }
    /// <summary>ElectiveGroup adı (ör. "Bölüm Dışı Seçmeli"). Null = havuza bağlı değil.</summary>
    public string? ElectiveGroupName { get; set; }
    /// <summary>Kaynak: "Compulsory" (zorunlu müfredat), "Elective" (tekil seçmeli), "Pool" (havuz üyesi).</summary>
    public string Source { get; set; } = "Compulsory";
}

/// <summary>
/// Havuz slot satırı (ders açma sayfası için). Müfredattaki bir CurriculumElectivePool kaydını,
/// içindeki aday ders sayısı + termId'de açılmış kaç tanesi olduğu özetiyle birlikte döner.
/// </summary>
public class PoolSlotForOpeningDto
{
    public int PoolId { get; set; }
    public string SlotName { get; set; } = "";
    public int ElectiveGroupId { get; set; }
    public string ElectiveGroupName { get; set; } = "";
    /// <summary>ElectiveSubtype.Code — TE/NDE/OC veya custom. Frontend renk için kullanır.</summary>
    public string SubtypeCode { get; set; } = "";
    public string SubtypeName { get; set; } = "";
    public int SuggestedSemesterStart { get; set; }
    public int SuggestedSemesterEnd { get; set; }
    /// <summary>Havuz override AKTS — null ise her aday dersin kendi katalog AKTS'si geçerli.</summary>
    public int? EctsPerCourse { get; set; }
    public int CandidateCount { get; set; }
    /// <summary>Aday dersler arasında verilen termId'de en az 1 aktif şubeye sahip olanların sayısı.</summary>
    public int OpenedCandidateCount { get; set; }
}

/// <summary>
/// Havuz aday dersi (modal için). Bir derin liste olmadığı için minimum alanları içerir.
/// </summary>
public class PoolCandidateDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int NationalCredit { get; set; }
    public int Ects { get; set; }
    public bool AlreadyOpened { get; set; }
    public int ExistingSectionCount { get; set; }
    public bool CanOpen { get; set; }
    /// <summary>CanOpen=false ise sebep: TermNotInPlanning | NotOwner | CourseUnassigned. Null = açılabilir.</summary>
    public string? BlockReason { get; set; }
}

public class CrossDepartmentDemandDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public string ProgramName { get; set; } = "";
    public int EstimatedStudentCount { get; set; }
}

public class InstructorOptionDto
{
    public int InstructorId { get; set; }
    public string FullName { get; set; } = "";
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public string DepartmentCode { get; set; } = "";
    public int MaxWeeklyHours { get; set; }
    public int AssignedWeeklyHours { get; set; }
}

// ── Request DTOs ──

public class OpenCourseRequest
{
    public int CourseId { get; set; }
    public int SectionCount { get; set; } = 1;
    public int CapacityPerSection { get; set; } = 40;
    public int? InternalQuota { get; set; }
    public int? ExternalQuota { get; set; }
    public int? InstructorId { get; set; }
    /// <summary>
    /// Dersin hangi bölümlere açık olacağı. Null/boş ise müfredattaki tüm bölümlere otomatik açılır.
    /// </summary>
    public List<int>? AccessibleDepartmentIds { get; set; }

    /// <summary>
    /// Madde 14: Cross-list'li dersler için paralel section da açılsın mı?
    /// Ör. YZM301 açılırken BIL301 için de paralel section (aynı hoca/kapasite/grup) oluşturulur.
    /// </summary>
    public bool IncludeCrossLists { get; set; } = false;
}

public class BulkOpenCoursesRequest
{
    public List<OpenCourseRequest> Courses { get; set; } = [];
}

public class BulkOpenCoursesResultDto
{
    public int TotalCoursesOpened { get; set; }
    public int TotalSectionsCreated { get; set; }
    /// <summary>
    /// Başarıyla açılan dersler için özet liste — modal'da satır satır gösterilir.
    /// Hata olursa transaction rollback'i ile bu liste de temizlenir (boş döner).
    /// </summary>
    public List<OpenedCourseSummaryDto> OpenedCourses { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public class OpenedCourseSummaryDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int SectionCount { get; set; }
    /// <summary>Açılan şubelerin numaraları — kullanıcıya "Şube 1, 2, 3" şeklinde gösterilir.</summary>
    public List<int> SectionNumbers { get; set; } = [];
    public int TotalCapacity { get; set; }
    /// <summary>Tüm şubelerde aynı hoca varsa adı, farklıysa null.</summary>
    public string? InstructorName { get; set; }
}

public class UpdateOpenedSectionRequest
{
    public int SectionId { get; set; }
    public int? InstructorId { get; set; }
    public int? Capacity { get; set; }
    public int? InternalQuota { get; set; }
    public int? ExternalQuota { get; set; }
}

// ── Bulk Assign ──

public class BulkAssignInstructorRequest
{
    public List<SectionInstructorAssignment> Assignments { get; set; } = [];
}

public class SectionInstructorAssignment
{
    public int SectionId { get; set; }
    public int InstructorId { get; set; }
}

public class BulkAssignInstructorResultDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class InstructorWorkloadDto
{
    public int InstructorId { get; set; }
    public string FullName { get; set; } = "";
    public string Title { get; set; } = "";
    public int MaxWeeklyHours { get; set; }
    public int AssignedWeeklyHours { get; set; }
    public int RemainingCapacity { get; set; }
    public List<AssignedSectionDto> AssignedSections { get; set; } = new();
}

public class AssignedSectionDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? DepartmentCode { get; set; }
    public int SectionNumber { get; set; }
    public int WeeklyHours { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
}

// ── Term Transfer ──

public record PreviousTermCourseDto(
    int CourseId, string CourseCode, string CourseName,
    int Ects, int Credits, int SuggestedSemester, bool IsCompulsory,
    int SectionCount, int TotalCapacity,
    string? InstructorName, int? InstructorId,
    bool AlreadyOpened,
    int TotalEnrolled,
    int? PredictedDemand,
    int? SourceInternalQuota,
    int? SourceExternalQuota,
    List<int>? SourceAccessibleDepartmentIds,
    bool HasCrossLists,
    bool HasSchedule);

public class TransferCourseItem
{
    public int CourseId { get; set; }
    public int? SectionCount { get; set; }
    public int? CapacityPerSection { get; set; }
    public int? InstructorId { get; set; }
    public int? InternalQuota { get; set; }
    public int? ExternalQuota { get; set; }
    public List<int>? AccessibleDepartmentIds { get; set; }
    public bool IncludeCrossLists { get; set; }
    public bool IncludeSchedule { get; set; }
}

public class TransferFromTermRequest
{
    public int DepartmentId { get; set; }
    public int SourceTermId { get; set; }
    public int TargetTermId { get; set; }
    public List<TransferCourseItem> Courses { get; set; } = [];
}

public class TransferFromTermResultDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalSectionsCreated { get; set; }
    public List<TransferCourseResultItem> Results { get; set; } = [];
}

public class TransferCourseResultItem
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int SectionsCreated { get; set; }
    public List<string> Warnings { get; set; } = [];
    /// <summary>
    /// Beklenmeyen exception için olay tanımlayıcısı (Guid kısa formu).
    /// Loga aynı id ile düşer; admin sorun bildirildiğinde bu id ile arar.
    /// </summary>
    public string? IncidentId { get; set; }
}

public record CloseAllSectionsResultDto(int ClosedSectionCount, int DroppedRegistrationCount);

public record DeleteAllSectionsResultDto(int DeletedSectionCount, int DeletedRegistrationCount, int DeletedAccessPolicyCount);

// ── External Courses (opened by other departments but in our curriculum) ──

public class ExternalCourseDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int Ects { get; set; }
    public string OwnerDepartmentName { get; set; } = "";
    public int SectionCount { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalEnrolled { get; set; }
    public int PredictedDemand { get; set; }
}
