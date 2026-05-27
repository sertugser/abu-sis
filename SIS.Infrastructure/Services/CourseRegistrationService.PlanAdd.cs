using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// CourseRegistrationService — plan ekleme akışı (AddToPlanAsync).
/// Kayıt fazından önce öğrenci kendi planına ders ekler. Validation + soft-hold capacity.
/// </summary>
public partial class CourseRegistrationService
{
    public async Task<RegistrationResponseDto> EnsureFailedCoursesInPlanAsync(int studentId)
    {
        var response = new RegistrationResponseDto { Success = true, Registrations = new List<RegistrationItemDto>() };

        var activeTerm = await _context.AcademicTerms.AsNoTracking().ActiveTerm().FirstOrDefaultAsync();
        if (activeTerm == null)
            return response;

        // Başarısız (FF/NA/U) olup sonradan geçilmemiş dersler.
        var graded = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => cr.StudentId == studentId && cr.Grade != null)
            .Select(cr => new { cr.CourseSection.CourseId, cr.Grade })
            .ToListAsync();

        var failedCourseIds = graded
            .GroupBy(g => g.CourseId)
            .Where(g => g.Any(x => RegistrationRulePolicy.IsFailedGrade(x.Grade))
                && !g.Any(x => RegistrationRulePolicy.IsPassedGrade(x.Grade)))
            .Select(g => g.Key)
            .ToHashSet();

        if (failedCourseIds.Count == 0)
            return response;

