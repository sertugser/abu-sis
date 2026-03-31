using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class RegistrationValidationService : IRegistrationValidationService
{
    private readonly AppDbContext _context;

    public RegistrationValidationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ValidationResultDto> ValidateRegistrationAsync(int studentId, List<int> courseSectionIds)
    {
        var result = new ValidationResultDto { IsValid = true };
        var errors = new List<ValidationError>();

        // Load student with programs
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
        {
            errors.Add(new ValidationError { Rule = "StudentExists", Message = "Ogrenci bulunamadi." });
            result.IsValid = false;
            result.Errors = errors;
            return result;
        }

        // Load requested sections with courses and prerequisites
        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
                .ThenInclude(c => c.Prerequisites)
            .Include(cs => cs.Registrations)
            .Where(cs => courseSectionIds.Contains(cs.Id))
            .ToListAsync();

        // Load active term
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.IsActive);

        // Load student's existing active registrations for this term
        var existingRegistrations = activeTerm == null
            ? new List<Domain.Entities.CourseRegistration>()
            : await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                    .ThenInclude(cs => cs.Course)
                .Where(cr => cr.StudentId == studentId
                    && cr.CourseSection.TermId == activeTerm.Id
                    && (cr.Status == RegistrationStatus.Active || cr.Status == RegistrationStatus.PendingAdvisorApproval))
                .ToListAsync();

        // Load student's all past registrations (for prerequisite and grade checks)
        var allPastRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId && cr.Grade != null)
            .ToListAsync();

        // === CHECK 1: Student Status (FR-13/FR-14) ===
        if (student.Status != StudentStatus.Active)
        {
            errors.Add(new ValidationError
            {
                Rule = "StudentStatus",
                Message = $"Ogrenci durumu '{student.Status}' oldugu icin kayit yapilamaz. Sadece 'Active' durumundaki ogrenciler kayit yapabilir."
            });
        }

        // === CHECK 2: Financial Eligibility (FR-49) ===
        if (student.HasFinancialBlock)
        {
            errors.Add(new ValidationError
            {
                Rule = "FinancialEligibility",
                Message = "Ogrencinin odenmemis borcu bulunmaktadir. Borc temizlenene kadar kayit yapilamaz."
            });
        }

        // === CHECK 3: Registration Period (FR-47) ===
        if (activeTerm == null)
        {
            errors.Add(new ValidationError
            {
                Rule = "RegistrationPeriod",
                Message = "Aktif akademik donem bulunamadi."
            });
        }
        else if (activeTerm.Status != TermStatus.RegistrationOpen && activeTerm.Status != TermStatus.AddDrop)
        {
            errors.Add(new ValidationError
            {
                Rule = "RegistrationPeriod",
                Message = $"Kayıt dönemi açık değil. Dönem durumu: {activeTerm.Status}"
            });
        }

        // If blocking errors so far, return early
        if (errors.Any())
        {
            result.IsValid = false;
            result.Errors = errors;
            return result;
        }

        // Build set of currently failed course IDs (for use in multiple checks)
        var failedCourseIds = allPastRegistrations
            .Where(r => RegistrationRulePolicy.IsFailedGrade(r.Grade))
            .Select(r => r.CourseSection.Course.Id)
            .Distinct()
            .ToList();

        var passedCourseIds = allPastRegistrations
            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
            .Select(r => r.CourseSection.Course.Id)
            .Distinct()
            .ToList();

        var currentlyFailedCourseIds = failedCourseIds.Except(passedCourseIds).ToList();

        // Per-section checks
        foreach (var section in sections)
        {
            var course = section.Course;

            // === CHECK 4: Prerequisite Check (FR-29/FR-45) ===
            // Skip if student already passed this course (retake for grade improvement)
            if (course.Prerequisites.Any() && !passedCourseIds.Contains(course.Id))
            {
                foreach (var prereq in course.Prerequisites)
                {
                    var passedPrereq = allPastRegistrations.Any(r =>
                        r.CourseSection.Course.Id == prereq.RequiredCourseId &&
                        RegistrationRulePolicy.IsPassedGrade(r.Grade));

                    // Also check course equivalencies
                    if (!passedPrereq)
                    {
                        var equivalencies = await _context.CourseEquivalencies
                            .Where(ce => ce.CourseId == prereq.RequiredCourseId || ce.EquivalentCourseId == prereq.RequiredCourseId)
                            .ToListAsync();

                        var equivalentCourseIds = equivalencies
                            .Select(e => e.CourseId == prereq.RequiredCourseId ? e.EquivalentCourseId : e.CourseId)
                            .ToList();

                        passedPrereq = allPastRegistrations.Any(r =>
                            equivalentCourseIds.Contains(r.CourseSection.Course.Id) &&
                            RegistrationRulePolicy.IsPassedGrade(r.Grade));
                    }

                    if (!passedPrereq)
                    {
                        var reqCourse = await _context.Courses.FindAsync(prereq.RequiredCourseId);
                        errors.Add(new ValidationError
                        {
                            Rule = "Prerequisite",
                            Message = $"'{course.Code} {course.Name}' dersi icin on kosul '{reqCourse?.Code} {reqCourse?.Name}' tamamlanmamis.",
                            CourseSectionId = section.Id
                        });
                    }
                }
            }

            // === CHECK 9: Capacity Check (FR-40) ===
            if (section.EnrolledCount >= section.Capacity)
            {
                errors.Add(new ValidationError
                {
                    Rule = "Capacity",
                    Message = $"'{course.Code} {course.Name}' dersinin kontenjani dolu ({section.EnrolledCount}/{section.Capacity}).",
                    CourseSectionId = section.Id
                });
            }

            // === CHECK 10: Duplicate Registration (FR-50) ===
            var alreadyRegistered = existingRegistrations.Any(r =>
                r.CourseSection.Course.Id == course.Id);

            if (alreadyRegistered)
            {
                errors.Add(new ValidationError
                {
                    Rule = "DuplicateRegistration",
                    Message = $"'{course.Code} {course.Name}' dersine zaten kayitlisiniz.",
                    CourseSectionId = section.Id
                });
            }
        }

        // === CHECK 5: Failed Courses Priority (FR-55) ===
        if (currentlyFailedCourseIds.Any())
        {
            var registeredForFailedCourses = sections
                .Where(s => currentlyFailedCourseIds.Contains(s.Course.Id))
                .Select(s => s.Course.Id)
                .ToList();

            var unregisteredFailedCourses = currentlyFailedCourseIds.Except(registeredForFailedCourses).ToList();

            if (unregisteredFailedCourses.Any())
            {
                var availableSections = await _context.CourseSections
                    .Where(cs => cs.TermId == activeTerm!.Id && cs.IsActive && unregisteredFailedCourses.Contains(cs.CourseId))
                    .Select(cs => cs.CourseId)
                    .Distinct()
                    .ToListAsync();

                var missingFailedCourses = unregisteredFailedCourses.Intersect(availableSections).ToList();

                if (missingFailedCourses.Any())
                {
                    foreach (var courseId in missingFailedCourses)
                    {
                        var course = await _context.Courses.FindAsync(courseId);
                        errors.Add(new ValidationError
                        {
                            Rule = "FailedCoursePriority",
                            Message = $"'{course?.Code} {course?.Name}' dersinden basarisiz oldunuz. Yeni ders almadan once bu dersi secmelisiniz."
                        });
                    }
                }
            }
        }

        // === CHECK 6: ECTS Cap (FR-56 + FR-56A) ===
        var requestedEcts = sections.Sum(s => s.Course.Ects);
        var existingEcts = existingRegistrations.Sum(r => r.CourseSection.Course.Ects);
        var totalEcts = requestedEcts + existingEcts;

        var ectsLimit = RegistrationRulePolicy.GetEctsLimit(student.Cgpa);

        if (totalEcts > ectsLimit)
        {
            errors.Add(new ValidationError
            {
                Rule = "ECTSCap",
                Message = $"ECTS limiti asiliyor. Mevcut: {existingEcts}, Istenen: {requestedEcts}, Toplam: {totalEcts}, Limit: {ectsLimit} (CGPA: {student.Cgpa:F2})"
            });
        }

        // === CHECK 7: CGPA Restriction (FR-56B) ===
        if (RegistrationRulePolicy.HasLowCgpaRestriction(student.Cgpa, student.CompletedSemesters))
        {
            var studentProgram = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);

            if (studentProgram != null)
            {
                var curriculumCourses = await _context.CurriculumCourses
                    .Include(cc => cc.Course)
                    .Where(cc => cc.CurriculumId == studentProgram.CurriculumId)
                    .ToListAsync();

                foreach (var section in sections)
                {
                    var courseId = section.Course.Id;

                    // Check if it's a failed course (FF/NA)
                    var isFailed = currentlyFailedCourseIds.Contains(courseId);

                    // Check if it's a DD/DC course for grade improvement
                    var isDDDC = allPastRegistrations.Any(r =>
                        r.CourseSection.Course.Id == courseId &&
                        (r.Grade == GradeCode.DD || r.Grade == GradeCode.DC));

                    var currCourse = curriculumCourses.FirstOrDefault(cc => cc.CourseId == courseId);

                    // Check if it's a non-departmental course (different department)
                    var isAllowed = RegistrationRulePolicy.IsAllowedUnderLowCgpaRestriction(
                        section.Course,
                        currCourse?.SuggestedSemester,
                        student.CompletedSemesters,
                        studentProgram.Program?.DepartmentId,
                        isFailed,
                        isDDDC);

                    if (!isAllowed)
                    {
                        errors.Add(new ValidationError
                        {
                            Rule = "CGPARestriction",
                            Message = $"CGPA {student.Cgpa:F2} ile sinifta kaldiniz. '{section.Course.Code} {section.Course.Name}' dersini alamazsiniz. Sadece basarisiz dersler, DD/DC not iyilestirme, bulundugunuz sinif ve alti dersler veya bolum disi secmeliler alinabilir.",
                            CourseSectionId = section.Id
                        });
                    }
                }
            }
        }

        // === CHECK 8: Schedule Conflict (FR-41) ===
        for (int i = 0; i < sections.Count; i++)
        {
            var newSection = sections[i];
            var newSchedule = JsonSerializer.Deserialize<List<ScheduleSlot>>(newSection.ScheduleJson) ?? new();

            // Check against existing registrations
            foreach (var existing in existingRegistrations)
            {
                var existingSchedule = JsonSerializer.Deserialize<List<ScheduleSlot>>(existing.CourseSection.ScheduleJson) ?? new();

                var overlapType = RegistrationRulePolicy.CheckScheduleOverlap(newSchedule, existingSchedule);

                if (overlapType != ConflictOverlapType.None)
                {
                    bool isNewCourseRepeated = currentlyFailedCourseIds.Contains(newSection.Course.Id);
                    bool isExistingCourseRepeated = currentlyFailedCourseIds.Contains(existing.CourseSection.Course.Id);
                    bool isRepeat = isNewCourseRepeated || isExistingCourseRepeated;

                    if (overlapType == ConflictOverlapType.Full && !isRepeat)
                    {
                        // FR-41a: Full overlap for first-time courses — CATEGORICALLY BLOCKED
                        errors.Add(new ValidationError
                        {
                            Rule = "ScheduleConflict_FullBlock",
                            Message = $"'{newSection.Course.Code}' ve '{existing.CourseSection.Course.Code}' dersleri tam cakisiyor. Ilk kez alinan derslerde tam cakisma kesinlikle engellenir.",
                            CourseSectionId = newSection.Id
                        });
                    }
                    else
                    {
                        // FR-41b/c: Partial overlap or repeat course overlap — needs advisor + dept manager approval
                        string conflictType = overlapType == ConflictOverlapType.Full ? "tam" : "kismi";
                        string repeatNote = isRepeat ? " (tekrar ders)" : "";
                        errors.Add(new ValidationError
                        {
                            Rule = "ScheduleConflict_NeedsApproval",
                            Message = $"'{newSection.Course.Code}' ve '{existing.CourseSection.Course.Code}' dersleri arasinda {conflictType} cakisma{repeatNote} var. Danisman ve Bolum Muduru onayi gereklidir.",
                            CourseSectionId = newSection.Id
                        });
                    }
                }
            }

            // Check against other new sections being registered simultaneously
            for (int j = i + 1; j < sections.Count; j++)
            {
                var otherSection = sections[j];
                var otherSchedule = JsonSerializer.Deserialize<List<ScheduleSlot>>(otherSection.ScheduleJson) ?? new();

                var overlapType = RegistrationRulePolicy.CheckScheduleOverlap(newSchedule, otherSchedule);
                if (overlapType != ConflictOverlapType.None)
                {
                    bool isRepeat = currentlyFailedCourseIds.Contains(newSection.Course.Id) || currentlyFailedCourseIds.Contains(otherSection.Course.Id);

                    if (overlapType == ConflictOverlapType.Full && !isRepeat)
                    {
                        errors.Add(new ValidationError
                        {
                            Rule = "ScheduleConflict_FullBlock",
                            Message = $"'{newSection.Course.Code}' ve '{otherSection.Course.Code}' dersleri tam cakisiyor.",
                            CourseSectionId = newSection.Id
                        });
                    }
                    else
                    {
                        string conflictType = overlapType == ConflictOverlapType.Full ? "tam" : "kismi";
                        errors.Add(new ValidationError
                        {
                            Rule = "ScheduleConflict_NeedsApproval",
                            Message = $"'{newSection.Course.Code}' ve '{otherSection.Course.Code}' dersleri arasinda {conflictType} cakisma var. Onay gereklidir.",
                            CourseSectionId = newSection.Id
                        });
                    }
                }
            }
        }

        // "NeedsApproval" errors should NOT block the registration, only warn
        result.IsValid = !errors.Any(e => e.Rule != "ScheduleConflict_NeedsApproval");
        result.Errors = errors;
        return result;
    }

}
