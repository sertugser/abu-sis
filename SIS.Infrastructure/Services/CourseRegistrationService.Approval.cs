using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// CourseRegistrationService — danışman onay/red akışları.
/// GetPendingApprovalsAsync: bekleyen kayıtları getir.
/// Approve / Reject: toplu onay/red, audit log + öğrenci notification.
/// </summary>
public partial class CourseRegistrationService
{
    public async Task<List<PendingApprovalDto>> GetPendingApprovalsAsync(int advisorInstructorId)
    {
        var nowPending = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= nowPending && t.TermEnd >= nowPending);
        if (activeTerm == null)
            return new List<PendingApprovalDto>();

        // Find active students whose advisor is this instructor
        var advisorStudentIds = await _context.Students
            .Where(s => s.AdvisorId == advisorInstructorId && s.Status == StudentStatus.Active)
            .Select(s => s.Id)
            .ToListAsync();

        if (!advisorStudentIds.Any())
            return new List<PendingApprovalDto>();

        // Get their PendingAdvisorApproval registrations for the active term
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Where(cr => advisorStudentIds.Contains(cr.StudentId)
                && cr.CourseSection.TermId == activeTerm.Id
                && cr.Status == RegistrationStatus.PendingAdvisorApproval)
            .ToListAsync();

        if (!registrations.Any())
            return new List<PendingApprovalDto>();

        // Load student user names
        var studentIds = registrations.Select(r => r.StudentId).Distinct().ToList();
        var students = await _context.Students
            .Where(s => studentIds.Contains(s.Id))
            .ToListAsync();

        var studentUserIds = students.Select(s => s.UserId).Distinct().ToList();

        // Load instructor user ids from the sections
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var allUserIds = studentUserIds.Union(instructorUserIds).ToList();
        var users = await _context.Users
            .Where(u => allUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var studentDict = students.ToDictionary(s => s.Id);

        // Load SectionSchedule records for schedule display and conflict checks
        var pendingSectionIds = registrations.Select(r => r.CourseSectionId).Distinct().ToList();
        var pendingSectionSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => pendingSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync();
        var pendingScheduleMap = pendingSectionSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group by student, build PendingApprovalDto
        var result = registrations
            .GroupBy(r => r.StudentId)
            .Select(g =>
            {
                var student = studentDict[g.Key];
                users.TryGetValue(student.UserId, out var studentName);

                var regItems = g.Select(r =>
                {
                    var instructor = r.CourseSection.Instructor;
                    var instructorName = string.Empty;
                    if (instructor != null && users.TryGetValue(instructor.UserId, out var iName))
                        instructorName = $"{instructor.Title} {iName}".Trim();

                    var sectionScheds = pendingScheduleMap.GetValueOrDefault(r.CourseSectionId)
                        ?? new List<Domain.Entities.SectionSchedule>();

                    return new PendingRegistrationItemDto
                    {
                        RegistrationId = r.Id,
                        CourseSectionId = r.CourseSectionId,
                        CourseCode = r.CourseSection.Course.Code,
                        CourseName = r.CourseSection.Course.Name,
                        Ects = r.CourseSection.Course.Ects,
                        Schedule = ScheduleHelper.FormatSchedule(sectionScheds),
                        InstructorName = instructorName,
                        IsRepeated = r.IsRepeated,
                        Status = r.Status.ToString()
                    };
                }).ToList();

                // Compute HasScheduleConflict between each pair of pending registrations
                var scheduleSlots = regItems
                    .Select(r => new {
                        Item = r,
                        Slots = ScheduleHelper.ToTimeBlocks(
                            pendingScheduleMap.GetValueOrDefault(r.CourseSectionId)
                            ?? new List<Domain.Entities.SectionSchedule>())
                    })
                    .ToList();

                for (int i = 0; i < scheduleSlots.Count; i++)
                {
                    for (int j = i + 1; j < scheduleSlots.Count; j++)
                    {
                        if (RegistrationRulePolicy.CheckScheduleOverlap(scheduleSlots[i].Slots, scheduleSlots[j].Slots) != ConflictOverlapType.None)
                        {
                            scheduleSlots[i].Item.HasScheduleConflict = true;
                            scheduleSlots[j].Item.HasScheduleConflict = true;
                        }
                    }
                }

                return new PendingApprovalDto
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentNumber,
                    StudentName = studentName ?? string.Empty,
                    Cgpa = student.Cgpa,
                    Sgpa = student.Sgpa,
                    CompletedSemesters = student.CompletedSemesters,
                    IsScholarshipStudent = student.IsScholarshipStudent,
                    Registrations = regItems,
                    TotalEcts = regItems.Sum(r => r.Ects),
                    EctsLimit = RegistrationRulePolicy.GetEctsLimit(student.Cgpa),
                    SubmittedAt = g.Max(r => r.UpdatedAt ?? r.CreatedAt)
                };
            })
            .ToList();

