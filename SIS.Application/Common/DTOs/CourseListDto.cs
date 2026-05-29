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