        // Bu dönem zaten planda/kayıtlı olan dersler (tekrar eklemeyelim).
        var inPlanCourseIds = (await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => cr.StudentId == studentId
                && cr.CourseSection.TermId == activeTerm.Id
                && (cr.Status == RegistrationStatus.Draft
                    || cr.Status == RegistrationStatus.PendingAdvisorApproval
                    || cr.Status == RegistrationStatus.Active))
            .Select(cr => cr.CourseSection.CourseId)
            .ToListAsync())
            .ToHashSet();

        var toAdd = failedCourseIds.Where(c => !inPlanCourseIds.Contains(c)).ToList();
        if (toAdd.Count == 0)
            return response;

        // Yayın kapısı: fakültesi onaylı dersler eklenebilir (ortak dersler muaf).
        var approvedFacultyIds = (await _context.FacultyScheduleApprovals
            .AsNoTracking()
            .Where(a => a.TermId == activeTerm.Id)
            .Select(a => a.FacultyId)
            .ToListAsync())
            .ToHashSet();

        foreach (var courseId in toAdd)
        {
            var sections = await _context.CourseSections
                .Include(cs => cs.Course)
                .Where(cs => cs.TermId == activeTerm.Id
                    && cs.CourseId == courseId
                    && cs.Status == CourseSectionStatus.Active
                    && !cs.IsEnrollmentSuspended)
                .OrderBy(cs => cs.SectionNumber)
                .ToListAsync();

            foreach (var section in sections)
            {
                // Yayın kapısı — bölüme bağlı dersin fakültesi onaylı olmalı.
                if (section.Course.DepartmentId.HasValue)
                {
                    var facId = await _context.Departments
                        .Where(d => d.Id == section.Course.DepartmentId.Value)
                        .Select(d => d.FacultyId)
                        .FirstOrDefaultAsync();
                    if (!approvedFacultyIds.Contains(facId))
                        continue;
                }
                if (section.EnrolledCount >= section.Capacity)
                    continue;

                // Tüm iş kuralları/kapasite AddToPlanAsync içinde uygulanır. Uygun ilk şubeyi ekle.
                var addResult = await AddToPlanAsync(studentId, section.Id, RegistrationStatus.Draft);
                if (addResult.Success)
                {
                    response.Registrations!.AddRange(addResult.Registrations);
                    break; // bu ders için uygun şube eklendi
                }
            }
        }

        return response;
    }

    public async Task<RegistrationResponseDto> AddToPlanAsync(int studentId, int courseSectionId, RegistrationStatus initialStatus = RegistrationStatus.Draft)
    {
        // Yalnızca öğrenci-planı (Draft) veya danışman öğrenci-adına (PendingAdvisorApproval)
        // ekleme desteklenir. Diğer statüler bu akış için anlamsız.
        var isAdvisorAdd = initialStatus == RegistrationStatus.PendingAdvisorApproval;
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

        // Yayın kapısı (draft/publish): section Active değilse VEYA fakültesi o dönem için
        // onaysızsa plana eklenemez. Catalog görünürlük kapısının yazma-tarafı karşılığı —
        // frontend'i atlayan istekleri de engeller. Ortak dersler (DepartmentId == null)
        // bir fakülteye bağlı olmadığından onaydan muaf; yalnızca Active şartı geçerlidir.
        if (section.Status != CourseSectionStatus.Active)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "SectionNotActive", Message = "Bu ders şubesi henüz seçime açık değil.", CourseSectionId = courseSectionId }
                }
            };
            return response;
        }

        // Faz 3b: "Düzenlemek için durdurulmuş" section geçici olarak yeni kayda kapalıdır
        // (mevcut kayıtlılar korunur). Yeni öğrenci plana ekleyemez.
        if (section.IsEnrollmentSuspended)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "EnrollmentSuspended", Message = "Bu ders şubesi şu anda düzenlemede — geçici olarak yeni kayda kapalı.", CourseSectionId = courseSectionId }
                }
            };
            return response;
        }

        if (section.Course.DepartmentId.HasValue)
        {
            var sectionFacultyId = await _context.Departments
                .Where(d => d.Id == section.Course.DepartmentId.Value)
                .Select(d => d.FacultyId)
                .FirstOrDefaultAsync();
            var facultyApproved = await _context.FacultyScheduleApprovals
                .AnyAsync(a => a.FacultyId == sectionFacultyId && a.TermId == section.TermId);
            if (!facultyApproved)
            {
                response.Success = false;
                response.ValidationResult = new ValidationResultDto
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new() { Rule = "FacultyScheduleNotApproved", Message = "Bu dersin programı henüz yayınlanmadı.", CourseSectionId = courseSectionId }
                    }
                };
                return response;
            }
        }

        var registrationControlCode = await ResolveStudentRegistrationControlCodeAsync(studentId, section.TermId);
        var webModuleGate = await ValidateModuleAsync(
            registrationControlCode,
            section.TermId,
            "WebModuleRegistrationClosed",
            "Ders kayıt işlemleri şu anda kullanılamıyor.");
        if (webModuleGate is not null)
        {
            response.Success = false;
            response.ValidationResult = webModuleGate;
            return response;
        }

        // Guard: kayıt dönemi kontrolü
        var term = await _context.AcademicTerms.AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == section.TermId);
        if (term != null && !await IsManualOpenModeAsync(registrationControlCode))
        {
            if (term.AcademicCalendar == null)
            {
                response.Success = false;
                response.ValidationResult = new ValidationResultDto
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new() { Rule = "CalendarMissing", Message = "Bu dönemin akademik takvimi henüz tanımlanmamış. Kayıt alamazsınız — lütfen öğrenci işleri ile iletişime geçin." }
                    }
                };
                return response;
            }
            if (!term.IsRegistrationOpen(_clock.UtcNow) && !term.IsAddDropOpen(_clock.UtcNow))
            {
                response.Success = false;
                response.ValidationResult = new ValidationResultDto
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new() { Rule = "RegistrationClosed", Message = "Ders kayıt dönemi açık değil." }
                    }
                };
                return response;
            }
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
                // Danışman öğrenci adına ekliyorsa plan doğrudan "onay bekler" (Submitted) başlar;
                // öğrenci kendi akışında Draft başlar ve sonradan Submit eder.
                Status = isAdvisorAdd ? RegistrationPlanStatus.Submitted : RegistrationPlanStatus.Draft,
                SubmittedAt = isAdvisorAdd ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow
            };
            _context.RegistrationPlans.Add(plan);
            await _context.SaveChangesAsync(); // need ID before FK assignment
        }
        else if (plan.Status == RegistrationPlanStatus.Rejected)
        {
            // Rejected plan can be edited and resubmitted.
            // Danışman ekliyorsa doğrudan Submitted'a çekilir; öğrenci ekliyorsa Draft'a döner.
            plan.Status = isAdvisorAdd ? RegistrationPlanStatus.Submitted : RegistrationPlanStatus.Draft;
            if (isAdvisorAdd) plan.SubmittedAt = DateTime.UtcNow;
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
        else if (isAdvisorAdd && plan.Status != RegistrationPlanStatus.Approved)
        {
            // Danışman, öğrencinin (Draft/Submitted) planına ekliyor → plan onay bekler durumda kalsın.
            plan.Status = RegistrationPlanStatus.Submitted;
            plan.SubmittedAt ??= DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;
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

            // İntibak: öğrencinin müfredatındaki (yeni→eski) ders haritası.
            // Önkoşul yeni bir dersse ve öğrenci o dersin eski karşılığını (PreviousCourse) geçtiyse önkoşul sağlanır.
            var studentCurriculumId = await _context.StudentPrograms
                .Where(sp => sp.StudentId == studentId && sp.Type == StudentProgramType.Main && sp.IsActive)
                .Select(sp => (int?)sp.CurriculumId)
                .FirstOrDefaultAsync();
            var previousMap = studentCurriculumId is null
                ? new Dictionary<int, int>()
                : PreviousCourseResolver.BuildMap(await _context.CurriculumCourses
                    .Where(cc => cc.CurriculumId == studentCurriculumId && cc.PreviousCourseId != null)
                    .ToListAsync());

            foreach (var reqId in prereqs)
            {
                var met = PreviousCourseResolver.IsCovered(reqId, passedSet, previousMap);
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
            droppedRecord.Status = initialStatus;
            droppedRecord.RegistrationPlanId = plan.Id;
            droppedRecord.UpdatedAt = DateTime.UtcNow;
            registration = droppedRecord;
        }
        else
        {
            // Create new CourseRegistration (plan/soft hold - FR-44).
            // Öğrenci akışı: Draft. Danışman öğrenci-adına: PendingAdvisorApproval.
            registration = new Domain.Entities.CourseRegistration
            {
                StudentId = studentId,
                CourseSectionId = courseSectionId,
                RegistrationPlanId = plan.Id,
                Status = initialStatus,
                CreatedAt = DateTime.UtcNow
            };
            _context.CourseRegistrations.Add(registration);
        }

        // FR-56C: Mark as repeated if student already passed this course (grade improvement)
        registration.IsRepeated = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .AnyAsync(cr => cr.StudentId == studentId
                && cr.CourseSection.CourseId == section.CourseId
                && cr.Grade != null
                && cr.Grade != GradeCode.FF
                && cr.Grade != GradeCode.NA
                && cr.Grade != GradeCode.U
                && cr.Grade != GradeCode.W
                && cr.Grade != GradeCode.I);

        // Madde 6 tamamlama: AKTS + kredi snapshot + havuz ataması + (Faz 4) Açma Nedeni id snapshot
        var (snapshotEcts, snapshotCredit, appliedPoolId, openingReasonIdSnapshot) = await ResolveCourseSnapshotsAsync(studentId, section.CourseId);
        registration.EctsSnapshot = snapshotEcts;
        registration.CreditSnapshot = snapshotCredit;
        registration.AppliedElectivePoolId = appliedPoolId;
        registration.OpeningReasonIdSnapshot = openingReasonIdSnapshot;

        // Faz 4 — Açma Nedeni BypassMandatoryAttendance=true ise devam zorunluluğunu zorla false yap.
        // Bu ders için OpeningReason "Dönemsel (Teorik)" gibi bypass flag'liyse, alttan tekrar bile olsa
        // attendance zorunlu sayılmaz. Madde 7 implement edildiğinde bu override geçerli kalır.
        if (openingReasonIdSnapshot.HasValue)
        {
            var bypassAttendance = await _context.CurriculumCourseOpeningReasons
                .AsNoTracking()
                .AnyAsync(r => r.Id == openingReasonIdSnapshot.Value && r.BypassMandatoryAttendance);
            if (bypassAttendance)
                registration.MandatoryAttendance = false;
        }

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

}
