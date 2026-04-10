using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class CourseOpeningService(
    AppDbContext context,
    IEnrollmentForecastService forecastService,
    IEnrollmentForecastCacheInvalidator forecastCacheInvalidator) : ICourseOpeningService
{
    private readonly AppDbContext _context = context;
    private readonly IEnrollmentForecastService _forecastService = forecastService;
    private readonly IEnrollmentForecastCacheInvalidator _forecastCacheInvalidator = forecastCacheInvalidator;

    public async Task<OpenedCourseListDto> GetOpenedCoursesAsync(int departmentId, int termId, CancellationToken ct)
    {
        var department = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == departmentId, ct)
            ?? throw new InvalidOperationException("Bölüm bulunamadı.");
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == termId && cs.Course.DepartmentId == departmentId)
            .OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.Id)
            .ToListAsync(ct);

        // Load instructor user names
        var instructorIds = sections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct().ToList();
        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, ct);

        // Load registered counts (Status != Dropped && Status != Draft)
        var sectionIds = sections.Select(s => s.Id).ToList();
        var registeredCounts = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r => sectionIds.Contains(r.CourseSectionId) && r.Status != RegistrationStatus.Dropped && r.Status != RegistrationStatus.Draft)
            .GroupBy(r => r.CourseSectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

        var draftOrPendingCounts = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r =>
                sectionIds.Contains(r.CourseSectionId)
                && (r.Status == RegistrationStatus.Draft || r.Status == RegistrationStatus.PendingAdvisorApproval))
            .GroupBy(r => r.CourseSectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

        // Load suggested semester from curriculum
        var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var suggestedSemesters = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId) && cc.Curriculum.Program.DepartmentId == departmentId && cc.Curriculum.IsActive)
            .GroupBy(cc => cc.CourseId)
            .Select(g => new { CourseId = g.Key, Semester = g.Min(cc => cc.SuggestedSemester) })
            .ToDictionaryAsync(x => x.CourseId, x => x.Semester, ct);

        // Compute section numbers per course
        var sectionNumberMap = new Dictionary<int, int>();
        var courseGroupCounters = new Dictionary<int, int>();
        foreach (var s in sections)
        {
            if (!courseGroupCounters.TryGetValue(s.CourseId, out var counter))
                counter = 0;
            counter++;
            courseGroupCounters[s.CourseId] = counter;
            sectionNumberMap[s.Id] = counter;
        }

        var dtos = sections.Select(s =>
        {
            instructorNameMap.TryGetValue(s.InstructorId ?? 0, out var instrName);
            registeredCounts.TryGetValue(s.Id, out var regCount);
            draftOrPendingCounts.TryGetValue(s.Id, out var draftOrPendingCount);
            suggestedSemesters.TryGetValue(s.CourseId, out var sugSem);

            return new OpenedCourseSectionDto
            {
                SectionId = s.Id,
                SectionNumber = sectionNumberMap[s.Id],
                CourseId = s.CourseId,
                CourseCode = s.Course.Code,
                CourseName = s.Course.Name,
                TheoryHours = s.Course.TheoryHours,
                LabHours = s.Course.LabHours,
                Credits = s.Course.TheoryHours + s.Course.LabHours,
                Ects = s.Course.Ects,
                SuggestedSemester = sugSem,
                Capacity = s.Capacity,
                EnrolledCount = s.EnrolledCount,
                RegisteredCount = regCount,
                DraftOrPendingCount = draftOrPendingCount,
                InstructorId = s.InstructorId,
                InstructorName = instrName,
                IsActive = s.IsActive,
            };
        }).ToList();

        return new OpenedCourseListDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TermId = termId,
            TermName = term.Name,
            TotalSections = dtos.Count,
            Sections = dtos,
        };
    }

    public async Task<List<CurriculumCourseForOpeningDto>> GetCurriculumCoursesForOpeningAsync(int departmentId, int termId, bool showAll = false, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct);
        var semesterParity = term?.Semester == SemesterType.Spring ? 0 : 1; // Spring→even(2,4,6,8), Fall→odd(1,3,5,7)

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Where(cc => cc.Curriculum.IsActive && cc.Curriculum.Program.DepartmentId == departmentId)
            .Where(cc => showAll || term == null || term.Semester == SemesterType.Summer || cc.SuggestedSemester % 2 == semesterParity)
            .OrderBy(cc => cc.SuggestedSemester).ThenBy(cc => cc.Course.Code)
            .ToListAsync(ct);

        var courseIds = curriculumCourses.Select(cc => cc.CourseId).Distinct().ToList();
        var existingSectionCounts = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == termId && cs.IsActive && courseIds.Contains(cs.CourseId))
            .GroupBy(cs => cs.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct);

        var forecastSummary = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        var forecastMap = (forecastSummary?.CourseSummaries ?? [])
            .Where(summary => courseIds.Contains(summary.CourseId))
            .ToDictionary(summary => summary.CourseId);

        var approvals = await _context.CourseSectionApprovals
            .AsNoTracking()
            .Where(approval =>
                approval.DepartmentId == departmentId
                && approval.TermId == termId
                && courseIds.Contains(approval.CourseId))
            .Select(approval => new CourseOpeningApprovalInfo
            {
                CourseId = approval.CourseId,
                Status = approval.Status,
                ApprovedSectionCount = approval.ApprovedSectionCount,
                Note = approval.Note
            })
            .ToDictionaryAsync(approval => approval.CourseId, ct);

        return curriculumCourses
            .GroupBy(cc => cc.CourseId)
            .Select(g =>
            {
                var first = g.First();
                existingSectionCounts.TryGetValue(first.CourseId, out var sectionCount);
                forecastMap.TryGetValue(first.CourseId, out var forecast);
                approvals.TryGetValue(first.CourseId, out var approval);

                var approvedSectionCount = approval?.ApprovedSectionCount;
                var remainingApprovedSections = approval?.Status == SectionApprovalStatus.Approved && approvedSectionCount.HasValue
                    ? Math.Max(0, approvedSectionCount.Value - sectionCount)
                    : 0;
                var workflowStatus = ResolveApprovalWorkflowStatus(approval?.Status, approvedSectionCount, sectionCount);
                var blockedReason = BuildOpeningBlockedReason(approval?.Status, approvedSectionCount, sectionCount, approval?.Note);

                return new CurriculumCourseForOpeningDto
                {
                    CourseId = first.CourseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    TheoryHours = first.Course.TheoryHours,
                    LabHours = first.Course.LabHours,
                    Ects = first.Course.Ects,
                    SuggestedSemester = first.SuggestedSemester,
                    IsCompulsory = first.IsCompulsory,
                    AlreadyOpened = sectionCount > 0,
                    ExistingSectionCount = sectionCount,
                    PredictedDemand = forecast?.TotalPredictedDemand ?? 0,
                    RetakeCount = forecast?.DemandBreakdown.TotalRetake ?? 0,
                    RecommendedSectionCount = forecast?.RecommendedSectionCount ?? 0,
                    RecommendedCapacity = forecast?.SectionDistribution.EffectiveCapacityPerSection ?? 0,
                    CrossDepartmentDemand = (forecast?.CrossDepartmentDemand ?? [])
                        .Select(item => new CrossDepartmentDemandDto
                        {
                            DepartmentId = item.DepartmentId,
                            DepartmentName = item.DepartmentName,
                            ProgramName = item.ProgramName,
                            EstimatedStudentCount = item.EstimatedStudentCount,
                        })
                        .ToList(),
                    PreviousTermEnrollment = forecast?.ComparableTermEnrollment,
                    ApprovalStatus = approval?.Status,
                    ApprovalWorkflowStatus = workflowStatus,
                    ApprovedSectionCount = approvedSectionCount,
                    OpenedSectionCount = sectionCount,
                    RemainingApprovedSectionCount = remainingApprovedSections,
                    CanOpen = approval?.Status == SectionApprovalStatus.Approved && remainingApprovedSections > 0,
                    OpeningBlockedReason = blockedReason,
                };
            })
            .OrderBy(c => c.SuggestedSemester).ThenBy(c => c.CourseCode)
            .ToList();
    }

    public async Task<List<InstructorOptionDto>> GetDepartmentInstructorsAsync(int departmentId, CancellationToken ct)
    {
        // Include instructors from all departments in the same faculty,
        // so shared-course departments (e.g. BMTR, BSM, YM) can see BM instructors
        var facultyId = await _context.Departments
            .AsNoTracking()
            .Where(d => d.Id == departmentId)
            .Select(d => d.FacultyId)
            .FirstOrDefaultAsync(ct);

        var instructors = await _context.Instructors
            .AsNoTracking()
            .Where(i => i.Department.FacultyId == facultyId)
            .Select(i => new { i.Id, i.Title, i.UserId, i.DepartmentId })
            .ToListAsync(ct);

        var userIds = instructors.Select(i => i.UserId).Distinct().ToList();
        var userMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FirstName, u.LastName }, ct);

        // Load department names for instructors from other departments
        var deptIds = instructors.Select(i => i.DepartmentId).Distinct().ToList();
        var deptNameMap = await _context.Departments
            .AsNoTracking()
            .Where(d => deptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Code, ct);

        return instructors.Select(i =>
        {
            var baseName = userMap.TryGetValue(i.UserId, out var u) ? $"{i.Title} {u.FirstName} {u.LastName}" : $"{i.Title} (#{i.Id})";
            // Append department code for instructors from other departments
            var name = i.DepartmentId != departmentId && deptNameMap.TryGetValue(i.DepartmentId, out var deptCode)
                ? $"{baseName} ({deptCode})"
                : baseName;
            return new InstructorOptionDto { InstructorId = i.Id, FullName = name };
        })
        .OrderByDescending(i => instructors.First(x => x.Id == i.InstructorId).DepartmentId == departmentId ? 1 : 0)
        .ThenBy(i => i.FullName)
        .ToList();
    }

    public async Task<List<OpenedCourseSectionDto>> OpenCourseAsync(int departmentId, int termId, OpenCourseRequest request, CancellationToken ct)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (term.Status != TermStatus.Planning)
            throw new InvalidOperationException(
                $"Ders açmak için dönem 'Planlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, ct)
            ?? throw new InvalidOperationException($"Ders bulunamadı (ID: {request.CourseId}).");

        if (course.DepartmentId.HasValue && course.DepartmentId != departmentId)
            throw new InvalidOperationException($"Ders '{course.Code}' bu bölüme ait değil.");

        // Kapasite validasyonu
        if (request.CapacityPerSection <= 0)
            throw new InvalidOperationException("Kontenjan 0'dan büyük olmalıdır.");

        // Şube sayısı üst sınırı
        if (request.SectionCount <= 0 || request.SectionCount > 20)
            throw new InvalidOperationException("Şube sayısı 1 ile 20 arasında olmalıdır.");

        var approval = await _context.CourseSectionApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.DepartmentId == departmentId
                && a.TermId == termId
                && a.CourseId == request.CourseId, ct);

        // Mevcut section'ları kontrol et (aktif/pasif fark etmez, SectionNumber çakışmasını önlemek için)
        var existingSections = await _context.CourseSections
            .Where(cs => cs.CourseId == request.CourseId && cs.TermId == termId)
            .ToListAsync(ct);

        var activeCount = existingSections.Count(s => s.IsActive);

        ValidateOpeningApproval(approval, activeCount, request.SectionCount, course.Code);

        // Mevcut en yüksek SectionNumber (approve'dan gelen pasif section'lar olabilir)
        var maxSectionNumber = existingSections.Count > 0
            ? existingSections.Max(s => s.SectionNumber)
            : 0;

        if (request.InstructorId.HasValue)
            await ValidateInstructorAssignmentAsync(request.InstructorId.Value, course.DepartmentId, ct);

        var created = new List<CourseSection>();
        for (var i = 0; i < request.SectionCount; i++)
        {
            var section = new CourseSection
            {
                CourseId = request.CourseId,
                TermId = termId,
                SectionNumber = maxSectionNumber + i + 1,
                Capacity = request.CapacityPerSection,
                InstructorId = request.InstructorId,
                EnrolledCount = 0,
                IsActive = true,
                IsFinalized = false,
            };
            _context.CourseSections.Add(section);
            created.Add(section);
        }
        await _context.SaveChangesAsync(ct);

        var existingPolicies = await _context.CourseAccessPolicies
            .Where(cap => cap.CourseId == request.CourseId && cap.TermId == termId)
            .ToListAsync(ct);

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

        // Resolve instructor name if set
        string? instructorName = null;
        if (request.InstructorId.HasValue)
        {
            var nameMap = await LoadInstructorNameMapAsync([request.InstructorId.Value], ct);
            nameMap.TryGetValue(request.InstructorId.Value, out instructorName);
        }

        // Get suggested semester
        var sugSem = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CourseId == request.CourseId && cc.Curriculum.IsActive && cc.Curriculum.Program.DepartmentId == departmentId)
            .Select(cc => (int?)cc.SuggestedSemester)
            .MinAsync(ct) ?? 0;

        return created.Select(s =>
        {
            return new OpenedCourseSectionDto
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
                EnrolledCount = 0,
                RegisteredCount = 0,
                DraftOrPendingCount = 0,
                InstructorId = s.InstructorId,
                InstructorName = instructorName,
                IsActive = true,
            };
        }).ToList();
    }

    public async Task<BulkOpenCoursesResultDto> BulkOpenCoursesAsync(int departmentId, int termId, BulkOpenCoursesRequest request, CancellationToken ct)
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
                result.TotalCoursesOpened++;
                result.TotalSectionsCreated += created.Count;
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

    public async Task<OpenedCourseSectionDto> UpdateSectionAsync(UpdateOpenedSectionRequest request, CancellationToken ct)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct)
            ?? throw new InvalidOperationException("Şube bulunamadı.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.Status != TermStatus.Planning && term.Status != TermStatus.Scheduling)
            throw new InvalidOperationException(
                $"Şube güncellemek için dönem 'Planlama' veya 'Programlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        if (!section.IsActive)
            throw new InvalidOperationException("Pasif şube güncellenemez.");

        var registeredCount = await GetRegisteredCountAsync(section.Id, ct);
        var draftOrPendingCount = await GetDraftOrPendingCountAsync(section.Id, ct);

        if (request.InstructorId.HasValue)
        {
            await ValidateInstructorAssignmentAsync(request.InstructorId.Value, section.Course.DepartmentId, ct);
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

        await _context.SaveChangesAsync(ct);
        _forecastCacheInvalidator.InvalidateForecastContext();

        // Build response
        string? instructorName = null;
        if (section.InstructorId.HasValue)
        {
            var nameMap = await LoadInstructorNameMapAsync([section.InstructorId.Value], ct);
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
            EnrolledCount = section.EnrolledCount,
            RegisteredCount = registeredCount,
            DraftOrPendingCount = draftOrPendingCount,
            InstructorId = section.InstructorId,
            InstructorName = instructorName,
            IsActive = section.IsActive,
        };
    }

    public async Task CloseSectionAsync(int sectionId, CancellationToken ct)
    {
        var section = await _context.CourseSections.FirstOrDefaultAsync(cs => cs.Id == sectionId, ct)
            ?? throw new InvalidOperationException("Şube bulunamadı.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.Status != TermStatus.Planning && term.Status != TermStatus.Scheduling)
            throw new InvalidOperationException(
                $"Şube kapatmak için dönem 'Planlama' veya 'Programlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        // Draft/PendingAdvisorApproval kayıtlarını temizle
        var pendingRegistrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && (cr.Status == RegistrationStatus.Draft || cr.Status == RegistrationStatus.PendingAdvisorApproval))
            .ToListAsync(ct);

        foreach (var reg in pendingRegistrations)
        {
            reg.Status = RegistrationStatus.Dropped;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        // Aktif kayıtlı öğrenci varsa kapatılamaz
        var activeCount = await _context.CourseRegistrations
            .CountAsync(cr => cr.CourseSectionId == sectionId && cr.Status == RegistrationStatus.Active, ct);
        if (activeCount > 0)
            throw new InvalidOperationException($"Aktif kayıtlı {activeCount} öğrenci bulunan şube kapatılamaz.");

        var hasOtherActiveSections = await _context.CourseSections
            .AsNoTracking()
            .AnyAsync(cs =>
                cs.CourseId == section.CourseId
                && cs.TermId == section.TermId
                && cs.IsActive
                && cs.Id != section.Id, ct);

        section.IsActive = false;
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

    private static string GetTermStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Draft            => "Taslak",
        TermStatus.Planning         => "Planlama",
        TermStatus.Scheduling       => "Programlama",
        TermStatus.Published        => "Yayınlandı",
        TermStatus.RegistrationOpen => "Kayıt Açık",
        TermStatus.AddDrop          => "Ders Ekle/Bırak",
        TermStatus.WithdrawalPeriod => "Çekilme Dönemi",
        TermStatus.GradeEntry       => "Not Girişi",
        TermStatus.Closed           => "Kapalı",
        TermStatus.Archived         => "Arşivlendi",
        _                           => s.ToString()
    };

    private async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(IEnumerable<int> instructorIds, CancellationToken ct)
    {
        var ids = instructorIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var instructors = await _context.Instructors
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.Title, i.UserId })
            .ToListAsync(ct);

        var userIds = instructors.Select(i => i.UserId).Distinct().ToList();
        var userMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FirstName, u.LastName }, ct);

        return instructors.ToDictionary(
            i => i.Id,
            i => userMap.TryGetValue(i.UserId, out var u) ? $"{i.Title} {u.FirstName} {u.LastName}" : $"{i.Title} (#{i.Id})");
    }

    private async Task ValidateInstructorAssignmentAsync(int instructorId, int? owningDepartmentId, CancellationToken ct)
    {
        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instructorId, ct);

        if (instructor == null)
            throw new InvalidOperationException("Atanan öğretim üyesi bulunamadı.");

        if (owningDepartmentId.HasValue && instructor.DepartmentId != owningDepartmentId.Value)
            throw new InvalidOperationException("Öğretim üyesi bu bölüme ait değil.");
    }

    private Task<int> GetRegisteredCountAsync(int sectionId, CancellationToken ct)
    {
        return _context.CourseRegistrations.CountAsync(
            r => r.CourseSectionId == sectionId
                && r.Status != RegistrationStatus.Dropped
                && r.Status != RegistrationStatus.Draft,
            ct);
    }

    private Task<int> GetDraftOrPendingCountAsync(int sectionId, CancellationToken ct)
    {
        return _context.CourseRegistrations.CountAsync(
            r => r.CourseSectionId == sectionId
                && (r.Status == RegistrationStatus.Draft || r.Status == RegistrationStatus.PendingAdvisorApproval),
            ct);
    }

    private static string ResolveApprovalWorkflowStatus(
        SectionApprovalStatus? status,
        int? approvedSectionCount,
        int openedSectionCount)
    {
        if (status == SectionApprovalStatus.Rejected)
            return "Rejected";

        if (status != SectionApprovalStatus.Approved)
            return "Pending";

        var approvedCount = Math.Max(0, approvedSectionCount ?? 0);
        if (approvedCount == 0 || openedSectionCount <= 0)
            return "Approved";

        return openedSectionCount >= approvedCount
            ? "Completed"
            : "PartiallyOpened";
    }

    private static string? BuildOpeningBlockedReason(
        SectionApprovalStatus? status,
        int? approvedSectionCount,
        int openedSectionCount,
        string? approvalNote)
    {
        if (status == null)
            return "Bu ders için henüz şube onayı oluşturulmadı.";

        if (status == SectionApprovalStatus.Pending)
            return "Bu ders için bölüm onayı bekleniyor.";

        if (status == SectionApprovalStatus.Rejected)
            return string.IsNullOrWhiteSpace(approvalNote)
                ? "Bu ders bu dönem için reddedildi."
                : $"Bu ders bu dönem için reddedildi. Not: {approvalNote}";

        var approvedCount = Math.Max(0, approvedSectionCount ?? 0);
        if (approvedCount <= 0)
            return "Bu ders için açılabilir şube onayı bulunmuyor.";

        if (openedSectionCount >= approvedCount)
            return $"Onaylanan toplam {approvedCount} şube zaten açıldı.";

        return null;
    }

    private static void ValidateOpeningApproval(
        CourseSectionApproval? approval,
        int openedSectionCount,
        int requestedSectionCount,
        string courseCode)
    {
        var blockedReason = BuildOpeningBlockedReason(
            approval?.Status,
            approval?.ApprovedSectionCount,
            openedSectionCount,
            approval?.Note);

        if (blockedReason != null && approval?.Status != SectionApprovalStatus.Approved)
            throw new InvalidOperationException(blockedReason);

        if (approval?.Status != SectionApprovalStatus.Approved || !approval.ApprovedSectionCount.HasValue)
            throw new InvalidOperationException(blockedReason ?? "Bu ders için şube onayı bulunmuyor.");

        var remainingApprovedSections = Math.Max(0, approval.ApprovedSectionCount.Value - openedSectionCount);
        if (remainingApprovedSections <= 0)
            throw new InvalidOperationException(blockedReason ?? $"'{courseCode}' için açılabilir şube kotası kalmadı.");

        if (requestedSectionCount > remainingApprovedSections)
            throw new InvalidOperationException(
                $"'{courseCode}' için en fazla {remainingApprovedSections} ek şube açabilirsiniz. Onaylanan toplam şube: {approval.ApprovedSectionCount.Value}, halihazırda açık: {openedSectionCount}.");
    }

    private sealed class CourseOpeningApprovalInfo
    {
        public int CourseId { get; init; }
        public SectionApprovalStatus Status { get; init; }
        public int? ApprovedSectionCount { get; init; }
        public string? Note { get; init; }
    }
}
