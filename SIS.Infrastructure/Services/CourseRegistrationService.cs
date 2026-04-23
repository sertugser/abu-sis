using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.Common.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class CourseRegistrationService : ICourseRegistrationService
{
    private readonly AppDbContext _context;
    private readonly IRegistrationValidationService _validationService;
    private readonly ISystemConfigProvider _config;
    private readonly IAdvisorConstraintService? _advisorConstraintService;
    private readonly IDateTimeProvider _clock;

    public CourseRegistrationService(
        AppDbContext context,
        IRegistrationValidationService validationService,
        IDateTimeProvider clock,
        ISystemConfigProvider? config = null,
        IAdvisorConstraintService? advisorConstraintService = null)
    {
        _context = context;
        _validationService = validationService;
        _clock = clock;
        _config = config ?? new NullSystemConfigProvider();
        _advisorConstraintService = advisorConstraintService;
    }

    public async Task<RegistrationResponseDto> AddToPlanAsync(int studentId, int courseSectionId)
    {
        var response = new RegistrationResponseDto();

        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == courseSectionId);

        if (section == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "SectionExists", Message = "Ders sectionu bulunamadi." }
                }
            };
            return response;
        }

        // Guard: kayıt dönemi kontrolü
        var term = await _context.AcademicTerms.AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == section.TermId);
        if (term != null && !term.IsRegistrationOpen(_clock.UtcNow) && !term.IsAddDropOpen(_clock.UtcNow))
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "RegistrationClosed", Message = $"Ders kayıt dönemi açık değil." }
                }
            };
            return response;
        }

        // Kapasite kontrolü: kontenjan dolu mu?
        // Madde 14: Cross-list grubundaysa toplam sayı grup bazında hesaplanır
        int effectiveEnrolled = section.EnrolledCount;
        if (section.CrossListGroupId.HasValue)
        {
            effectiveEnrolled = await _context.CourseSections
                .Where(cs => cs.CrossListGroupId == section.CrossListGroupId.Value)
                .SumAsync(cs => cs.EnrolledCount);
        }
        if (effectiveEnrolled >= section.Capacity)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "Capacity", Message = $"'{section.Course.Code} {section.Course.Name}' dersinin kontenjanı dolu ({effectiveEnrolled}/{section.Capacity})." }
                }
            };
            return response;
        }

        var advisorConstraintResult = await EvaluateAdvisorConstraintsAsync(studentId, new[]
        {
            new AdvisorConstraintSectionInfo
            {
                CourseSectionId = section.Id,
                CourseId = section.CourseId,
                TermId = section.TermId,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name
            }
        });

        if (!advisorConstraintResult.IsValid)
        {
            response.Success = false;
            response.ValidationResult = advisorConstraintResult;
            return response;
        }

        // Get or create RegistrationPlan for this student+term
        var plan = await _context.RegistrationPlans
            .FirstOrDefaultAsync(rp => rp.StudentId == studentId && rp.TermId == section.TermId);

        if (plan == null)
        {
            plan = new Domain.Entities.RegistrationPlan
            {
                StudentId = studentId,
                TermId = section.TermId,
                Status = RegistrationPlanStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            _context.RegistrationPlans.Add(plan);
            await _context.SaveChangesAsync(); // need ID before FK assignment
        }
        else if (plan.Status == RegistrationPlanStatus.Rejected)
        {
            // Rejected plan can be edited and resubmitted
            plan.Status = RegistrationPlanStatus.Draft;
            plan.UpdatedAt = DateTime.UtcNow;

            // B18: Önceki reddedilmiş kayıtları temizle — plan tazelendi.
            // Eski Rejected kayıtlar Dropped'a çevrilir (sayaç zaten RejectStudentRegistrations'da düşürülmüştü).
            var staleRejected = await _context.CourseRegistrations
                .Where(cr => cr.StudentId == studentId
                    && cr.RegistrationPlanId == plan.Id
                    && cr.Status == RegistrationStatus.Rejected)
                .ToListAsync();
            foreach (var stale in staleRejected)
            {
                stale.Status = RegistrationStatus.Dropped;
                stale.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Check if already in plan (same section)
        var existingForSection = await _context.CourseRegistrations
            .FirstOrDefaultAsync(cr => cr.StudentId == studentId
                && cr.CourseSectionId == courseSectionId
                && (cr.Status == RegistrationStatus.Draft
                    || cr.Status == RegistrationStatus.Active
                    || cr.Status == RegistrationStatus.PendingAdvisorApproval));

        if (existingForSection != null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "AlreadyInPlan", Message = "Bu ders zaten planda veya kayitli.", CourseSectionId = courseSectionId }
                }
            };
            return response;
        }

        // BUG FIX: Check if already in plan for the same COURSE but different section
        var existingForCourse = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .FirstOrDefaultAsync(cr => cr.StudentId == studentId
                && cr.CourseSection.CourseId == section.CourseId
                && cr.CourseSectionId != courseSectionId
                && (cr.Status == RegistrationStatus.Draft
                    || cr.Status == RegistrationStatus.Active
                    || cr.Status == RegistrationStatus.PendingAdvisorApproval));

        if (existingForCourse != null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "CourseAlreadySelected", Message = "Bu dersin baska bir sectionu zaten planda veya kayitli.", CourseSectionId = courseSectionId }
                }
            };
            return response;
        }

        // Madde 14: Cross-list çakışma kontrolü — aynı grup'taki başka bir section'a kayıtlı mı?
        if (section.CrossListGroupId.HasValue)
        {
            var existingInGroup = await _context.CourseRegistrations
                .Include(cr => cr.CourseSection).ThenInclude(cs => cs.Course)
                .FirstOrDefaultAsync(cr => cr.StudentId == studentId
                    && cr.CourseSectionId != courseSectionId
                    && cr.CourseSection.CrossListGroupId == section.CrossListGroupId.Value
                    && (cr.Status == RegistrationStatus.Draft
                        || cr.Status == RegistrationStatus.Active
                        || cr.Status == RegistrationStatus.PendingAdvisorApproval));
            if (existingInGroup != null)
            {
                response.Success = false;
                response.ValidationResult = new ValidationResultDto
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new() { Rule = "CrossListAlreadySelected", Message = $"'{existingInGroup.CourseSection.Course.Code}' bu dersin cross-list eşdeğeri, zaten planda. İki farklı koddan aynı ders alınamaz.", CourseSectionId = courseSectionId }
                    }
                };
                return response;
            }
        }

        // Prerequisite check — block if prerequisites are not met
        var prereqs = await _context.CoursePrerequisites
            .Where(p => p.CourseId == section.CourseId)
            .Select(p => p.RequiredCourseId)
            .ToListAsync();

        if (prereqs.Count > 0)
        {
            // Passed courses: grade is not FF/NA/U/W/I
            var passedCourseIds = await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                .Where(cr => cr.StudentId == studentId
                    && cr.Grade != null
                    && cr.Grade != GradeCode.FF && cr.Grade != GradeCode.NA
                    && cr.Grade != GradeCode.U && cr.Grade != GradeCode.W
                    && cr.Grade != GradeCode.I
                    && cr.Status != RegistrationStatus.Dropped)
                .Select(cr => cr.CourseSection.CourseId)
                .Distinct()
                .ToListAsync();

            var passedSet = passedCourseIds.ToHashSet();

            // Check equivalencies
            var equivMap = (await _context.CourseEquivalencies.ToListAsync())
                .GroupBy(e => e.CourseId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.EquivalentCourseId).ToHashSet());

            foreach (var reqId in prereqs)
            {
                var met = passedSet.Contains(reqId);
                if (!met && equivMap.TryGetValue(reqId, out var equivIds))
                    met = equivIds.Any(eid => passedSet.Contains(eid));

                if (!met)
                {
                    var reqCourse = await _context.Courses.FindAsync(reqId);
                    response.Success = false;
                    response.ValidationResult = new ValidationResultDto
                    {
                        IsValid = false,
                        Errors = new List<ValidationError>
                        {
                            new() { Rule = "PrerequisiteNotMet", Message = $"Onkosul karsilanmamistir: {reqCourse?.Code ?? "?"} dersi gecilmeli.", CourseSectionId = courseSectionId }
                        }
                    };
                    return response;
                }
            }
        }

        // BUG FIX: Check for previously dropped/withdrawn record (unique index on StudentId+CourseSectionId)
        var droppedRecord = await _context.CourseRegistrations
            .FirstOrDefaultAsync(cr => cr.StudentId == studentId
                && cr.CourseSectionId == courseSectionId
                && (cr.Status == RegistrationStatus.Dropped
                    || cr.Status == RegistrationStatus.Withdrawn));

        Domain.Entities.CourseRegistration registration;

        if (droppedRecord != null)
        {
            // Reactivate existing record instead of creating new (unique constraint)
            droppedRecord.Status = RegistrationStatus.Draft;
            droppedRecord.RegistrationPlanId = plan.Id;
            droppedRecord.UpdatedAt = DateTime.UtcNow;
            registration = droppedRecord;
        }
        else
        {
            // Create new CourseRegistration with Status = Draft (plan/soft hold - FR-44)
            registration = new Domain.Entities.CourseRegistration
            {
                StudentId = studentId,
                CourseSectionId = courseSectionId,
                RegistrationPlanId = plan.Id,
                Status = RegistrationStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            _context.CourseRegistrations.Add(registration);
        }

        // FR-56C: Mark as repeated if student already passed this course (grade improvement)
        registration.IsRepeated = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .AnyAsync(cr => cr.StudentId == studentId
                && cr.CourseSection.CourseId == section.CourseId
                && RegistrationRulePolicy.IsPassedGrade(cr.Grade));

        // Madde 6 tamamlama: AKTS snapshot + havuz atamasını çöz ve kayıt satırına yaz
        var (snapshotEcts, appliedPoolId) = await ResolveEctsSnapshotAsync(studentId, section.CourseId);
        registration.EctsSnapshot = snapshotEcts;
        registration.AppliedElectivePoolId = appliedPoolId;

        // === KRİTİK BÖLGE: Serializable transaction + fresh kapasite kontrolü + increment ===
        // Eş zamanlı kayıt çakışmasını önler (B1), kapasite kontrolünü atomik yapar (B3+B4).
        var useTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        await using var tx = useTransaction
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
            : null;
        try
        {
            // Fresh reload — başka bir öğrenci aynı anda koltuğu almış olabilir
            await _context.Entry(section).ReloadAsync();
            // Madde 14: Cross-list grubundaysa toplam sayı
            int freshEffective = section.EnrolledCount;
            if (section.CrossListGroupId.HasValue)
            {
                freshEffective = await _context.CourseSections
                    .Where(cs => cs.CrossListGroupId == section.CrossListGroupId.Value)
                    .SumAsync(cs => cs.EnrolledCount);
            }
            if (freshEffective >= section.Capacity)
            {
                if (tx != null) await tx.RollbackAsync();
                response.Success = false;
                response.ValidationResult = new ValidationResultDto
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new() { Rule = "Capacity", Message = $"'{section.Course.Code} {section.Course.Name}' dersinin kontenjanı bu esnada doldu ({freshEffective}/{section.Capacity})." }
                    }
                };
                return response;
            }

            // Soft hold: sayımı artır (FR-44)
            section.EnrolledCount++;
            await _context.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }

        response.Success = true;
        response.Registrations = new List<RegistrationItemDto>
        {
            new()
            {
                Id = registration.Id,
                CourseSectionId = section.Id,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name,
                Ects = section.Course.Ects,
                Status = registration.Status.ToString()
            }
        };
        if (advisorConstraintResult.Warnings.Any())
        {
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = true,
                Warnings = advisorConstraintResult.Warnings
            };
        }

        return response;
    }

    public async Task<RegistrationResponseDto> SubmitRegistrationAsync(int studentId, RegistrationSubmitDto dto)
    {
        var response = new RegistrationResponseDto();

        // Validate the registration
        var validationResult = await _validationService.ValidateRegistrationAsync(studentId, dto.CourseSectionIds);

        if (!validationResult.IsValid)
        {
            response.Success = false;
            response.ValidationResult = validationResult;
            return response;
        }

        var submittedSections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Where(cs => dto.CourseSectionIds.Contains(cs.Id))
            .ToListAsync();

        var advisorConstraintResult = await EvaluateAdvisorConstraintsAsync(
            studentId,
            submittedSections.Select(section => new AdvisorConstraintSectionInfo
            {
                CourseSectionId = section.Id,
                CourseId = section.CourseId,
                TermId = section.TermId,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name
            }));

        if (!advisorConstraintResult.IsValid)
        {
            response.Success = false;
            response.ValidationResult = advisorConstraintResult;
            return response;
        }

        validationResult.Warnings.AddRange(advisorConstraintResult.Warnings);

        // Check if student is scholarship (FR-52: skip payment confirmation)
        var student = await _context.Students.FindAsync(studentId);
        if (student == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "StudentExists", Message = "Ogrenci bulunamadi." }
                }
            };
            return response;
        }

        // Danışman onayı her zaman zorunlu — burslu öğrenci dahil (ADIM 11)
        var targetStatus = RegistrationStatus.PendingAdvisorApproval;

        // Load or create registrations for the submitted sections
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId
                && dto.CourseSectionIds.Contains(cr.CourseSectionId)
                && cr.Status == RegistrationStatus.Draft)
            .ToListAsync();

        // For sections that don't have a Draft registration yet, create them
        var existingCourseSectionIds = registrations.Select(r => r.CourseSectionId).ToHashSet();
        var missingSectionIds = dto.CourseSectionIds.Where(id => !existingCourseSectionIds.Contains(id)).ToList();

        if (missingSectionIds.Any())
        {
            var missingSections = await _context.CourseSections
                .Include(cs => cs.Course)
                .Where(cs => missingSectionIds.Contains(cs.Id))
                .ToListAsync();

            // Daha önce Dropped/Withdrawn olmuş ama unique constraint (Student+Section) nedeniyle
            // yeniden Add edilirse kırılacak kayıtları önceden yakala — bunları reactivate edeceğiz.
            var existingAny = await _context.CourseRegistrations
                .Where(cr => cr.StudentId == studentId && missingSectionIds.Contains(cr.CourseSectionId))
                .ToListAsync();
            var existingByDropped = existingAny
                .Where(cr => cr.Status == RegistrationStatus.Dropped || cr.Status == RegistrationStatus.Withdrawn)
                .ToDictionary(cr => cr.CourseSectionId);

            // === KRİTİK BÖLGE: Serializable transaction + ders başına fresh kapasite kontrolü + increment ===
            // Çift sayımı önler (B2), kapasite kontrolünü tüm kayıt noktalarında zorunlu hale getirir (B3).
            var useTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            await using var tx = useTransaction
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;
            try
            {
                foreach (var section in missingSections)
                {
                    // Fresh reload — eş zamanlı başka kaydın etkisini gör
                    await _context.Entry(section).ReloadAsync();
                    if (section.EnrolledCount >= section.Capacity)
                    {
                        if (tx != null) await tx.RollbackAsync();
                        response.Success = false;
                        response.ValidationResult = new ValidationResultDto
                        {
                            IsValid = false,
                            Errors = new List<ValidationError>
                            {
                                new() { Rule = "Capacity", Message = $"'{section.Course.Code} {section.Course.Name}' dersinin kontenjanı bu esnada doldu ({section.EnrolledCount}/{section.Capacity}).", CourseSectionId = section.Id }
                            }
                        };
                        return response;
                    }

                    Domain.Entities.CourseRegistration reg;
                    if (existingByDropped.TryGetValue(section.Id, out var droppedReg))
                    {
                        // Reactivate — yeni kayıt oluşturmak unique constraint'i kırardı
                        droppedReg.Status = RegistrationStatus.Draft;
                        droppedReg.UpdatedAt = DateTime.UtcNow;
                        reg = droppedReg;
                    }
                    else
                    {
                        reg = new Domain.Entities.CourseRegistration
                        {
                            StudentId = studentId,
                            CourseSectionId = section.Id,
                            Status = RegistrationStatus.Draft,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.CourseRegistrations.Add(reg);
                    }

                    // Madde 6 tamamlama: AKTS snapshot + havuz atamasını burada da çöz
                    var (snapshotEcts, appliedPoolId) = await ResolveEctsSnapshotAsync(studentId, section.CourseId);
                    reg.EctsSnapshot = snapshotEcts;
                    reg.AppliedElectivePoolId = appliedPoolId;

                    section.EnrolledCount++;
                    registrations.Add(reg);
                }
                await _context.SaveChangesAsync();
                if (tx != null) await tx.CommitAsync();
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync();
                throw;
            }

            // Reload to get navigation properties
            registrations = await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                    .ThenInclude(cs => cs.Course)
                .Where(cr => cr.StudentId == studentId
                    && dto.CourseSectionIds.Contains(cr.CourseSectionId)
                    && cr.Status == RegistrationStatus.Draft)
                .ToListAsync();
        }

        // Update all Draft -> target status
        foreach (var reg in registrations)
        {
            reg.Status = targetStatus;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        // Update RegistrationPlan status to Submitted
        var termId = registrations.FirstOrDefault()?.CourseSection.TermId;
        if (termId.HasValue)
        {
            var plan = await _context.RegistrationPlans
                .FirstOrDefaultAsync(rp => rp.StudentId == studentId && rp.TermId == termId.Value);
            if (plan != null)
            {
                plan.Status = RegistrationPlanStatus.Submitted;
                plan.SubmittedAt = DateTime.UtcNow;
                plan.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        response.Success = true;
        response.ValidationResult = validationResult; // May contain NeedsApproval warnings
        response.Registrations = registrations.Select(r => new RegistrationItemDto
        {
            Id = r.Id,
            CourseSectionId = r.CourseSectionId,
            CourseCode = r.CourseSection.Course.Code,
            CourseName = r.CourseSection.Course.Name,
            Ects = r.CourseSection.Course.Ects,
            Status = r.Status.ToString()
        }).ToList();

        return response;
    }

    public async Task<List<StudentRegistrationDto>> GetStudentRegistrationsAsync(int studentId)
    {
        var now = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= now && t.TermEnd >= now);
        if (activeTerm == null)
            return new List<StudentRegistrationDto>();

        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Where(cr => cr.StudentId == studentId
                && cr.CourseSection.TermId == activeTerm.Id)
            .ToListAsync();

        // Load instructor user names
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNames = await _context.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        // Load curriculum course info for compulsory/semester
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        var mainProgramId = student?.StudentPrograms
            .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive)?.ProgramId;

        Dictionary<int, (bool IsCompulsory, int Semester)>? curriculumMap = null;
        if (mainProgramId.HasValue)
        {
            var curriculumCourses = await _context.CurriculumCourses
                .Include(cc => cc.Curriculum)
                .Where(cc => cc.Curriculum.ProgramId == mainProgramId.Value && cc.Curriculum.IsActive)
                .ToListAsync();

            curriculumMap = curriculumCourses.ToDictionary(
                cc => cc.CourseId,
                cc => (cc.IsCompulsory, cc.SuggestedSemester));
        }

        // Check for past failures to determine take type
        var courseIds = registrations.Select(r => r.CourseSection.CourseId).Distinct().ToList();
        var pastGrades = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == studentId
                && cr.Grade != null
                && courseIds.Contains(cr.CourseSection.CourseId))
            .Select(cr => new { cr.CourseSection.CourseId, cr.Grade })
            .ToListAsync();

        var failedCourseIds = pastGrades
            .Where(g => g.Grade == GradeCode.FF || g.Grade == GradeCode.NA || g.Grade == GradeCode.U)
            .Select(g => g.CourseId)
            .ToHashSet();

        // Load SectionSchedule records for schedule display
        var regSectionIds = registrations.Select(r => r.CourseSectionId).Distinct().ToList();
        var sectionSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => regSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync();
        var schedulesBySectionId = sectionSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.FormatSchedule(g));

        return registrations.Select(r =>
        {
            var courseId = r.CourseSection.CourseId;
            var isCompulsory = curriculumMap != null
                && curriculumMap.TryGetValue(courseId, out var info) && info.IsCompulsory;
            var semester = curriculumMap != null
                && curriculumMap.TryGetValue(courseId, out var info2) ? info2.Semester : 0;

            // Determine take type (Alış)
            string takeType;
            if (r.IsRepeated)
                takeType = "Yukseltme";
            else if (failedCourseIds.Contains(courseId))
                takeType = "Alttan";
            else if (isCompulsory)
                takeType = "Zorunlu";
            else
                takeType = "Secmeli";

            return new StudentRegistrationDto
            {
                RegistrationId = r.Id,
                CourseSectionId = r.CourseSectionId,
                CourseCode = r.CourseSection.Course.Code,
                CourseName = r.CourseSection.Course.Name,
                Ects = r.CourseSection.Course.Ects,
                TheoryHours = r.CourseSection.Course.TheoryHours,
                LabHours = r.CourseSection.Course.LabHours,
                InstructorName = r.CourseSection.Instructor != null
                    && userNames.TryGetValue(r.CourseSection.Instructor.UserId, out var name)
                        ? $"{r.CourseSection.Instructor.Title} {name}".Trim()
                        : string.Empty,
                Schedule = schedulesBySectionId.GetValueOrDefault(r.CourseSectionId, string.Empty),
                Status = r.Status.ToString(),
                AdvisorNote = r.AdvisorNote,
                IsCompulsory = isCompulsory,
                SuggestedSemester = semester,
                IsRepeated = r.IsRepeated,
                TakeType = takeType
            };
        }).ToList();
    }

    public async Task<StudentAttendanceStatusDto?> GetStudentAttendanceStatusAsync(int studentId, int? termId = null)
    {
        var registeredTermRows = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => cr.StudentId == studentId
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .Select(cr => new
            {
                TermId = cr.CourseSection.Term.Id,
                TermName = cr.CourseSection.Term.Name,
                cr.CourseSection.Term.Year,
                cr.CourseSection.Term.TermType,
                IsActive = !cr.CourseSection.Term.IsCancelled
                    && cr.CourseSection.Term.TermStart <= DateTime.UtcNow
                    && cr.CourseSection.Term.TermEnd >= DateTime.UtcNow
            })
            .Distinct()
            .ToListAsync();

        var availableTerms = registeredTermRows
            .GroupBy(term => term.TermId)
            .Select(group =>
            {
                var term = group.First();
                return new StudentAttendanceTermDto
                {
                    TermId = term.TermId,
                    TermName = term.TermName,
                    Year = term.Year,
                    Semester = term.TermType.ToString(),
                    IsActive = term.IsActive
                };
            })
            .OrderByDescending(term => term.IsActive)
            .ThenByDescending(term => term.Year)
            .ThenByDescending(term => term.TermId)
            .ToList();

        var selectedTerm = termId.HasValue
            ? availableTerms.FirstOrDefault(term => term.TermId == termId.Value)
            : availableTerms.FirstOrDefault(term => term.IsActive) ?? availableTerms.FirstOrDefault();

        if (selectedTerm == null && termId.HasValue)
            return null;

        if (selectedTerm == null)
        {
            var nowAttendance = _clock.UtcNow;
            var activeTerm = await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= nowAttendance && t.TermEnd >= nowAttendance);

            if (activeTerm == null)
                return null;

            selectedTerm = new StudentAttendanceTermDto
            {
                TermId = activeTerm.Id,
                TermName = activeTerm.Name,
                Year = activeTerm.Year,
                Semester = activeTerm.TermType.ToString(),
                IsActive = activeTerm.IsActive(nowAttendance)
            };
            availableTerms.Add(selectedTerm);
        }

        var weekCount = Math.Max(1, await _config.GetIntAsync("Attendance.TermWeekCount", 14));
        var theoryLimitPercent = ClampPercent(await _config.GetDecimalAsync("Attendance.TheoryAbsenceLimitPercent", 30m));
        var labLimitPercent = ClampPercent(await _config.GetDecimalAsync("Attendance.LabAbsenceLimitPercent", 20m));

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        var activeStudentProgram = student?.StudentPrograms
            .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
        var activeProgramId = activeStudentProgram?.ProgramId;
        var activeProgramName = activeStudentProgram?.Program?.Name ?? string.Empty;

        var curriculumMap = new Dictionary<int, int>();
        if (activeProgramId.HasValue)
        {
            var curriculumCourses = await _context.CurriculumCourses
                .AsNoTracking()
                .Include(cc => cc.Curriculum)
                .Where(cc => cc.Curriculum.ProgramId == activeProgramId.Value && cc.Curriculum.IsActive)
                .Select(cc => new { cc.CourseId, cc.SuggestedSemester })
                .ToListAsync();

            curriculumMap = curriculumCourses
                .GroupBy(cc => cc.CourseId)
                .ToDictionary(group => group.Key, group => group.First().SuggestedSemester);
        }

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
                    .ThenInclude(c => c.Department)
            .Where(cr => cr.StudentId == studentId
                && cr.CourseSection.TermId == selectedTerm.TermId
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .OrderBy(cr => cr.CourseSection.Course.Code)
            .ThenBy(cr => cr.CourseSection.SectionNumber)
            .ToListAsync();

        return new StudentAttendanceStatusDto
        {
            TermId = selectedTerm.TermId,
            TermName = selectedTerm.TermName,
            Year = selectedTerm.Year,
            Semester = selectedTerm.Semester,
            AvailableTerms = availableTerms,
            Policy = new StudentAttendancePolicyDto
            {
                TermWeekCount = weekCount,
                TheoryAbsenceLimitPercent = theoryLimitPercent,
                LabAbsenceLimitPercent = labLimitPercent
            },
            Courses = registrations.Select(registration =>
            {
                var course = registration.CourseSection.Course;
                var theoryTotalHours = weekCount * Math.Max(0, course.TheoryHours);
                var labTotalHours = weekCount * Math.Max(0, course.LabHours);
                var isInStudentProgram = curriculumMap.TryGetValue(course.Id, out var suggestedSemester);
                var programName = isInStudentProgram && !string.IsNullOrWhiteSpace(activeProgramName)
                    ? activeProgramName
                    : course.Department?.Name ?? "Ortak Dersler";

                return new StudentAttendanceCourseDto
                {
                    RegistrationId = registration.Id,
                    CourseSectionId = registration.CourseSectionId,
                    SectionNumber = registration.CourseSection.SectionNumber,
                    CourseCode = course.Code,
                    CourseName = course.Name,
                    Ects = course.Ects,
                    NationalCredit = course.NationalCredit ?? course.Ects,
                    SuggestedSemester = suggestedSemester,
                    ProgramName = programName,
                    TheoryHours = course.TheoryHours,
                    LabHours = course.LabHours,
                    MandatoryAttendance = registration.MandatoryAttendance,
                    AttendanceEntered = false,
                    TheoryAbsenceHours = null,
                    LabAbsenceHours = null,
                    TotalAbsenceHours = null,
                    TotalTheoryHours = theoryTotalHours,
                    TotalLabHours = labTotalHours,
                    TheoryAbsenceLimitHours = CalculateLimitHours(theoryTotalHours, theoryLimitPercent),
                    LabAbsenceLimitHours = CalculateLimitHours(labTotalHours, labLimitPercent),
                    AttendanceStatusLabel = "Yoklama Girişi Yapılmamış"
                };
            }).ToList()
        };
    }

    public async Task<List<CourseHistoryTermDto>> GetCourseHistoryAsync(int studentId)
    {
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == studentId
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        // Load instructor user names
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNames = await _context.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var grouped = registrations
            .GroupBy(r => r.CourseSection.TermId)
            .Select(g =>
            {
                var term = g.First().CourseSection.Term;
                return new CourseHistoryTermDto
                {
                    TermId = term.Id,
                    TermName = term.Name,
                    Year = term.Year,
                    Semester = term.TermType.ToString(),
                    Courses = g.Select(r => new CourseHistoryItemDto
                    {
                        RegistrationId = r.Id,
                        CourseSectionId = r.CourseSectionId,
                        SectionNumber = r.CourseSection.SectionNumber,
                        CourseCode = r.CourseSection.Course.Code,
                        CourseName = r.CourseSection.Course.Name,
                        Ects = r.CourseSection.Course.Ects,
                        InstructorName = r.CourseSection.Instructor != null
                            && userNames.TryGetValue(r.CourseSection.Instructor.UserId, out var name)
                                ? $"{r.CourseSection.Instructor.Title} {name}".Trim()
                                : string.Empty,
                        Grade = r.Grade?.ToString(),
                        Status = r.Status.ToString(),
                        IsRepeated = r.IsRepeated
                    }).OrderBy(c => c.CourseCode).ToList()
                };
            })
            .OrderByDescending(t => t.Year)
                .ThenByDescending(t => t.Semester)
            .ToList();

        return grouped;
    }

    public async Task<RegistrationResponseDto> DropCourseAsync(int studentId, int registrationId)
    {
        var response = new RegistrationResponseDto();

        var registration = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
                    .ThenInclude(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(cr => cr.Id == registrationId && cr.StudentId == studentId);

        if (registration == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "RegistrationExists", Message = "Kayit bulunamadi." }
                }
            };
            return response;
        }

        // Guard: dönem AddDrop aşamasında olmalı
        var dropTerm = registration.CourseSection.Term;
        if (!dropTerm.IsAddDropOpen(_clock.UtcNow) && !dropTerm.IsRegistrationOpen(_clock.UtcNow))
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "TermStatus", Message = $"Ders bırakma sadece kayıt veya ekleme/bırakma döneminde yapılabilir." }
                }
            };
            return response;
        }

        // Check status is droppable
        if (registration.Status != RegistrationStatus.Active
            && registration.Status != RegistrationStatus.PendingAdvisorApproval
            && registration.Status != RegistrationStatus.Draft)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "DropStatus", Message = $"'{registration.Status}' durumundaki ders birakılamaz." }
                }
            };
            return response;
        }

        // Check add-drop period (FR-48)
        var term = registration.CourseSection.Term;
        var now = DateTime.UtcNow;
        if (!term.IsAddDropOpen(now))
        {
            // Allow dropping Draft items anytime, but Active/PendingAdvisorApproval only during add-drop
            if (registration.Status != RegistrationStatus.Draft)
            {
                response.Success = false;
                response.ValidationResult = new ValidationResultDto
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new()
                        {
                            Rule = "AddDropPeriod",
                            Message = "Ders bırakma dönemi dışında. Ders bırakma dönemi kapalı."
                        }
                    }
                };
                return response;
            }
        }

        // Set status to Dropped
        registration.Status = RegistrationStatus.Dropped;
        registration.UpdatedAt = DateTime.UtcNow;

        // Decrement section.EnrolledCount
        var section = registration.CourseSection;
        if (section.EnrolledCount > 0)
            section.EnrolledCount--;

        await _context.SaveChangesAsync();

        response.Success = true;
        response.Registrations = new List<RegistrationItemDto>
        {
            new()
            {
                Id = registration.Id,
                CourseSectionId = section.Id,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name,
                Ects = section.Course.Ects,
                Status = registration.Status.ToString()
            }
        };

        return response;
    }

    public async Task<RegistrationResponseDto> WithdrawCourseAsync(int studentId, int registrationId)
    {
        var response = new RegistrationResponseDto();
        var errors = new List<ValidationError>();

        var registration = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
                    .ThenInclude(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(cr => cr.Id == registrationId && cr.StudentId == studentId);

        if (registration == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "RegistrationExists", Message = "Kayit bulunamadi." }
                }
            };
            return response;
        }

        // Guard: dönem WithdrawalPeriod aşamasında olmalı
        var withdrawTerm = registration.CourseSection.Term;
        if (!withdrawTerm.IsWithdrawalOpen(_clock.UtcNow))
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "TermStatus", Message = $"Ders çekilme sadece çekilme döneminde yapılabilir." }
                }
            };
            return response;
        }

        // Only active registrations can be withdrawn
        if (registration.Status != RegistrationStatus.Active)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "WithdrawStatus", Message = "Sadece 'Active' durumundaki derslerden cekilebilir." }
                }
            };
            return response;
        }

        var term = registration.CourseSection.Term;
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "StudentExists", Message = "Ogrenci bulunamadi." }
                }
            };
            return response;
        }

        // FR-57: Check withdrawal deadline
        if (!term.IsWithdrawalOpen(DateTime.UtcNow))
        {
            errors.Add(new ValidationError
            {
                Rule = "WithdrawalDeadline",
                Message = "Ders çekilme dönemi kapalı. Çekilme süresi dolmuş veya henüz başlamamış."
            });
        }

        // FR-58: Cannot withdraw if it's the only course (BR-11)
        var activeRegistrationsCount = await _context.CourseRegistrations
            .CountAsync(cr => cr.StudentId == studentId
                && cr.CourseSection.TermId == term.Id
                && cr.Status == RegistrationStatus.Active);

        if (activeRegistrationsCount <= 1)
        {
            errors.Add(new ValidationError
            {
                Rule = "LastCourse",
                Message = "Son dersinizden cekilemezsiniz. En az bir dersiniz aktif olmalidir."
            });
        }

        // FR-59: Cannot withdraw from the same course more than once
        if (registration.CourseWithdrawalCount >= 1)
        {
            errors.Add(new ValidationError
            {
                Rule = "DuplicateWithdrawal",
                Message = "Ayni dersten birden fazla cekilemezsiniz."
            });
        }

        // FR-60: Cannot withdraw in first two semesters (undergraduate)
        if (student.CompletedSemesters < 2)
        {
            var mainProgram = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
            if (mainProgram?.Program != null &&
                (mainProgram.Program.DegreeLevel == DegreeLevel.Bachelor || mainProgram.Program.DegreeLevel == DegreeLevel.Associate))
            {
                errors.Add(new ValidationError
                {
                    Rule = "FirstTwoSemesters",
                    Message = "Ilk iki donemde ders cekilme islemi yapilamaz."
                });
            }
        }

        // FR-61: Cannot withdraw from mandatory retake courses
        var allPastRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId && cr.Grade != null)
            .ToListAsync();

        var courseId = registration.CourseSection.CourseId;
        var isFailedRetake = allPastRegistrations.Any(r =>
            r.CourseSection.CourseId == courseId &&
            (r.Grade == GradeCode.FF || r.Grade == GradeCode.NA || r.Grade == GradeCode.U));

        // Check if it was subsequently passed
        var wasSubsequentlyPassed = allPastRegistrations.Any(r =>
            r.CourseSection.CourseId == courseId &&
            r.Grade != null &&
            r.Grade != GradeCode.FF &&
            r.Grade != GradeCode.NA &&
            r.Grade != GradeCode.U &&
            r.Grade != GradeCode.W &&
            r.Grade != GradeCode.I);

        if (isFailedRetake && !wasSubsequentlyPassed)
        {
            errors.Add(new ValidationError
            {
                Rule = "MandatoryRetake",
                Message = "Basarisiz oldugunuz zorunlu tekrar derslerden cekilemezsiniz."
            });
        }

        // FR-62: Max withdrawals: 6 for bachelor, 3 for associate
        var mainProg = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
        if (mainProg?.Program != null)
        {
            var isAssociate = mainProg.Program.DegreeLevel == DegreeLevel.Associate;
            var maxWithdrawalsKey = isAssociate
                ? "Withdrawal.MaxCount.Associate"
                : "Withdrawal.MaxCount.Undergraduate";
            int maxWithdrawals = await _config.GetIntAsync(maxWithdrawalsKey, isAssociate ? 3 : 6);
            if (student.WithdrawalCount >= maxWithdrawals)
            {
                errors.Add(new ValidationError
                {
                    Rule = "MaxWithdrawals",
                    Message = $"Maksimum ders cekilme hakkini ({maxWithdrawals}) kullandiniz."
                });
            }
        }

        if (errors.Any())
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = errors
            };
            return response;
        }

        // Set status to Withdrawn, assign W grade
        registration.Status = RegistrationStatus.Withdrawn;
        registration.Grade = GradeCode.W;
        registration.CourseWithdrawalCount++;
        registration.UpdatedAt = DateTime.UtcNow;

        // Decrement section enrolled count
        var sec = registration.CourseSection;
        if (sec.EnrolledCount > 0)
            sec.EnrolledCount--;

        // Increment student's cumulative withdrawal count
        student.WithdrawalCount++;

        await _context.SaveChangesAsync();

        response.Success = true;
        response.Registrations = new List<RegistrationItemDto>
        {
            new()
            {
                Id = registration.Id,
                CourseSectionId = sec.Id,
                CourseCode = sec.Course.Code,
                CourseName = sec.Course.Name,
                Ects = sec.Course.Ects,
                Status = registration.Status.ToString()
            }
        };

        return response;
    }

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
            .OrderBy(dto => dto.StudentName)
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

        return result;
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

    private static decimal ClampPercent(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 100m) return 100m;
        return value;
    }

    private static int CalculateLimitHours(int totalHours, decimal limitPercent)
    {
        if (totalHours <= 0 || limitPercent <= 0m)
            return 0;

        return (int)Math.Ceiling(totalHours * limitPercent / 100m);
    }

    private async Task<ValidationResultDto> EvaluateAdvisorConstraintsAsync(
        int studentId,
        IEnumerable<AdvisorConstraintSectionInfo> sections)
    {
        var result = new ValidationResultDto { IsValid = true };

        if (_advisorConstraintService == null)
            return result;

        foreach (var section in sections
            .GroupBy(s => new { s.CourseSectionId, s.CourseId, s.TermId })
            .Select(g => g.First()))
        {
            var effectiveConstraint = await _advisorConstraintService.GetEffectiveConstraintAsync(
                studentId,
                section.CourseId,
                section.TermId);

            if (effectiveConstraint == null)
                continue;

            if (effectiveConstraint.ConstraintType == SIS.Domain.Enums.AdvisorConstraintType.Block)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Rule = "AdvisorConstraintBlock",
                    Message = string.IsNullOrWhiteSpace(effectiveConstraint.Note)
                        ? $"Danisman kisiti nedeniyle '{section.CourseCode} {section.CourseName}' dersi secilemez."
                        : $"Danisman kisiti nedeniyle '{section.CourseCode} {section.CourseName}' dersi secilemez: {effectiveConstraint.Note}",
                    CourseSectionId = section.CourseSectionId
                });
                continue;
            }

            result.Warnings.Add(new ValidationError
            {
                Rule = "AdvisorConstraintWarning",
                Message = string.IsNullOrWhiteSpace(effectiveConstraint.Note)
                    ? $"Danismaniniz '{section.CourseCode} {section.CourseName}' dersini almanizi onermemektedir."
                    : $"Danismaniniz '{section.CourseCode} {section.CourseName}' dersi icin uyari koydu: {effectiveConstraint.Note}",
                CourseSectionId = section.CourseSectionId
            });
        }

        return result;
    }

    private sealed class AdvisorConstraintSectionInfo
    {
        public int CourseSectionId { get; init; }
        public int CourseId { get; init; }
        public int TermId { get; init; }
        public string CourseCode { get; init; } = string.Empty;
        public string CourseName { get; init; } = string.Empty;
    }

    private static string GetTermStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Inactive   => "Pasif",
        TermStatus.Active     => "Aktif",
        TermStatus.Cancelled  => "İptal",
        _                     => s.ToString()
    };

    /// <summary>
    /// Bir dersin öğrencinin müfredatındaki AKTS değerini ve (varsa) hangi havuza sayılacağını çözer.
    /// Zorunlu/tekil seçmeli → katalog AKTS, havuz üyesi → havuzun EctsPerCourse (null ise katalog),
    /// müfredat dışı → katalog AKTS + null havuz.
    /// Kayıt anında bu değer EctsSnapshot ve AppliedElectivePoolId alanlarına donar.
    /// </summary>
    private async Task<(int? ectsSnapshot, int? appliedPoolId)> ResolveEctsSnapshotAsync(
        int studentId, int courseId, CancellationToken ct = default)
    {
        var activeProgram = await _context.StudentPrograms
            .AsNoTracking()
            .Where(sp => sp.StudentId == studentId && sp.Type == StudentProgramType.Main && sp.IsActive)
            .FirstOrDefaultAsync(ct);

        var catalogCourse = await _context.Courses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId, ct);
        var catalogEcts = catalogCourse?.Ects ?? 0;

        if (activeProgram == null)
            return (catalogEcts, null);

        // 1) Direkt müfredat ataması (zorunlu veya tekil seçmeli)
        var directExists = await _context.CurriculumCourses.AsNoTracking()
            .AnyAsync(cc => cc.CurriculumId == activeProgram.CurriculumId && cc.CourseId == courseId, ct);
        if (directExists)
            return (catalogEcts, null);

        // 2) Müfredatın havuzlarından birinde mi?
        var poolHit = await _context.CurriculumElectivePools.AsNoTracking()
            .Where(p => p.CurriculumId == activeProgram.CurriculumId
                && p.ElectiveGroup.Courses.Any(egc => egc.CourseId == courseId))
            .Select(p => new { p.Id, p.EctsPerCourse })
            .FirstOrDefaultAsync(ct);
        if (poolHit != null)
        {
            var ects = poolHit.EctsPerCourse ?? catalogEcts;
            return (ects, poolHit.Id);
        }

        // 3) Müfredat dışı (warning olarak raporlanmıştı) — katalog AKTS
        return (catalogEcts, null);
    }
}
