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
}

public class StudentCourseDto
{
    public int CourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Ects { get; set; }
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

    public List<SectionDto> Sections { get; set; } = new();
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
}
