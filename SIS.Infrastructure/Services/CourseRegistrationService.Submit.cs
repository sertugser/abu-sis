using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// CourseRegistrationService — kayıt gönderme (SubmitRegistrationAsync).
/// Plan'daki dersleri Active'e geçirir; danışman onayına gönderir.
/// </summary>
public partial class CourseRegistrationService
{
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

        var activeTermId = submittedSections.Select(section => (int?)section.TermId).Distinct().SingleOrDefault();
        var webModuleGate = await ValidateStudentRegistrationWebModuleAsync(studentId, activeTermId);
        if (webModuleGate is not null)
        {
            response.Success = false;
            response.ValidationResult = webModuleGate;
            return response;
        }

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

                    // Madde 6 tamamlama: AKTS + kredi snapshot + havuz ataması + (Faz 4) Açma Nedeni id snapshot
                    var (snapshotEcts, snapshotCredit, appliedPoolId, openingReasonIdSnapshot) = await ResolveCourseSnapshotsAsync(studentId, section.CourseId);
                    reg.EctsSnapshot = snapshotEcts;
                    reg.CreditSnapshot = snapshotCredit;
                    reg.AppliedElectivePoolId = appliedPoolId;
                    reg.OpeningReasonIdSnapshot = openingReasonIdSnapshot;

                    // Faz 4 — BypassMandatoryAttendance flag'i: bu ders için devam zorunluluğunu false zorla.
                    if (openingReasonIdSnapshot.HasValue)
                    {
                        var bypassAttendance = await _context.CurriculumCourseOpeningReasons
                            .AsNoTracking()
                            .AnyAsync(r => r.Id == openingReasonIdSnapshot.Value && r.BypassMandatoryAttendance);
                        if (bypassAttendance)
                            reg.MandatoryAttendance = false;
                    }

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

}
