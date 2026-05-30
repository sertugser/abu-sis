namespace SIS.Application.CourseRegistration.DTOs;

public class StudentRegistrationDto
{
    public int RegistrationId { get; set; }
    public int CourseSectionId { get; set; }
    /// <summary>Ders kimliği — "Diğer Bilgiler" detay çağrısı (/course/{id}/detail) için.</summary>
    public int CourseId { get; set; }
    /// <summary>Şube numarası (Proliz "Şb" kolonu).</summary>
    public int SectionNumber { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    /// <summary>Ulusal kredi (Proliz "Krd" kolonu). CreditSnapshot ?? Course.NationalCredit.</summary>
    public int NationalCredit { get; set; }
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    /// <summary>Uygulama saati — "T+U" gösterimi teori + (lab/uygulama) ile kurulur.</summary>
    public int? ApplicationHours { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AdvisorNote { get; set; }
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }
    public bool IsRepeated { get; set; }
    public string TakeType { get; set; } = string.Empty;
    /// <summary>Açma Nedeni adı (OpeningReasonIdSnapshot çözümlenmiş). Yoksa null.</summary>
    public string? OpeningReasonName { get; set; }
    /// <summary>Seçmeli havuz slot adı — Proliz "Grup Kod" (AppliedElectivePoolId çözümlenmiş). Yoksa null.</summary>
    public string? ElectivePoolSlotName { get; set; }
    /// <summary>Seçmeli grup kodu (havuzun ElectiveGroup.Code'u). Yoksa null.</summary>
    public string? ElectiveGroupCode { get; set; }
}
