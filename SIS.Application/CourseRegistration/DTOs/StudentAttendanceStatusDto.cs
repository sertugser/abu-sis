namespace SIS.Application.CourseRegistration.DTOs;

public class StudentAttendanceStatusDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public List<StudentAttendanceTermDto> AvailableTerms { get; set; } = new();
    public StudentAttendancePolicyDto Policy { get; set; } = new();
    public List<StudentAttendanceCourseDto> Courses { get; set; } = new();
}

public class StudentAttendanceTermDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class StudentAttendancePolicyDto
{
    public int TermWeekCount { get; set; }
    public decimal TheoryAbsenceLimitPercent { get; set; }
    public decimal LabAbsenceLimitPercent { get; set; }
    /// <summary>
    /// Global attendance method from SystemConfig ("Separate", "TotalHours", "Passive", "MarkOnly").
    /// </summary>
    public string AttendanceMethod { get; set; } = "Separate";
}

public class StudentAttendanceCourseDto
{
    public int RegistrationId { get; set; }
    public int CourseSectionId { get; set; }
    public int SectionNumber { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public int NationalCredit { get; set; }
    public int SuggestedSemester { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public bool MandatoryAttendance { get; set; }
    public bool AttendanceEntered { get; set; }
    public int? TheoryAbsenceHours { get; set; }
    public int? LabAbsenceHours { get; set; }
    public int? TotalAbsenceHours { get; set; }
    public int TotalTheoryHours { get; set; }
    public int TotalLabHours { get; set; }
    public int TheoryAbsenceLimitHours { get; set; }
    public int LabAbsenceLimitHours { get; set; }
    /// <summary>Birleşik (teori+uygulama) limit — yalnızca TotalHours yönteminde anlamlı.</summary>
    public int TotalAbsenceLimitHours { get; set; }
    /// <summary>
    /// Devamsızlık takibi bu ders için etkin mi? Passive/MarkOnly yönteminde false
    /// (limit gösterilse de devamsızlıktan kalma uygulanmaz).
    /// </summary>
    public bool AttendanceTracked { get; set; }
    /// <summary>
    /// Yürürlükteki yönteme göre öğrenci bu dersten devamsızlıktan kaldı mı?
    /// Yoklama girilmemişse veya takip kapalıysa false.
    /// </summary>
    public bool IsFailedByAbsence { get; set; }
    public string AttendanceStatusLabel { get; set; } = string.Empty;
}
