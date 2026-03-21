using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedRegistrationsAsync(AppDbContext context)
    {
        if (context.CourseRegistrations.Any()) return;

        var students = context.Students
            .Include(s => s.StudentPrograms)
            .ToList();
        var allCurrCourses = context.Set<CurriculumCourse>()
            .Include(cc => cc.Course)
            .ToList();
        var terms = context.AcademicTerms
            .OrderBy(t => t.Year).ThenBy(t => t.Semester)
            .ToList();
        var instructors = context.Instructors.ToList();
        var curricula = context.Curricula.ToList();
        var activeTerm = terms.First(t => t.IsActive);

        foreach (var student in students)
        {
            var sp = student.StudentPrograms.FirstOrDefault();
            if (sp == null) continue;

            var curriculum = curricula.First(c => c.Id == sp.CurriculumId);
            var myCourses = allCurrCourses
                .Where(cc => cc.CurriculumId == curriculum.Id && cc.IsCompulsory && cc.SuggestedSemester > 0)
                .OrderBy(cc => cc.SuggestedSemester)
                .ToList();

            var deptId = curriculum.Program?.DepartmentId ?? instructors.First().DepartmentId;
            var deptInstructors = instructors.Where(i => i.DepartmentId == deptId).ToList();
            if (!deptInstructors.Any()) deptInstructors = instructors.Take(1).ToList();

            var targetCgpa = student.Cgpa;
            var completedSemesters = student.CompletedSemesters;

            // Determine grade pattern based on GPA category
            var gradePattern = GetGradePattern(targetCgpa);
            int gradeIndex = 0;

            decimal totalQualityPoints = 0;
            int totalGpaEcts = 0;
            int currSemester = 0;

            // Past semester terms (chronological, excluding active)
            var pastTerms = terms.Where(t => !t.IsActive).OrderBy(t => t.Year).ThenBy(t => t.Semester).ToList();

            var entryYear = sp.EnrollmentDate.Year;
            var studentTerms = pastTerms
                .Where(t => t.Year >= entryYear || (t.Year == entryYear - 1 && t.Semester == SemesterType.Spring))
                .Take(completedSemesters)
                .ToList();

            // Instructor index for deterministic assignment
            int instrIdx = 0;

            foreach (var term in studentTerms)
            {
                currSemester++;
                var semCourses = myCourses.Where(cc => cc.SuggestedSemester == currSemester).ToList();

                foreach (var cc in semCourses)
                {
                    GradeCode grade;
                    if (cc.Course.IsNonGpa)
                    {
                        grade = GradeCode.S;
                    }
                    else
                    {
                        grade = gradePattern[gradeIndex % gradePattern.Length];
                        gradeIndex++;

                        var pt = GradePointMap.GetPoint(grade) ?? 0;
                        totalQualityPoints += pt * cc.Course.Ects;
                        totalGpaEcts += cc.Course.Ects;
                    }

                    var section = CreateSectionDeterministic(context, cc.Course.Id, term.Id, deptInstructors, ref instrIdx);
                    await context.SaveChangesAsync();

                    context.CourseRegistrations.Add(new CourseRegistration
                    {
                        StudentId = student.Id,
                        CourseSectionId = section.Id,
                        Status = RegistrationStatus.Completed,
                        Grade = grade,
                        IsRepeated = false,
                        MandatoryAttendance = false,
                        CourseWithdrawalCount = 0,
                        CreatedAt = term.RegistrationStart,
                        UpdatedAt = term.TermEnd
                    });
                }
            }

            // Active term registrations (no grades)
            var nextSem = currSemester + 1;
            var activeCourses = myCourses.Where(cc => cc.SuggestedSemester == nextSem).ToList();
            if (!activeCourses.Any() && myCourses.Any())
                activeCourses = myCourses.Where(cc => cc.SuggestedSemester == currSemester).Take(3).ToList();

            foreach (var cc in activeCourses)
            {
                var section = CreateSectionDeterministic(context, cc.Course.Id, activeTerm.Id, deptInstructors, ref instrIdx, isActive: true);
                await context.SaveChangesAsync();

                context.CourseRegistrations.Add(new CourseRegistration
                {
                    StudentId = student.Id,
                    CourseSectionId = section.Id,
                    Status = RegistrationStatus.Active,
                    Grade = null,
                    IsRepeated = false,
                    MandatoryAttendance = false,
                    CourseWithdrawalCount = 0,
                    CreatedAt = activeTerm.RegistrationStart
                });
            }

            // Update student GPA from actual registrations
            if (totalGpaEcts > 0)
            {
                var calcCgpa = Math.Round(totalQualityPoints / totalGpaEcts, 2);
                student.Cgpa = calcCgpa;
                student.Sgpa = calcCgpa;
            }

            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gerçekçi not dağılımları — her pattern 12 not içerir, AKTS ağırlıklı ortalama
    /// hedef aralığa düşer. Farklı derslerin AKTS'leri farklı olduğu için öğrenciler
    /// arasında doğal varyasyon oluşur.
    ///
    /// Başarılı  (>2.00): AA,BA,BB,CB,BB,AA,BA,BB,CB,BA,BB,AA  → ~3.29
    /// Sınırda (1.70–2.00): CC,DC,CB,CC,DC,DD,CC,DC,CC,CB,DC,CC → ~1.83
    /// Başarısız  (<1.70): DC,CC,DD,DC,CC,DD,DC,DC,DD,CC,DC,DD  → ~1.46
    /// </summary>
    private static GradeCode[] GetGradePattern(decimal targetCgpa) => targetCgpa switch
    {
        // AA(4)+BA(3.5)+BB(3)+CB(2.5)+BB(3)+AA(4)+BA(3.5)+BB(3)+CB(2.5)+BA(3.5)+BB(3)+AA(4) = 39.5 → 39.5/12 = 3.29
        > 2.00m =>
        [
            GradeCode.AA, GradeCode.BA, GradeCode.BB, GradeCode.CB,
            GradeCode.BB, GradeCode.AA, GradeCode.BA, GradeCode.BB,
            GradeCode.CB, GradeCode.BA, GradeCode.BB, GradeCode.AA
        ],
        // CC(2)+DC(1.5)+CB(2.5)+CC(2)+DC(1.5)+DD(1)+CC(2)+DC(1.5)+CC(2)+CB(2.5)+DC(1.5)+CC(2) = 22 → 22/12 = 1.83
        >= 1.70m =>
        [
            GradeCode.CC, GradeCode.DC, GradeCode.CB, GradeCode.CC,
            GradeCode.DC, GradeCode.DD, GradeCode.CC, GradeCode.DC,
            GradeCode.CC, GradeCode.CB, GradeCode.DC, GradeCode.CC
        ],
        // DC(1.5)+CC(2)+DD(1)+DC(1.5)+CC(2)+DD(1)+DC(1.5)+DC(1.5)+DD(1)+CC(2)+DC(1.5)+DD(1) = 17.5 → 17.5/12 = 1.46
        _ =>
        [
            GradeCode.DC, GradeCode.CC, GradeCode.DD, GradeCode.DC,
            GradeCode.CC, GradeCode.DD, GradeCode.DC, GradeCode.DC,
            GradeCode.DD, GradeCode.CC, GradeCode.DC, GradeCode.DD
        ],
    };

    private static CourseSection CreateSectionDeterministic(
        AppDbContext context, int courseId, int termId,
        List<Instructor> instructors, ref int instrIdx, bool isActive = false)
    {
        // Reuse existing section if available
        var existing = context.CourseSections.Local
            .FirstOrDefault(s => s.CourseId == courseId && s.TermId == termId);
        if (existing != null)
        {
            existing.EnrolledCount++;
            return existing;
        }

        var dbExisting = context.CourseSections
            .FirstOrDefault(s => s.CourseId == courseId && s.TermId == termId);
        if (dbExisting != null)
        {
            dbExisting.EnrolledCount++;
            return dbExisting;
        }

        var instructor = instructors[instrIdx % instructors.Count];
        instrIdx++;

        var section = new CourseSection
        {
            CourseId = courseId,
            TermId = termId,
            InstructorId = instructor.Id,
            Capacity = 40,
            EnrolledCount = 1,
            IsActive = isActive,
            IsFinalized = false,
            ScheduleJson = "[]"
        };
        context.CourseSections.Add(section);
        return section;
    }
}