        // Alttan dersler, müfredat özeti ve plan durumu ekle
        foreach (var dto in result)
        {
            var student = studentDict[dto.StudentId];

            // Plan durumu
            var plan = await _context.RegistrationPlans
                .FirstOrDefaultAsync(rp => rp.StudentId == dto.StudentId && rp.TermId == activeTerm.Id);
            dto.PlanStatus = plan?.Status.ToString() ?? "N/A";

            // Alttan dersler (FF/NA/U)
            var failedRegs = await _context.CourseRegistrations
                .Include(cr => cr.CourseSection).ThenInclude(cs => cs.Course)
                .Where(cr => cr.StudentId == dto.StudentId
                    && cr.Grade != null
                    && (cr.Grade == GradeCode.FF || cr.Grade == GradeCode.NA || cr.Grade == GradeCode.U)
                    && cr.Status != RegistrationStatus.Dropped)
                .ToListAsync();

            var selectedCourseIds = dto.Registrations.Select(r =>
                registrations.First(reg => reg.Id == r.RegistrationId).CourseSection.CourseId).ToHashSet();

            dto.FailedCourses = failedRegs
                .GroupBy(r => r.CourseSection.CourseId)
                .Select(g => g.OrderByDescending(r => r.Id).First())
                .Select(r => new FailedCourseDto
                {
                    CourseCode = r.CourseSection.Course.Code,
                    CourseName = r.CourseSection.Course.Name,
                    Grade = r.Grade!.Value.ToString(),
                    IsSelectedInPlan = selectedCourseIds.Contains(r.CourseSection.CourseId)
                }).ToList();

            // Müfredat özeti
            var mainProgram = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
            if (mainProgram != null)
            {
                var curriculumCourses = await _context.CurriculumCourses
                    .Include(cc => cc.Course)
                    .Where(cc => cc.CurriculumId == mainProgram.CurriculumId)
                    .ToListAsync();

                var passedCourseIds = await _context.CourseRegistrations
                    .Include(cr => cr.CourseSection)
                    .Where(cr => cr.StudentId == dto.StudentId && RegistrationRulePolicy.IsPassedGrade(cr.Grade))
                    .Select(cr => cr.CourseSection.CourseId)
                    .Distinct()
                    .ToListAsync();

                var passedSet = passedCourseIds.ToHashSet();

                dto.CurriculumTotalCourses = curriculumCourses.Count;
                dto.CurriculumCompletedCourses = curriculumCourses.Count(cc => passedSet.Contains(cc.CourseId));
                dto.CurriculumTotalEcts = curriculumCourses.Sum(cc => cc.Course.Ects);
                dto.CurriculumCompletedEcts = curriculumCourses.Where(cc => passedSet.Contains(cc.CourseId)).Sum(cc => cc.Course.Ects);
            }
        }

