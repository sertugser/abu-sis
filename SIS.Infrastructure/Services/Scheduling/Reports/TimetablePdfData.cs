namespace SIS.Infrastructure.Services.Scheduling.Reports;

/// <summary>
/// Öğrenci ve öğretim elemanı ders programı PDF'i için ortak (nötr) model.
/// Tek <see cref="TimetablePdfRenderer"/> bunu render eder — kaynak ne olursa olsun.
/// </summary>
public sealed record TimetablePdfData(
    string Title,        // "Öğrenci Ders Programı" / "Öğretim Elemanı Ders Programı"
    string SubjectName,  // öğrenci veya öğretim elemanı adı
    string TermName,
    IReadOnlyList<TimetablePdfSection> Sections);

public sealed record TimetablePdfSection(
    string SectionNumber,
    string CourseCode,
    IReadOnlyList<TimetablePdfBlock> Blocks);

public sealed record TimetablePdfBlock(
    int Day,            // .NET DayOfWeek: 0=Paz, 1=Pzt … 6=Cmt
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? ClassroomName,
    bool IsLab);
