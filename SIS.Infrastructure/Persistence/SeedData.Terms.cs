using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using System.Globalization;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedTermsAndClassroomsAsync(AppDbContext context)
    {
        // Academic Terms
        if (!context.AcademicTerms.Any())
        {
            var terms = new (string Name, string ShortName, string EnglishName, int Year, SemesterType Sem, TermType TermType, string RegStart, string RegEnd, string AddStart, string AddEnd, string TermStart, string TermEnd, string WithdrawalDeadline, bool Active)[]
            {
                ("2020-2021 Güz",   "20-21 Güz",   "Fall 2020-2021",   2020, SemesterType.Fall,   TermType.Regular, "2020-09-14", "2020-09-28", "2020-09-29", "2020-10-12", "2020-09-28", "2021-01-15", "2020-11-16", false),
                ("2020-2021 Bahar", "20-21 Bahar", "Spring 2020-2021", 2020, SemesterType.Spring, TermType.Regular, "2021-02-01", "2021-02-15", "2021-02-16", "2021-02-28", "2021-02-15", "2021-06-15", "2021-04-05", false),
                ("2021-2022 Güz",   "21-22 Güz",   "Fall 2021-2022",   2021, SemesterType.Fall,   TermType.Regular, "2021-09-13", "2021-09-27", "2021-09-28", "2021-10-11", "2021-09-27", "2022-01-14", "2021-11-15", false),
                ("2021-2022 Bahar", "21-22 Bahar", "Spring 2021-2022", 2021, SemesterType.Spring, TermType.Regular, "2022-02-07", "2022-02-21", "2022-02-22", "2022-03-07", "2022-02-21", "2022-06-17", "2022-04-11", false),
                ("2022-2023 Güz",   "22-23 Güz",   "Fall 2022-2023",   2022, SemesterType.Fall,   TermType.Regular, "2022-09-12", "2022-09-26", "2022-09-27", "2022-10-10", "2022-09-26", "2023-01-13", "2022-11-14", false),
                ("2022-2023 Bahar", "22-23 Bahar", "Spring 2022-2023", 2022, SemesterType.Spring, TermType.Regular, "2023-02-06", "2023-02-20", "2023-02-21", "2023-03-06", "2023-02-20", "2023-06-16", "2023-04-10", false),
                ("2023-2024 Güz",   "23-24 Güz",   "Fall 2023-2024",   2023, SemesterType.Fall,   TermType.Regular, "2023-09-01", "2023-09-15", "2023-09-16", "2023-09-30", "2023-09-16", "2024-01-10", "2023-11-04", false),
                ("2023-2024 Bahar", "23-24 Bahar", "Spring 2023-2024", 2023, SemesterType.Spring, TermType.Regular, "2024-02-01", "2024-02-15", "2024-02-16", "2024-02-28", "2024-02-16", "2024-06-15", "2024-04-05", false),
                ("2024-2025 Güz",   "24-25 Güz",   "Fall 2024-2025",   2024, SemesterType.Fall,   TermType.Regular, "2024-09-01", "2024-09-15", "2024-09-16", "2024-09-30", "2024-09-16", "2025-01-10", "2024-11-04", false),
                ("2024-2025 Bahar", "24-25 Bahar", "Spring 2024-2025", 2024, SemesterType.Spring, TermType.Regular, "2025-02-01", "2025-02-15", "2025-02-16", "2025-02-28", "2025-02-16", "2025-06-15", "2025-04-07", false),
                ("2025-2026 Güz",   "25-26 Güz",   "Fall 2025-2026",   2025, SemesterType.Fall,   TermType.Regular, "2026-03-01", "2026-04-15", "2026-04-16", "2026-04-30", "2026-03-01", "2026-07-15", "2026-04-19", false),
                ("2025-2026 Bahar", "25-26 Bahar", "Spring 2025-2026", 2025, SemesterType.Spring, TermType.Regular, "2026-02-16", "2026-02-20", "2026-03-02", "2026-03-06", "2026-02-23", "2026-06-05", "2026-04-03", true),
            };

            foreach (var t in terms)
            {
                context.AcademicTerms.Add(new AcademicTerm
                {
                    Name = t.Name,
                    ShortName = t.ShortName,
                    EnglishName = t.EnglishName,
                    Year = t.Year,
                    Semester = t.Sem,
                    TermType = t.TermType,
                    RegistrationStart = ParseUtcDate(t.RegStart),
                    RegistrationEnd = ParseUtcDate(t.RegEnd),
                    AddDropStart = ParseUtcDate(t.AddStart),
                    AddDropEnd = ParseUtcDate(t.AddEnd),
                    WithdrawalDeadline = ParseUtcDate(t.WithdrawalDeadline),
                    TermStart = ParseUtcDate(t.TermStart),
                    TermEnd = ParseUtcDate(t.TermEnd),
                    IsActive = t.Active
                });
            }
            await context.SaveChangesAsync();
        }

        // Classrooms
        if (!context.Classrooms.Any())
        {
            var classrooms = new (string Name, int Capacity, ClassroomType Type)[]
            {
                ("AMFİ 1", 110, ClassroomType.Lecture),
                ("Amfi - Sınıf-Yeni-Kat Sıfır", 80, ClassroomType.Lecture),
                ("C 017", 70, ClassroomType.Lecture),
                ("C 018", 70, ClassroomType.Lecture),
                ("C 025", 70, ClassroomType.Lecture),
                ("C 026", 70, ClassroomType.Lecture),
                ("C 036", 80, ClassroomType.Lecture),
                ("C 054", 30, ClassroomType.Lecture),
                ("C 005", 50, ClassroomType.Lecture),
                ("L139 Ceng Lab-1", 30, ClassroomType.Lab),
                ("L141 Ceng Lab-2", 30, ClassroomType.Lab),
                ("Ceng Lab-3 (Kat 0)", 30, ClassroomType.Lab),
                ("L140 EEE LAB", 30, ClassroomType.Lab),
                ("L143 PHY LAB", 30, ClassroomType.Lab),
            };

            foreach (var (name, capacity, type) in classrooms)
            {
                context.Classrooms.Add(new Classroom { Name = name, Capacity = capacity, Type = type });
            }
            await context.SaveChangesAsync();
        }
    }

    internal static async Task SeedCalendarEventsAsync(AppDbContext context)
    {
        if (context.TermCalendarEvents.Any())
            return;

        var springTerm = await context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Year == 2025 && t.Semester == SemesterType.Spring);

        if (springTerm is null)
            return;

        var events = new[]
        {
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.GraduateApplication,  Title = "Lisansüstü Program Başvuruları",          StartDate = Utc(2025, 11, 24), EndDate = Utc(2026, 2, 12),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.HorizontalTransfer,   Title = "Yatay Geçiş Başvuruları",                 StartDate = Utc(2026,  1, 26), EndDate = Utc(2026, 1, 30),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.DoubleMajor,          Title = "Çift Anadal / Yan Dal Başvuruları",       StartDate = Utc(2026,  1, 26), EndDate = Utc(2026, 1, 30),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.TuitionPayment,       Title = "Harç Ödeme",                              StartDate = Utc(2026,  1, 26), EndDate = Utc(2026, 2, 13),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.CourseRegistration,   Title = "Ders Kayıt ve Danışman Onayları",         StartDate = Utc(2026,  2, 16), EndDate = Utc(2026, 2, 20),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.ClassesStart,         Title = "Derslerin Başlangıcı",                    StartDate = Utc(2026,  2, 23), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.AddDrop,              Title = "Ders Ekle / Bırak",                       StartDate = Utc(2026,  3,  2), EndDate = Utc(2026, 3,  6),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.ReligiousHoliday,     Title = "Ramazan Bayramı Tatili",                  StartDate = Utc(2026,  3, 19), EndDate = Utc(2026, 3, 22),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.CourseWithdrawal,     Title = "Ders Çekilme Son Günü",                   StartDate = Utc(2026,  4,  3), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.Other,                Title = "Kayıt Dondurma Başvurusu Son Günü",       StartDate = Utc(2026,  4,  3), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MidtermExam,          Title = "Ara Sınavlar",                            StartDate = Utc(2026,  4,  6), EndDate = Utc(2026, 4, 11),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MidtermExam,          Title = "Ara Sınav Mazeret Sınavları",             StartDate = Utc(2026,  4, 15), EndDate = Utc(2026, 4, 17),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.NationalHoliday,      Title = "Ulusal Egemenlik ve Çocuk Bayramı",       StartDate = Utc(2026,  4, 23), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MidtermGradePublication, Title = "Ara Sınav Notlarının Yayımlanması",    StartDate = Utc(2026,  4, 24), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.NationalHoliday,      Title = "Emek ve Dayanışma Günü",                  StartDate = Utc(2026,  5,  1), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.NationalHoliday,      Title = "Atatürk'ü Anma, Gençlik ve Spor Bayramı", StartDate = Utc(2026, 5, 19), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.ReligiousHoliday,     Title = "Kurban Bayramı Tatili",                   StartDate = Utc(2026,  5, 26), EndDate = Utc(2026, 5, 30),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.ClassesEnd,           Title = "Derslerin Sonu",                          StartDate = Utc(2026,  6,  5), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.FinalExam,            Title = "Final Sınavları",                         StartDate = Utc(2026,  6,  8), EndDate = Utc(2026, 6, 19),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.Other,                Title = "Hazırlık Muafiyet Sınavları",             StartDate = Utc(2026,  6, 15), EndDate = Utc(2026, 6, 18),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.FinalGradePublication, Title = "Final Notlarının Yayımlanması",          StartDate = Utc(2026,  6, 24), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MakeupExam,           Title = "Bütünleme Sınavı Başvuru Son Günü",       StartDate = Utc(2026,  6, 26), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MakeupExam,           Title = "Bütünleme Sınavları",                     StartDate = Utc(2026,  6, 29), EndDate = Utc(2026, 7,  4),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MakeupGradePublication, Title = "Bütünleme Notlarının Yayımlanması",    StartDate = Utc(2026,  7,  8), EndDate = null,              IsPublic = true },
        };

        context.TermCalendarEvents.AddRange(events);
        await context.SaveChangesAsync();
    }

    private static DateTime ParseUtcDate(string value) =>
        DateTime.SpecifyKind(
            DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeKind.Utc);

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
