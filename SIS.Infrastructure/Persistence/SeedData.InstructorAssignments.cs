using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    // Target enrollment per section: BIO101=35, CENG101=17, CENG102=40, CENG213=5
    private static readonly (string CourseCode, int TargetEnrollment)[] SectionEnrollments =
    [
        ("BIO101",  35),
        ("CENG101", 17),
        ("CENG102", 40),
        ("CENG213",  5),
    ];

    public static async Task AssignInstructorCoursesAsync(AppDbContext context)
    {
        // Find the instructor by email
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "ender.sevinc@ankarabilim.edu.tr");

        if (user is null) return;

        var instructor = await context.Instructors
            .FirstOrDefaultAsync(i => i.UserId == user.Id);

        if (instructor is null) return;

        // Get the active term
        var activeTerm = await context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive);

        if (activeTerm is null) return;

        // Get the BM department
        var bmDept = await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == "BM");

        if (bmDept is null) return;

        // --- Step 1: Create sections if not already present ---
        var existingSections = await context.CourseSections
            .Where(cs => cs.TermId == activeTerm.Id && cs.InstructorId == instructor.Id)
            .ToListAsync();

        if (existingSections.Count == 0)
        {
            var courseCodes = SectionEnrollments.Select(e => e.CourseCode).ToList();

            var bmCourses = await context.Courses
                .AsNoTracking()
                .Where(c => c.DepartmentId == bmDept.Id && courseCodes.Contains(c.Code))
                .ToListAsync();

            if (bmCourses.Count == 0) return;

            var classroom = await context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync();

            foreach (var course in bmCourses)
            {
                context.CourseSections.Add(new CourseSection
                {
                    CourseId = course.Id,
                    TermId = activeTerm.Id,
                    InstructorId = instructor.Id,
                    Capacity = 40,
                    EnrolledCount = 0,
                    ClassroomId = classroom?.Id,
                    IsActive = true,
                    IsFinalized = false
                });
            }

            await context.SaveChangesAsync();

            existingSections = await context.CourseSections
                .Where(cs => cs.TermId == activeTerm.Id && cs.InstructorId == instructor.Id)
                .ToListAsync();
        }

        // --- Step 2: Enroll students into sections ---
        var sections = await context.CourseSections
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == activeTerm.Id && cs.InstructorId == instructor.Id)
            .ToListAsync();

        // Check if registrations already exist for these sections
        var sectionIds = sections.Select(s => s.Id).ToList();
        var hasRegistrations = await context.CourseRegistrations
            .AnyAsync(cr => sectionIds.Contains(cr.CourseSectionId));

        if (hasRegistrations) return;

        // Grab a pool of students to distribute
        var totalNeeded = SectionEnrollments.Sum(e => e.TargetEnrollment);
        var studentPool = await context.Students
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Take(totalNeeded)
            .Select(s => s.Id)
            .ToListAsync();

        if (studentPool.Count == 0) return;

        var studentIndex = 0;
        var now = DateTime.UtcNow;

        foreach (var (courseCode, target) in SectionEnrollments)
        {
            var section = sections.FirstOrDefault(s => s.Course.Code == courseCode);
            if (section is null) continue;

            var enrolled = 0;
            while (enrolled < target && studentIndex < studentPool.Count)
            {
                context.CourseRegistrations.Add(new CourseRegistration
                {
                    StudentId = studentPool[studentIndex],
                    CourseSectionId = section.Id,
                    Status = RegistrationStatus.Active,
                    Grade = null,
                    IsRepeated = false,
                    MandatoryAttendance = false,
                    CourseWithdrawalCount = 0,
                    CreatedAt = now
                });

                studentIndex++;
                enrolled++;
            }

            section.EnrolledCount = enrolled;
        }

        await context.SaveChangesAsync();
    }
}
