using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedGradeEntryTestDataAsync(AppDbContext context)
    {
        var spring2025 = await context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Year == 2025 && t.TermType == TermType.Spring && !t.IsCancelled);
        var ceng102 = await context.Courses.FirstOrDefaultAsync(c => c.Code == "CENG102");
        var enderUserId = await context.Users
            .Where(u => u.Email == "ender.sevinc@ankarabilim.edu.tr")
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
        var ender = enderUserId.HasValue
            ? await context.Instructors.FirstOrDefaultAsync(i => i.UserId == enderUserId.Value)
            : null;

        if (spring2025 is null || ceng102 is null || ender is null)
            return;

        var section = await context.CourseSections
            .Include(s => s.Registrations)
            .FirstOrDefaultAsync(s =>
                s.CourseId == ceng102.Id &&
                s.TermId == spring2025.Id &&
                s.InstructorId == ender.Id);

        if (section is null)
        {
            var nextSectionNumber = await context.CourseSections
                .Where(s => s.CourseId == ceng102.Id && s.TermId == spring2025.Id)
                .Select(s => (int?)s.SectionNumber)
                .MaxAsync() ?? 0;

            section = new CourseSection
            {
                CourseId = ceng102.Id,
                TermId = spring2025.Id,
                InstructorId = ender.Id,
                SectionNumber = nextSectionNumber + 1,
                Capacity = 40,
                EnrolledCount = 0,
                Status = CourseSectionStatus.Active,
                IsFinalized = false,
                AreGradesPublished = false,
                GradesPublishedAt = null,
                CreatedAt = DateTime.UtcNow,
            };
            context.CourseSections.Add(section);
            await context.SaveChangesAsync();
        }
        else
        {
            section.InstructorId = ender.Id;
            section.Capacity = Math.Max(section.Capacity, 40);
            section.Status = CourseSectionStatus.Active;
            section.IsFinalized = false;
            section.AreGradesPublished = false;
            section.GradesPublishedAt = null;
            section.UpdatedAt = DateTime.UtcNow;
        }

        var existingStudentIdsInSection = await context.CourseRegistrations
            .Where(r => r.CourseSectionId == section.Id)
            .Select(r => r.StudentId)
            .ToListAsync();

        var existingCeng102StudentIdsInTerm = await context.CourseRegistrations
            .Where(r =>
                r.CourseSection.TermId == spring2025.Id &&
                r.CourseSection.CourseId == ceng102.Id &&
                r.CourseSectionId != section.Id)
            .Select(r => r.StudentId)
            .ToListAsync();

        var selectedStudents = await context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .Where(s =>
                s.Status == StudentStatus.Active &&
                s.StudentPrograms.Any(sp => sp.IsActive && sp.Program.Code == "BM-L") &&
                !existingCeng102StudentIdsInTerm.Contains(s.Id))
            .OrderBy(s => s.StudentNumber)
            .Take(25)
            .ToListAsync();

        var selectedStudentIds = selectedStudents.Select(s => s.Id).ToHashSet();
        var now = DateTime.UtcNow;

        foreach (var student in selectedStudents)
        {
            if (existingStudentIdsInSection.Contains(student.Id))
                continue;

            context.CourseRegistrations.Add(new CourseRegistration
            {
                StudentId = student.Id,
                CourseSectionId = section.Id,
                Status = RegistrationStatus.Active,
                Grade = null,
                IsRepeated = false,
                MandatoryAttendance = false,
                CourseWithdrawalCount = 0,
                EctsSnapshot = ceng102.Ects,
                CreditSnapshot = ceng102.NationalCredit,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        section.EnrolledCount = await context.CourseRegistrations
            .CountAsync(r => r.CourseSectionId == section.Id && selectedStudentIds.Contains(r.StudentId));
        section.EnrolledCount = Math.Max(section.EnrolledCount, selectedStudents.Count);

        await context.SaveChangesAsync();
    }
}
