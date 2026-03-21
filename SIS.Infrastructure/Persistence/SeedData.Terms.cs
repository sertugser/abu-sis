using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedTermsAndClassroomsAsync(AppDbContext context)
    {
        // Academic Terms
        if (!context.AcademicTerms.Any())
        {
            var terms = new (string Name, int Year, SemesterType Sem, string RegStart, string RegEnd, string AddStart, string AddEnd, string TermStart, string TermEnd, bool Active)[]
            {
                ("2020-2021 Güz",   2020, SemesterType.Fall,   "2020-09-14", "2020-09-28", "2020-09-29", "2020-10-12", "2020-09-28", "2021-01-15", false),
                ("2020-2021 Bahar", 2020, SemesterType.Spring, "2021-02-01", "2021-02-15", "2021-02-16", "2021-02-28", "2021-02-15", "2021-06-15", false),
                ("2021-2022 Güz",   2021, SemesterType.Fall,   "2021-09-13", "2021-09-27", "2021-09-28", "2021-10-11", "2021-09-27", "2022-01-14", false),
                ("2021-2022 Bahar", 2021, SemesterType.Spring, "2022-02-07", "2022-02-21", "2022-02-22", "2022-03-07", "2022-02-21", "2022-06-17", false),
                ("2022-2023 Güz",   2022, SemesterType.Fall,   "2022-09-12", "2022-09-26", "2022-09-27", "2022-10-10", "2022-09-26", "2023-01-13", false),
                ("2022-2023 Bahar", 2022, SemesterType.Spring, "2023-02-06", "2023-02-20", "2023-02-21", "2023-03-06", "2023-02-20", "2023-06-16", false),
                ("2023-2024 Güz",   2023, SemesterType.Fall,   "2023-09-01", "2023-09-15", "2023-09-16", "2023-09-30", "2023-09-16", "2024-01-10", false),
                ("2023-2024 Bahar", 2023, SemesterType.Spring, "2024-02-01", "2024-02-15", "2024-02-16", "2024-02-28", "2024-02-16", "2024-06-15", false),
                ("2024-2025 Güz",   2024, SemesterType.Fall,   "2024-09-01", "2024-09-15", "2024-09-16", "2024-09-30", "2024-09-16", "2025-01-10", false),
                ("2024-2025 Bahar", 2024, SemesterType.Spring, "2025-02-01", "2025-02-15", "2025-02-16", "2025-02-28", "2025-02-16", "2025-06-15", false),
                ("2025-2026 Güz",   2025, SemesterType.Fall,   "2026-03-01", "2026-04-15", "2026-04-16", "2026-04-30", "2026-03-01", "2026-07-15", false),
                ("2025-2026 Bahar", 2025, SemesterType.Spring, "2026-03-01", "2026-04-15", "2026-04-16", "2026-04-30", "2026-02-16", "2026-06-15", true),
            };

            foreach (var t in terms)
            {
                context.AcademicTerms.Add(new AcademicTerm
                {
                    Name = t.Name,
                    Year = t.Year,
                    Semester = t.Sem,
                    RegistrationStart = DateTime.Parse(t.RegStart, null, System.Globalization.DateTimeStyles.AssumeUniversal),
                    RegistrationEnd = DateTime.Parse(t.RegEnd, null, System.Globalization.DateTimeStyles.AssumeUniversal),
                    AddDropStart = DateTime.Parse(t.AddStart, null, System.Globalization.DateTimeStyles.AssumeUniversal),
                    AddDropEnd = DateTime.Parse(t.AddEnd, null, System.Globalization.DateTimeStyles.AssumeUniversal),
                    WithdrawalDeadlineWeek = 7,
                    TermStart = DateTime.Parse(t.TermStart, null, System.Globalization.DateTimeStyles.AssumeUniversal),
                    TermEnd = DateTime.Parse(t.TermEnd, null, System.Globalization.DateTimeStyles.AssumeUniversal),
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
}
