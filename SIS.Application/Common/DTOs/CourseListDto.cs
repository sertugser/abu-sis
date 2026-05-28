namespace SIS.Application.Common.DTOs;

public class CourseListDto
{
    public int CourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Ects { get; set; }
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }
    public List<SectionDto> Sections { get; set; } = new();
}

public class SectionDto
{
    public int SectionId { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int AvailableSeats { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool QuotaVisible { get; set; } = true;

    /// <summary>
    /// Faz 3b: Section yayın sonrası "düzenlemek için durduruldu" — geçici olarak yeni kayda
    /// kapalı (mevcut kayıtlılar korunur). Frontend "kayda kapalı" rozeti gösterir ve plana
    /// ekleme butonunu pasifleştirir.
    /// </summary>
    public bool IsEnrollmentSuspended { get; set; }
}

public class StudentCourseDto
{
    public int CourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Ects { get; set; }
    /// <summary>Ulusal/yerel kredi (Proliz aday grid "KRD" kolonu).</summary>
    public int NationalCredit { get; set; }
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }

    /// <summary>
    /// Ders durumu: IlkDefa (hic alinmamis), Alttan (FF/NA/U - tekrar alinmali), OncedenAlindi (gecmis, yukseltme mumkun)
    /// </summary>
    public string TakeStatus { get; set; } = string.Empty;

    public bool PrerequisitesMet { get; set; }

    /// <summary>Daha once alinmissa onceki harf notu (orn: "DC", "FF", "CC")</summary>
    public string? PreviousGrade { get; set; }

    /// <summary>Mufredattaki konumu: Zorunlu / Secmeli / MufredatDisi</summary>
    public string CurriculumType { get; set; } = string.Empty;

    /// <summary>
    /// Faz C C1: Bu ders öğrencinin müfredatındaki hangi grup slot(lar)ına aday?
    /// Bir ders Karar #5 gereği birden fazla grupta yer alabilir; her aday slot
    /// inline döner. Boş liste = ders herhangi bir slot'a aday değil (zorunlu veya
    /// müfredat dışı).
    /// </summary>
    public List<ApplicablePoolSlotDto> ApplicablePoolSlots { get; set; } = new();

    public List<SectionDto> Sections { get; set; } = new();
}

/// <summary>
/// Faz C C1: Bir dersin öğrencinin müfredatında aday olduğu pool slot'unun özeti.
/// Frontend slot-bazlı ders kayıt UI'ı için kullanır.
/// </summary>
public class ApplicablePoolSlotDto
{
    public int PoolId { get; set; }
    public string SlotName { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int? EctsPerCourse { get; set; }
}

public class ActiveTermDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime AddDropStart { get; set; }
    public DateTime AddDropEnd { get; set; }
    public bool IsRegistrationOpen { get; set; }
    public bool IsAddDropOpen { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasAcademicCalendar { get; set; }
    public string? StudentRegistrationMessage { get; set; }
}
