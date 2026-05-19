using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CourseOpening;

/// <summary>
/// Section yaşam döngüsü: aç / toplu aç / güncelle / kapat / yeniden aç.
/// Faz 3.C: Eski CourseOpeningService'in CRUD ve lifecycle bölümü.
/// </summary>
public class CourseSectionLifecycleService(
    AppDbContext context,
    IEnrollmentForecastCacheInvalidator forecastCacheInvalidator,
    ISystemConfigProvider systemConfig,
    CourseOpeningHelpers helpers) : ICourseSectionLifecycleService
{
    private const int MaxSectionsFallback = 20;
    private const string MaxSectionsKey = "CourseOpening.MaxSectionsPerCourse";

    private readonly AppDbContext _context = context;
    private readonly IEnrollmentForecastCacheInvalidator _forecastCacheInvalidator = forecastCacheInvalidator;
    private readonly ISystemConfigProvider _systemConfig = systemConfig;
    private readonly CourseOpeningHelpers _helpers = helpers;

    public async Task<List<OpenedCourseSectionDto>> OpenCourseAsync(int departmentId, int termId, OpenCourseRequest request, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (term.IsCancelled)
            throw new InvalidOperationException("Ders açmak için dönem iptal edilmemiş olmalıdır.");

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, ct)
            ?? throw new InvalidOperationException($"Ders bulunamadı (ID: {request.CourseId}).");

        if (!course.DepartmentId.HasValue)
            throw new InvalidOperationException($"Ders '{course.Code}' bir bölüme atanmamış; açılamaz. Önce bir birime atayın.");
        if (course.DepartmentId != departmentId)
            throw new InvalidOperationException($"Ders '{course.Code}' bu bölüme ait değil.");

        if (request.CapacityPerSection <= 0)
            throw new InvalidOperationException("Kontenjan 0'dan büyük olmalıdır.");

        var maxSections = await _systemConfig.GetIntAsync(MaxSectionsKey, MaxSectionsFallback, ct);
        if (request.SectionCount <= 0 || request.SectionCount > maxSections)
            throw new InvalidOperationException($"Şube sayısı 1 ile {maxSections} arasında olmalıdır.");

        var existingSections = await _context.CourseSections
            .Where(cs => cs.CourseId == request.CourseId && cs.TermId == termId)
            .ToListAsync(ct);

        var maxSectionNumber = existingSections.Count > 0
            ? existingSections.Max(s => s.SectionNumber)
            : 0;

        int? crossListGroupId = null;
        var linkedToOpen = new List<(int CourseId, int? DeptId)>();
        if (request.IncludeCrossLists)
        {
            var linked = await _context.CourseCrossLists
                .AsNoTracking()
                .Include(x => x.LinkedCourse)
                .Where(x => x.CourseId == request.CourseId)
                .Select(x => new { x.LinkedCourseId, DeptId = x.LinkedCourse.DepartmentId })
                .ToListAsync(ct);
            if (linked.Any())
            {
                var maxGroupId = await _context.CourseSections
                    .Where(cs => cs.CrossListGroupId != null)
                    .Select(cs => cs.CrossListGroupId!.Value)
                    .DefaultIfEmpty(0)
                    .MaxAsync(ct);
                crossListGroupId = maxGroupId + 1;
                foreach (var l in linked)
                    linkedToOpen.Add((l.LinkedCourseId, l.DeptId));
            }
        }

        if (request.InstructorId.HasValue)
        {
            await _helpers.ValidateInstructorAssignmentAsync(request.InstructorId.Value, course.DepartmentId, ct);
            var additionalSectionCount = request.SectionCount * (1 + linkedToOpen.Count);
            var weeklyHoursPerSection = course.TheoryHours + course.LabHours;
            await ValidateInstructorWeeklyLoadLimitAsync(
                request.InstructorId.Value,
                termId,
                weeklyHoursPerSection * additionalSectionCount,
                ct);
        }

        var created = new List<CourseSection>();
        for (var i = 0; i < request.SectionCount; i++)
        {
            var section = new CourseSection
            {
                CourseId = request.CourseId,
                TermId = termId,
                SectionNumber = maxSectionNumber + i + 1,
                Capacity = request.CapacityPerSection,
                InternalQuota = request.InternalQuota,
                ExternalQuota = request.ExternalQuota,
                InstructorId = request.InstructorId,
                EnrolledCount = 0,
                Status = CourseSectionStatus.Active,
                IsFinalized = false,
                CrossListGroupId = crossListGroupId,
            };
            _context.CourseSections.Add(section);
            created.Add(section);
        }

        var linkedCreated = new List<CourseSection>();
        foreach (var (linkedCourseId, _) in linkedToOpen)
        {
            var linkedMax = await _context.CourseSections
                .Where(cs => cs.CourseId == linkedCourseId && cs.TermId == termId)
                .Select(cs => (int?)cs.SectionNumber)
                .MaxAsync(ct) ?? 0;
            for (var i = 0; i < request.SectionCount; i++)
            {
                var linkedSection = new CourseSection
                {
                    CourseId = linkedCourseId,
                    TermId = termId,
                    SectionNumber = linkedMax + i + 1,
                    Capacity = request.CapacityPerSection,
                    InternalQuota = request.InternalQuota,
                    ExternalQuota = request.ExternalQuota,
                    InstructorId = request.InstructorId,
                    EnrolledCount = 0,
                    Status = CourseSectionStatus.Active,
                    IsFinalized = false,
                    CrossListGroupId = crossListGroupId,
                };
                _context.CourseSections.Add(linkedSection);
                linkedCreated.Add(linkedSection);
            }
        }

        await _context.SaveChangesAsync(ct);

        var existingPolicies = await _context.CourseAccessPolicies
            .Where(cap => cap.CourseId == request.CourseId && cap.TermId == termId)
            .ToListAsync(ct);

        var activeCount = existingSections.Count(s => s.IsActive);
        var shouldReplacePolicies = activeCount == 0
            || existingPolicies.Count == 0
            || request.AccessibleDepartmentIds is { Count: > 0 };

        if (shouldReplacePolicies)
        {
            _context.CourseAccessPolicies.RemoveRange(existingPolicies);

            if (request.AccessibleDepartmentIds is { Count: > 0 })
            {
                foreach (var deptId in request.AccessibleDepartmentIds.Distinct())
                {
                    _context.CourseAccessPolicies.Add(new CourseAccessPolicy
                    {
                        CourseId = request.CourseId,
                        TermId = termId,
                        DepartmentId = deptId,
                    });
                }
            }
            else
            {
                var curriculumDeptIds = await _context.CurriculumCourses
                    .AsNoTracking()
                    .Where(cc => cc.CourseId == request.CourseId && cc.Curriculum.IsActive)
                    .Select(cc => cc.Curriculum.Program.DepartmentId)
                    .Distinct()
                    .ToListAsync(ct);

                if (!curriculumDeptIds.Contains(departmentId))
                    curriculumDeptIds.Add(departmentId);

                foreach (var deptId in curriculumDeptIds)
                {
                    _context.CourseAccessPolicies.Add(new CourseAccessPolicy
                    {
                        CourseId = request.CourseId,
                        TermId = termId,
                        DepartmentId = deptId,
                    });
                }
            }

            await _context.SaveChangesAsync(ct);
        }

        _forecastCacheInvalidator.InvalidateForecastContext();

        string? instructorName = null;
        if (request.InstructorId.HasValue)
        {
            var nameMap = await _helpers.LoadInstructorNameMapAsync([request.InstructorId.Value], ct);
            nameMap.TryGetValue(request.InstructorId.Value, out instructorName);
        }

        var sugSem = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CourseId == request.CourseId && cc.Curriculum.IsActive && cc.Curriculum.Program.DepartmentId == departmentId)
            .Select(cc => (int?)cc.SuggestedSemester)
            .MinAsync(ct) ?? 0;

        var mainDtos = created.Select(s => new OpenedCourseSectionDto
        {
            SectionId = s.Id,
            SectionNumber = s.SectionNumber,
            CourseId = s.CourseId,
            CourseCode = course.Code,
            CourseName = course.Name,
            TheoryHours = course.TheoryHours,
            LabHours = course.LabHours,
            Credits = course.TheoryHours + course.LabHours,
            Ects = course.Ects,
            SuggestedSemester = sugSem,
            Capacity = s.Capacity,
            InternalQuota = s.InternalQuota,
            ExternalQuota = s.ExternalQuota,
            EnrolledCount = 0,
            RegisteredCount = 0,
            DraftOrPendingCount = 0,
            InstructorId = s.InstructorId,
            InstructorName = instructorName,
            IsActive = true,
            CrossListGroupId = s.CrossListGroupId,
        }).ToList();

        if (linkedCreated.Count > 0)
        {
            var linkedCourseIds = linkedCreated.Select(s => s.CourseId).Distinct().ToList();
            var linkedCourseMap = await _context.Courses
                .AsNoTracking()
                .Where(c => linkedCourseIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);

            foreach (var s in linkedCreated)
            {
                if (!linkedCourseMap.TryGetValue(s.CourseId, out var lc)) continue;
                mainDtos.Add(new OpenedCourseSectionDto
                {
                    SectionId = s.Id,
                    SectionNumber = s.SectionNumber,
                    CourseId = s.CourseId,
                    CourseCode = lc.Code,
                    CourseName = lc.Name,
                    TheoryHours = lc.TheoryHours,
                    LabHours = lc.LabHours,
                    Credits = lc.TheoryHours + lc.LabHours,
                    Ects = lc.Ects,
                    SuggestedSemester = 0,
                    Capacity = s.Capacity,
                    InternalQuota = s.InternalQuota,
                    ExternalQuota = s.ExternalQuota,
                    EnrolledCount = 0,
                    RegisteredCount = 0,
                    DraftOrPendingCount = 0,
                    InstructorId = s.InstructorId,
                    InstructorName = instructorName,
                    IsActive = true,
                    CrossListGroupId = s.CrossListGroupId,
                });
            }
        }

        return mainDtos;
    }

    public async Task<BulkOpenCoursesResultDto> BulkOpenCoursesAsync(int departmentId, int termId, BulkOpenCoursesRequest request, CancellationToken ct = default)
    {
        var result = new BulkOpenCoursesResultDto();
        if (request.Courses.Count == 0)
            return result;

        var useTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        await using var transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;
        OpenCourseRequest? currentCourse = null;

        try
        {
            foreach (var courseReq in request.Courses)
            {
                currentCourse = courseReq;
                var created = await OpenCourseAsync(departmentId, termId, courseReq, ct);
                if (created.Count == 0) continue;

                // Cross-list ya da müfredat dışı paralel açılışlarda dönüş listesi
                // birden fazla CourseId içerebilir — her biri için ayrı özet üretiyoruz.
                foreach (var grp in created.GroupBy(s => s.CourseId))
                {
                    var first = grp.First();
                    var instructorNames = grp
                        .Select(s => s.InstructorName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .ToList();

                    result.OpenedCourses.Add(new OpenedCourseSummaryDto
                    {
                        CourseId = grp.Key,
                        CourseCode = first.CourseCode,
                        CourseName = first.CourseName,
                        SectionCount = grp.Count(),
                        SectionNumbers = grp.Select(s => s.SectionNumber).OrderBy(n => n).ToList(),
                        TotalCapacity = grp.Sum(s => s.Capacity),
                        InstructorName = instructorNames.Count == 1 ? instructorNames[0] : null,
                    });
                    result.TotalCoursesOpened++;
                    result.TotalSectionsCreated += grp.Count();
                }
            }

            if (transaction != null)
                await transaction.CommitAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            if (transaction != null)
                await transaction.RollbackAsync(ct);
            result.TotalCoursesOpened = 0;
            result.TotalSectionsCreated = 0;
            result.OpenedCourses.Clear();
            result.Errors.Add($"Ders ID {currentCourse?.CourseId}: {ex.Message} Toplu işlem geri alındı.");
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(ct);
            throw;
        }

        return result;
    }

    public async Task<OpenedCourseSectionDto> UpdateSectionAsync(UpdateOpenedSectionRequest request, CancellationToken ct = default)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct)
            ?? throw new InvalidOperationException("Şube bulunamadı.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.IsCancelled)
            throw new InvalidOperationException("Şube güncellemek için dönem iptal edilmemiş olmalıdır.");

        if (!section.IsActive)
            throw new InvalidOperationException("Pasif şube güncellenemez.");

        var registeredCount = await _helpers.GetRegisteredCountAsync(section.Id, ct);
        var draftOrPendingCount = await _helpers.GetDraftOrPendingCountAsync(section.Id, ct);

        if (request.InstructorId.HasValue)
        {
            await _helpers.ValidateInstructorAssignmentAsync(request.InstructorId.Value, section.Course.DepartmentId, ct);
            await ValidateInstructorWeeklyLoadLimitAsync(
                request.InstructorId.Value,
                section.TermId,
                section.Course.TheoryHours + section.Course.LabHours,
                ct,
                excludedSectionId: section.Id);
            section.InstructorId = request.InstructorId.Value;
        }

        if (request.Capacity.HasValue)
        {
            if (request.Capacity.Value <= 0)
                throw new InvalidOperationException("Kontenjan 0'dan büyük olmalıdır.");

            var minimumRequiredCapacity = Math.Max(section.EnrolledCount, registeredCount);
            if (request.Capacity.Value < minimumRequiredCapacity)
                throw new InvalidOperationException(
                    $"Kontenjan mevcut kayıtlı öğrenci sayısından ({minimumRequiredCapacity}) az olamaz.");

            section.Capacity = request.Capacity.Value;
        }

        if (request.InternalQuota.HasValue)
            section.InternalQuota = request.InternalQuota.Value == 0 ? null : request.InternalQuota.Value;

        if (request.ExternalQuota.HasValue)
            section.ExternalQuota = request.ExternalQuota.Value == 0 ? null : request.ExternalQuota.Value;

        await _context.SaveChangesAsync(ct);
        _forecastCacheInvalidator.InvalidateForecastContext();

        string? instructorName = null;
        if (section.InstructorId.HasValue)
        {
            var nameMap = await _helpers.LoadInstructorNameMapAsync([section.InstructorId.Value], ct);
            nameMap.TryGetValue(section.InstructorId.Value, out instructorName);
        }

        var sectionNumber = await _context.CourseSections
            .Where(cs => cs.TermId == section.TermId && cs.CourseId == section.CourseId && cs.Id <= section.Id)
            .CountAsync(ct);

        var sugSem = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CourseId == section.CourseId && cc.Curriculum.IsActive)
            .Select(cc => (int?)cc.SuggestedSemester)
            .MinAsync(ct) ?? 0;

        return new OpenedCourseSectionDto
        {
            SectionId = section.Id,
            SectionNumber = sectionNumber,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            Credits = section.Course.TheoryHours + section.Course.LabHours,
            Ects = section.Course.Ects,
            SuggestedSemester = sugSem,
            Capacity = section.Capacity,
            InternalQuota = section.InternalQuota,
            ExternalQuota = section.ExternalQuota,
            EnrolledCount = section.EnrolledCount,
            RegisteredCount = registeredCount,
            DraftOrPendingCount = draftOrPendingCount,
            InstructorId = section.InstructorId,
            InstructorName = instructorName,
            IsActive = section.IsActive,
        };
    }

    public async Task CloseSectionAsync(int sectionId, CancellationToken ct = default)
    {
        var section = await _context.CourseSections.FirstOrDefaultAsync(cs => cs.Id == sectionId, ct)
            ?? throw new InvalidOperationException("Şube bulunamadı.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.IsCancelled)
            throw new InvalidOperationException("Şube kapatmak için dönem iptal edilmemiş olmalıdır.");

        var pendingRegistrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && (cr.Status == RegistrationStatus.Draft || cr.Status == RegistrationStatus.PendingAdvisorApproval))
            .ToListAsync(ct);

        foreach (var reg in pendingRegistrations)
        {
            reg.Status = RegistrationStatus.Dropped;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        var activeCount = await _context.CourseRegistrations
            .CountAsync(cr => cr.CourseSectionId == sectionId && cr.Status == RegistrationStatus.Active, ct);
        if (activeCount > 0)
            throw new InvalidOperationException($"Aktif kayıtlı {activeCount} öğrenci bulunan şube kapatılamaz.");

        var hasOtherActiveSections = await _context.CourseSections
            .AsNoTracking()
            .AnyAsync(cs =>
                cs.CourseId == section.CourseId
                && cs.TermId == section.TermId
                && cs.Status == CourseSectionStatus.Active
                && cs.Id != section.Id, ct);

        section.Status = CourseSectionStatus.Cancelled;
        section.EnrolledCount = 0;

        if (!hasOtherActiveSections)
        {
            var accessPolicies = await _context.CourseAccessPolicies
                .Where(cap => cap.CourseId == section.CourseId && cap.TermId == section.TermId)
                .ToListAsync(ct);
            _context.CourseAccessPolicies.RemoveRange(accessPolicies);
        }

        await _context.SaveChangesAsync(ct);
        _forecastCacheInvalidator.InvalidateForecastContext();
    }

    public async Task<CloseAllSectionsResultDto> CloseAllSectionsAsync(int courseId, int termId, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (term.IsCancelled)
            throw new InvalidOperationException("Ders kapatmak için dönem iptal edilmemiş olmalıdır.");

        var sections = await _context.CourseSections
            .Where(cs => cs.CourseId == courseId && cs.TermId == termId && cs.Status == CourseSectionStatus.Active)
            .ToListAsync(ct);

        if (sections.Count == 0)
            throw new InvalidOperationException("Bu ders için kapatılacak aktif şube bulunamadı.");

        var sectionIds = sections.Select(s => s.Id).ToList();
        var activeCount = await _context.CourseRegistrations
            .CountAsync(cr => sectionIds.Contains(cr.CourseSectionId) && cr.Status == RegistrationStatus.Active, ct);
        if (activeCount > 0)
            throw new InvalidOperationException($"Toplam {activeCount} aktif kayıtlı öğrenci bulunan ders kapatılamaz. Önce öğrencileri başka şubelere aktarın veya kayıtlarını düşürün.");

        var pendingRegistrations = await _context.CourseRegistrations
            .Where(cr => sectionIds.Contains(cr.CourseSectionId)
                && (cr.Status == RegistrationStatus.Draft || cr.Status == RegistrationStatus.PendingAdvisorApproval))
            .ToListAsync(ct);

        foreach (var reg in pendingRegistrations)
        {
            reg.Status = RegistrationStatus.Dropped;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var section in sections)
        {
            section.Status = CourseSectionStatus.Cancelled;
            section.EnrolledCount = 0;
        }

        var accessPolicies = await _context.CourseAccessPolicies
            .Where(cap => cap.CourseId == courseId && cap.TermId == termId)
            .ToListAsync(ct);
        _context.CourseAccessPolicies.RemoveRange(accessPolicies);

        await _context.SaveChangesAsync(ct);
        _forecastCacheInvalidator.InvalidateForecastContext();

        return new CloseAllSectionsResultDto(
            ClosedSectionCount: sections.Count,
            DroppedRegistrationCount: pendingRegistrations.Count
        );
    }

    public async Task ReactivateSectionAsync(int sectionId, CancellationToken ct = default)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, ct)
            ?? throw new InvalidOperationException("Şube bulunamadı.");

        if (section.IsActive)
            throw new InvalidOperationException("Şube zaten aktif.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.IsCancelled)
            throw new InvalidOperationException("Şube yeniden açmak için dönem iptal edilmemiş olmalıdır.");

        if (section.InstructorId.HasValue)
        {
            await ValidateInstructorWeeklyLoadLimitAsync(
                section.InstructorId.Value,
                section.TermId,
                section.Course.TheoryHours + section.Course.LabHours,
                ct);
        }

        section.Status = CourseSectionStatus.Active;

        var hasAccessPolicy = await _context.CourseAccessPolicies
            .AnyAsync(cap => cap.CourseId == section.CourseId && cap.TermId == section.TermId, ct);

        if (!hasAccessPolicy && section.Course.DepartmentId.HasValue)
        {
            var deptIds = await _context.CurriculumCourses
                .Include(cc => cc.Curriculum)
                .ThenInclude(c => c.Program)
                .Where(cc => cc.CourseId == section.CourseId)
                .Select(cc => cc.Curriculum.Program.DepartmentId)
                .Distinct()
                .ToListAsync(ct);

            if (deptIds.Count == 0)
                deptIds.Add(section.Course.DepartmentId.Value);

            foreach (var deptId in deptIds)
            {
                _context.CourseAccessPolicies.Add(new CourseAccessPolicy
                {
                    CourseId = section.CourseId,
                    TermId = section.TermId,
                    DepartmentId = deptId,
                });
            }
        }

        await _context.SaveChangesAsync(ct);
        _forecastCacheInvalidator.InvalidateForecastContext();
    }

    private async Task ValidateInstructorWeeklyLoadLimitAsync(
        int instructorId,
        int termId,
        int weeklyHoursToAdd,
        CancellationToken ct,
        int? excludedSectionId = null)
    {
        if (weeklyHoursToAdd <= 0)
            return;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instructorId, ct)
            ?? throw new InvalidOperationException("Atanan öğretim üyesi bulunamadı.");

        var weeklyHoursPolicy = await InstructorWeeklyHoursPolicy.LoadAsync(_systemConfig, ct);
        var effectiveMaxWeeklyHours = weeklyHoursPolicy.Resolve(instructor.Title, instructor.MaxWeeklyHours);

        var assignedWeeklyHours = await _context.CourseSections
            .AsNoTracking()
            .Where(cs =>
                cs.InstructorId == instructorId
                && cs.TermId == termId
                && cs.Status == CourseSectionStatus.Active
                && (!excludedSectionId.HasValue || cs.Id != excludedSectionId.Value))
            .SumAsync(cs => cs.Course.TheoryHours + cs.Course.LabHours, ct);

        var projectedWeeklyHours = assignedWeeklyHours + weeklyHoursToAdd;
        if (projectedWeeklyHours > effectiveMaxWeeklyHours)
        {
            throw new InvalidOperationException(
                $"Ogretim uyesi haftalik ders saati limiti asiliyor ({projectedWeeklyHours}/{effectiveMaxWeeklyHours} saat).");
        }
    }
    /// <summary>
    /// Bir dersin tüm şubelerini DB'den fiziksel siler (hard delete).
    /// CloseAllSectionsAsync'in aksine geri alınamaz; ders "hiç açılmamış gibi" olur.
    /// </summary>
    public async Task<DeleteAllSectionsResultDto> DeleteAllSectionsHardAsync(int courseId, int termId, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönemde silme yapılamaz.");

        // Tüm şubeleri (Aktif + Cancelled dahil) topla — komple temizlik amaçlı
        var sections = await _context.CourseSections
            .Where(cs => cs.CourseId == courseId && cs.TermId == termId)
            .ToListAsync(ct);

        if (sections.Count == 0)
            throw new InvalidOperationException("Bu ders için silinecek şube bulunamadı.");

        var sectionIds = sections.Select(s => s.Id).ToList();

        // Safety 1: Aktif kayıtlı öğrenci varsa sil
        var activeRegistrationCount = await _context.CourseRegistrations
            .CountAsync(cr => sectionIds.Contains(cr.CourseSectionId)
                && cr.Status == RegistrationStatus.Active, ct);
        if (activeRegistrationCount > 0)
            throw new InvalidOperationException(
                $"Bu derste {activeRegistrationCount} aktif kayıtlı öğrenci var. Önce öğrencilerin kaydı düşürülmeli.");

        // Safety 2: Tamamlanmış (Completed) kayıtlar geçmiş not olarak korunmalı — silinmez
        var completedRegistrationCount = await _context.CourseRegistrations
            .CountAsync(cr => sectionIds.Contains(cr.CourseSectionId)
                && cr.Status == RegistrationStatus.Completed, ct);
        if (completedRegistrationCount > 0)
            throw new InvalidOperationException(
                $"Bu derste {completedRegistrationCount} tamamlanmış öğrenci kaydı var (transcript'te görünür) — silinemez.");

        // Safety 3: Ders değerlendirmesi yapılmış mı? (öğrenci ders evaluation'ı)
        var evaluationCount = await _context.EvaluationResponses
            .CountAsync(er => sectionIds.Contains(er.CourseSectionId), ct);
        if (evaluationCount > 0)
            throw new InvalidOperationException(
                $"Bu ders için {evaluationCount} adet ders değerlendirmesi yapılmış — silinemez.");

        // Safety 4: Yoklama oturumu kaydı varsa sil (geçmiş yoklama silinmemeli)
        var attendanceSessionCount = await _context.AttendanceSessions
            .CountAsync(asn => sectionIds.Contains(asn.CourseSectionId), ct);
        if (attendanceSessionCount > 0)
            throw new InvalidOperationException(
                $"Bu derste {attendanceSessionCount} yoklama oturumu kaydı var — silinemez.");

        // Hard-delete: Restrict FK olan satırları manuel temizle
        var allRegistrations = await _context.CourseRegistrations
            .Where(cr => sectionIds.Contains(cr.CourseSectionId))
            .ToListAsync(ct);
        _context.CourseRegistrations.RemoveRange(allRegistrations);

        // SchedulingAuditLog (Restrict FK) — log kayıtları silinir, geçmiş audit'i kaybederiz ama bu kabul edilebilir
        var auditLogs = await _context.SchedulingAuditLogs
            .Where(log => sectionIds.Contains(log.SectionId))
            .ToListAsync(ct);
        _context.SchedulingAuditLogs.RemoveRange(auditLogs);

        // Course-level CourseAccessPolicy temizliği
        var accessPolicies = await _context.CourseAccessPolicies
            .Where(cap => cap.CourseId == courseId && cap.TermId == termId)
            .ToListAsync(ct);
        _context.CourseAccessPolicies.RemoveRange(accessPolicies);

        // CourseSection silinince:
        //   - SectionSchedule (Cascade) → otomatik silinir
        //   - AttendanceSession (Cascade) → safety 4 zaten 0 olduğunu garanti etti
        //   - ConstraintEvaluation (SetNull) → otomatik null'lanır
        //   - ConstraintException (SetNull) → otomatik null'lanır
        //   - DistributionConstraintCourse (SetNull) → otomatik null'lanır
        _context.CourseSections.RemoveRange(sections);

        await _context.SaveChangesAsync(ct);
        _forecastCacheInvalidator.InvalidateForecastContext();

        return new DeleteAllSectionsResultDto(
            DeletedSectionCount: sections.Count,
            DeletedRegistrationCount: allRegistrations.Count,
            DeletedAccessPolicyCount: accessPolicies.Count
        );
    }

    public async Task<AutoSplitCourseResultDto> AutoSplitCourseAsync(AutoSplitCourseRequest request, CancellationToken ct = default)
    {
        var maxSections = await _systemConfig.GetIntAsync(MaxSectionsKey, MaxSectionsFallback, ct);
        if (request.TargetSectionCount <= 0 || request.TargetSectionCount > maxSections)
            throw new InvalidOperationException($"Hedef şube sayısı 1 ile {maxSections} arasında olmalıdır.");

        if (request.Strategy == AutoSplitStrategy.ModOddEven && request.TargetSectionCount != 2)
            throw new InvalidOperationException("MOD Tek/Çift stratejisi yalnızca 2 şube ile kullanılır.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TermId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (term.IsCancelled)
            throw new InvalidOperationException("Şubelendirme için dönem iptal edilmemiş olmalıdır.");

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, ct)
            ?? throw new InvalidOperationException("Ders bulunamadı.");

        var allSections = await _context.CourseSections
            .Where(cs => cs.CourseId == request.CourseId && cs.TermId == request.TermId)
            .OrderBy(cs => cs.SectionNumber)
            .ToListAsync(ct);

        var activeSections = allSections.Where(s => s.Status == CourseSectionStatus.Active).ToList();

        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.Student)
            .Where(cr => cr.Status == RegistrationStatus.Active
                && cr.CourseSection.CourseId == request.CourseId
                && cr.CourseSection.TermId == request.TermId)
            .ToListAsync(ct);

        // Hedef şube listesi (mevcut aktif + gerekirse yeni)
        var k = request.TargetSectionCount;
        var n = activeSections.Count;

        var defaultCapacity = request.DefaultCapacityPerSection
            ?? (activeSections.Count > 0
                ? (int)Math.Round(activeSections.Average(s => (double)s.Capacity))
                : 40);
        if (defaultCapacity <= 0) defaultCapacity = 40;

        var targetSections = new List<CourseSection>();
        var createdCount = 0;
        var closedCount = 0;

        if (k >= n)
        {
            targetSections.AddRange(activeSections);
            var maxSectionNumber = allSections.Count > 0 ? allSections.Max(s => s.SectionNumber) : 0;
            for (var i = 0; i < k - n; i++)
            {
                var newSection = new CourseSection
                {
                    CourseId = request.CourseId,
                    TermId = request.TermId,
                    SectionNumber = maxSectionNumber + i + 1,
                    Capacity = defaultCapacity,
                    InternalQuota = null,
                    ExternalQuota = null,
                    InstructorId = null,
                    EnrolledCount = 0,
                    Status = CourseSectionStatus.Active,
                    IsFinalized = false,
                };
                _context.CourseSections.Add(newSection);
                targetSections.Add(newSection);
                createdCount++;
            }
        }
        else
        {
            // K < N: en az öğrenciye sahip (N-K) şube boşaltılıp Closed yapılır
            var ordered = activeSections
                .OrderBy(s => registrations.Count(r => r.CourseSectionId == s.Id))
                .ThenByDescending(s => s.SectionNumber)
                .ToList();

            var toClose = ordered.Take(n - k).ToList();
            targetSections.AddRange(activeSections.Except(toClose).OrderBy(s => s.SectionNumber));

            // Kapatılacak şubelerdeki Draft/PendingAdvisorApproval kayıtları Dropped'a çek
            var closingSectionIds = toClose.Select(s => s.Id).ToList();
            var pendingRegs = await _context.CourseRegistrations
                .Where(cr => closingSectionIds.Contains(cr.CourseSectionId)
                    && (cr.Status == RegistrationStatus.Draft || cr.Status == RegistrationStatus.PendingAdvisorApproval))
                .ToListAsync(ct);
            foreach (var reg in pendingRegs)
            {
                reg.Status = RegistrationStatus.Dropped;
                reg.UpdatedAt = DateTime.UtcNow;
            }

            foreach (var section in toClose)
            {
                section.Status = CourseSectionStatus.Cancelled;
                section.EnrolledCount = 0;
                closedCount++;
            }
        }

        await _context.SaveChangesAsync(ct);

        // ── Dağıtım ──
        var redistributed = DistributeRegistrations(registrations, targetSections, request.Strategy);

        await _context.SaveChangesAsync(ct);
        _forecastCacheInvalidator.InvalidateForecastContext();

        var summaries = targetSections
            .OrderBy(s => s.SectionNumber)
            .Select(s => new AutoSplitSectionSummary(
                s.Id,
                s.SectionNumber,
                registrations.Count(r => r.CourseSectionId == s.Id)))
            .ToList();

        return new AutoSplitCourseResultDto(
            FinalSectionCount: targetSections.Count,
            CreatedSectionCount: createdCount,
            ClosedSectionCount: closedCount,
            RedistributedRegistrationCount: redistributed,
            Sections: summaries);
    }

    private static int DistributeRegistrations(
        List<CourseRegistration> registrations,
        List<CourseSection> targetSections,
        AutoSplitStrategy strategy)
    {
        if (registrations.Count == 0 || targetSections.Count == 0) return 0;
        var sorted = targetSections.OrderBy(s => s.SectionNumber).ToList();

        switch (strategy)
        {
            case AutoSplitStrategy.Equal:
                {
                    var ordered = registrations.OrderBy(r => r.Student.StudentNumber).ToList();
                    for (var i = 0; i < ordered.Count; i++)
                        ordered[i].CourseSectionId = sorted[i % sorted.Count].Id;
                    return ordered.Count;
                }
            case AutoSplitStrategy.EqualRepeat:
                {
                    var repeaters = registrations
                        .Where(r => r.IsRepeated)
                        .OrderBy(r => r.Student.StudentNumber)
                        .ToList();
                    var normals = registrations
                        .Where(r => !r.IsRepeated)
                        .OrderBy(r => r.Student.StudentNumber)
                        .ToList();

                    var idx = 0;
                    foreach (var r in repeaters)
                    {
                        r.CourseSectionId = sorted[idx % sorted.Count].Id;
                        idx++;
                    }
                    foreach (var r in normals)
                    {
                        r.CourseSectionId = sorted[idx % sorted.Count].Id;
                        idx++;
                    }
                    return repeaters.Count + normals.Count;
                }
            case AutoSplitStrategy.ModOddEven:
                {
                    // sorted.Count == 2 (validated at entry)
                    var first = sorted[0].Id;
                    var second = sorted[1].Id;
                    foreach (var r in registrations)
                    {
                        var lastChar = r.Student.StudentNumber.Length > 0
                            ? r.Student.StudentNumber[^1]
                            : '0';
                        var isEven = char.IsDigit(lastChar) && (lastChar - '0') % 2 == 0;
                        r.CourseSectionId = isEven ? first : second;
                    }
                    return registrations.Count;
                }
            default:
                throw new InvalidOperationException($"Bilinmeyen strateji: {strategy}");
        }
    }

    public async Task<TransferStudentsToSectionResultDto> TransferStudentsBetweenSectionsAsync(
        TransferStudentsToSectionRequest request,
        CancellationToken ct = default)
    {
        if (request.SourceSectionId == request.TargetSectionId)
            throw new InvalidOperationException("Kaynak ve hedef şube aynı olamaz.");

        if (request.StudentIds.Count == 0)
            throw new InvalidOperationException("Taşınacak öğrenci seçilmedi.");

        var sourceSection = await _context.CourseSections
            .FirstOrDefaultAsync(cs => cs.Id == request.SourceSectionId, ct)
            ?? throw new InvalidOperationException("Kaynak şube bulunamadı.");

        var targetSection = await _context.CourseSections
            .FirstOrDefaultAsync(cs => cs.Id == request.TargetSectionId, ct)
            ?? throw new InvalidOperationException("Hedef şube bulunamadı.");

        if (sourceSection.CourseId != targetSection.CourseId)
            throw new InvalidOperationException("Şubeler aynı derse ait olmalıdır.");

        if (sourceSection.TermId != targetSection.TermId)
            throw new InvalidOperationException("Şubeler aynı döneme ait olmalıdır.");

        if (targetSection.Status != CourseSectionStatus.Active)
            throw new InvalidOperationException("Hedef şube aktif değil.");

        var registrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == request.SourceSectionId
                && cr.Status == RegistrationStatus.Active
                && request.StudentIds.Contains(cr.StudentId))
            .ToListAsync(ct);

        if (registrations.Count == 0)
            throw new InvalidOperationException("Taşınabilir öğrenci kaydı bulunamadı.");

        var currentTargetActive = await _context.CourseRegistrations
            .CountAsync(cr => cr.CourseSectionId == request.TargetSectionId
                && cr.Status == RegistrationStatus.Active, ct);

        if (currentTargetActive + registrations.Count > targetSection.Capacity)
        {
            var available = Math.Max(0, targetSection.Capacity - currentTargetActive);
            throw new InvalidOperationException(
                $"Hedef şubenin yeterli kapasitesi yok. Mevcut: {currentTargetActive}/{targetSection.Capacity}, " +
                $"taşınması istenen: {registrations.Count}, kalan kapasite: {available}.");
        }

        foreach (var registration in registrations)
        {
            registration.CourseSectionId = request.TargetSectionId;
        }

        await _context.SaveChangesAsync(ct);
        _forecastCacheInvalidator.InvalidateForecastContext();

        return new TransferStudentsToSectionResultDto(
            RequestedCount: request.StudentIds.Count,
            TransferredCount: registrations.Count,
            TransferredStudentIds: registrations.Select(r => r.StudentId).ToList());
    }
}