        var calculationMethod = await _config.GetStringAsync("Instructor.AdvisingCalculationMethod", "standard");
        return string.Equals(calculationMethod, "load-based", StringComparison.OrdinalIgnoreCase)
            ? result
                .OrderByDescending(dto => dto.EctsLimit > 0 ? (decimal)dto.TotalEcts / dto.EctsLimit : 0m)
                .ThenByDescending(dto => dto.FailedCourses.Count)
                .ThenBy(dto => dto.Cgpa)
                .ThenBy(dto => dto.StudentName)
                .ToList()
            : result
                .OrderBy(dto => dto.StudentName)
                .ToList();
    }

    public async Task<ApprovalResponseDto> ApproveStudentRegistrationsAsync(int advisorInstructorId, ApprovalActionDto dto)
    {
        // Verify the student's advisor is this instructor
        var student = await _context.Students.FindAsync(dto.StudentId);
        if (student == null || student.AdvisorId != advisorInstructorId)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = "Bu ogrencinin danismani degilsiniz veya ogrenci bulunamadi.",
                AffectedRegistrations = 0
            };
        }

        var nowApprove = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= nowApprove && t.TermEnd >= nowApprove);
        if (activeTerm == null)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = "Aktif donem bulunamadi.",
                AffectedRegistrations = 0
            };
        }

        // Guard: dönem RegistrationOpen veya AddDrop olmalı
        if (!activeTerm.IsRegistrationOpen(_clock.UtcNow) && !activeTerm.IsAddDropOpen(_clock.UtcNow))
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = $"Danışman onayı sadece kayıt veya ekleme/bırakma döneminde yapılabilir.",
                AffectedRegistrations = 0
            };
        }

        var approvalControlCode = await ResolveAdvisorApprovalControlCodeAsync(dto.StudentId, activeTerm.Id, nowApprove);
        var approvalGate = await ValidateModuleAsync(
            approvalControlCode,
            activeTerm.Id,
            "WebModuleAdvisorApprovalClosed",
            "Danışman onay işlemleri şu anda kullanılamıyor.");
        if (approvalGate is not null)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = approvalGate.Errors.First().Message,
                AffectedRegistrations = 0
            };
        }

        // Find PendingAdvisorApproval registrations for this student in active term
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == dto.StudentId
                && cr.CourseSection.TermId == activeTerm.Id
                && cr.Status == RegistrationStatus.PendingAdvisorApproval)
            .ToListAsync();

        foreach (var reg in registrations)
        {
            reg.Status = RegistrationStatus.Active;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        // Update RegistrationPlan status to Approved
        var plan = await _context.RegistrationPlans
            .FirstOrDefaultAsync(rp => rp.StudentId == dto.StudentId && rp.TermId == activeTerm.Id);
        if (plan != null)
        {
            plan.Status = RegistrationPlanStatus.Approved;
            plan.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return new ApprovalResponseDto
        {
            Success = true,
            Message = $"{registrations.Count} kayit onaylandi.",
            AffectedRegistrations = registrations.Count
        };
    }

    public async Task<ApprovalResponseDto> RejectStudentRegistrationsAsync(int advisorInstructorId, ApprovalActionDto dto)
    {
        // Verify the student's advisor is this instructor
        var student = await _context.Students.FindAsync(dto.StudentId);
        if (student == null || student.AdvisorId != advisorInstructorId)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = "Bu ogrencinin danismani degilsiniz veya ogrenci bulunamadi.",
                AffectedRegistrations = 0
            };
        }

        var nowReject = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= nowReject && t.TermEnd >= nowReject);
        if (activeTerm == null)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = "Aktif donem bulunamadi.",
                AffectedRegistrations = 0
            };
        }

        var rejectControlCode = await ResolveAdvisorApprovalControlCodeAsync(dto.StudentId, activeTerm.Id, nowReject);
        var rejectGate = await ValidateModuleAsync(
            rejectControlCode,
            activeTerm.Id,
            "WebModuleAdvisorApprovalClosed",
            "Danışman onay işlemleri şu anda kullanılamıyor.");
        if (rejectGate is not null)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = rejectGate.Errors.First().Message,
                AffectedRegistrations = 0
            };
        }

        // Find PendingAdvisorApproval registrations (Include CourseSection for EnrolledCount)
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == dto.StudentId
                && cr.CourseSection.TermId == activeTerm.Id
                && cr.Status == RegistrationStatus.PendingAdvisorApproval)
            .ToListAsync();

        foreach (var reg in registrations)
        {
            reg.Status = RegistrationStatus.Rejected;
            reg.AdvisorNote = dto.Note;
            reg.UpdatedAt = DateTime.UtcNow;
            reg.CourseSection.EnrolledCount = Math.Max(0, reg.CourseSection.EnrolledCount - 1);
        }

        // Update RegistrationPlan status to Rejected with advisor note
        var plan = await _context.RegistrationPlans
            .FirstOrDefaultAsync(rp => rp.StudentId == dto.StudentId && rp.TermId == activeTerm.Id);
        if (plan != null)
        {
            plan.Status = RegistrationPlanStatus.Rejected;
            plan.AdvisorNote = dto.Note;
            plan.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return new ApprovalResponseDto
        {
            Success = true,
            Message = $"{registrations.Count} kayit reddedildi.",
            AffectedRegistrations = registrations.Count
        };
    }
}
