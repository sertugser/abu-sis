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

        // Madde 13: Ortak dersleri de göster — bu bölümün sahip olduğu dersler veya
        // bu bölümün öğrencilerinin kayıtlanabileceği (CourseAccessPolicy) dersler
        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course).ThenInclude(c => c.Department)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == termId
                && (cs.Course.DepartmentId == departmentId
                    || _context.CourseAccessPolicies.Any(cap =>
                        cap.CourseId == cs.CourseId
                        && cap.TermId == termId
                        && cap.DepartmentId == departmentId)))
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
                InternalQuota = s.InternalQuota,
                ExternalQuota = s.ExternalQuota,
                EnrolledCount = s.EnrolledCount,
                RegisteredCount = regCount,
                DraftOrPendingCount = draftOrPendingCount,
                InstructorId = s.InstructorId,
                InstructorName = instrName,
                IsActive = s.IsActive,
                DepartmentId = s.Course.DepartmentId,
                DepartmentName = s.Course.Department?.Name,
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

        // 1) Müfredatta tek tek atanmış dersler (zorunlu + tekil seçmeli)
        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Where(cc => cc.Curriculum.IsActive && cc.Curriculum.Program.DepartmentId == departmentId)
            .Where(cc => showAll || term == null || term.Semester == SemesterType.Summer || cc.SuggestedSemester % 2 == semesterParity)
            .OrderBy(cc => cc.SuggestedSemester).ThenBy(cc => cc.Course.Code)
            .ToListAsync(ct);

        // 2) Müfredatın seçmeli havuzlarındaki dersler (CurriculumElectivePool → ElectiveGroup → ElectiveGroupCourse)
        // Havuzlar için dönem filtresi: aralık >=2 ise her zaman (her iki parite de vardır),
        // aralık ==1 ise sadece parite eşleşiyorsa dahil edilir.
        var pools = await _context.CurriculumElectivePools
            .AsNoTracking()
            .Include(p => p.ElectiveGroup)
                .ThenInclude(g => g.Courses)
                    .ThenInclude(egc => egc.Course)
            .Where(p => p.Curriculum.IsActive && p.Curriculum.Program.DepartmentId == departmentId)
            .Where(p => showAll
                     || term == null
                     || term.Semester == SemesterType.Summer
                     || (p.SuggestedSemesterEnd - p.SuggestedSemesterStart) >= 1
                     || p.SuggestedSemesterStart % 2 == semesterParity)
            .ToListAsync(ct);

        // Her havuz için üye dersleri düz listeye aç
        var poolCourseEntries = pools
            .SelectMany(p => p.ElectiveGroup.Courses.Select(egc => new
            {
                PoolId = p.Id,
                PoolStart = p.SuggestedSemesterStart,
                PoolEnd = p.SuggestedSemesterEnd,
                Group = p.ElectiveGroup,
                Course = egc.Course,
            }))
            .ToList();

        // 3) Toplam ilgili ders kümesi (ek veri sorguları için)
        var allCourseIds = curriculumCourses.Select(cc => cc.CourseId)
            .Concat(poolCourseEntries.Select(pce => pce.Course.Id))
            .Distinct()
            .ToList();

        var existingSectionCounts = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == termId && cs.IsActive && allCourseIds.Contains(cs.CourseId))
            .GroupBy(cs => cs.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct);

        var forecastSummary = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        var forecastMap = (forecastSummary?.CourseSummaries ?? [])
            .Where(summary => allCourseIds.Contains(summary.CourseId))
            .ToDictionary(summary => summary.CourseId);

        var isTermInPlanning = term?.Status == TermStatus.Planning;

        // 4) DTO üretimi — direkt atanmış dersler önce, sonra direkt'te olmayan havuz üyeleri
        var resultMap = new Dictionary<int, CurriculumCourseForOpeningDto>();

        foreach (var g in curriculumCourses.GroupBy(cc => cc.CourseId))
        {
            var first = g.First();
            existingSectionCounts.TryGetValue(first.CourseId, out var sectionCount);
            forecastMap.TryGetValue(first.CourseId, out var forecast);
            // Madde 12: "Sahipsiz ders herkes açabilir" deliği kapatıldı — sahiplik zorunlu
            var hasOwner = first.Course.DepartmentId.HasValue;
            var isOwner = hasOwner && first.Course.DepartmentId == departmentId;
            var blockReason = ResolveBlockReason(isTermInPlanning, hasOwner, isOwner);

            resultMap[first.CourseId] = new CurriculumCourseForOpeningDto
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
                CanOpen = blockReason is null,
                BlockReason = blockReason,
                Source = first.IsCompulsory ? "Compulsory" : "Elective",
            };
        }

        // Havuz üyelerini ekle — direkt atanmış derslerle çakışanları atla
        foreach (var pce in poolCourseEntries)
        {
            if (resultMap.ContainsKey(pce.Course.Id))
                continue; // Direkt atama öncelikli

            existingSectionCounts.TryGetValue(pce.Course.Id, out var sectionCount);
            forecastMap.TryGetValue(pce.Course.Id, out var forecast);
            // Madde 12: Havuzdaki ders başka bir birime ait olabilir; açma yetkisi sahibinindir
            var hasOwner = pce.Course.DepartmentId.HasValue;
            var isOwner = hasOwner && pce.Course.DepartmentId == departmentId;
            var blockReason = ResolveBlockReason(isTermInPlanning, hasOwner, isOwner);

            resultMap[pce.Course.Id] = new CurriculumCourseForOpeningDto
            {
                CourseId = pce.Course.Id,
                CourseCode = pce.Course.Code,
                CourseName = pce.Course.Name,
                TheoryHours = pce.Course.TheoryHours,
                LabHours = pce.Course.LabHours,
                Ects = pce.Course.Ects,
                SuggestedSemester = pce.PoolStart,
                IsCompulsory = false,
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
                CanOpen = blockReason is null,
                BlockReason = blockReason,
                ElectivePoolId = pce.PoolId,
                ElectiveGroupCode = pce.Group.Code,
                ElectiveGroupName = pce.Group.Name,
                ElectiveSubtype = pce.Group.ElectiveSubtype.ToString(),
                Source = "Pool",
            };
        }

        return resultMap.Values
            .OrderBy(c => c.SuggestedSemester).ThenBy(c => c.CourseCode)
            .ToList();
    }

    /// <summary>
    /// Ders açma engelinin sebebini belirler. Null dönerse ders açılabilir.
    /// Öncelik sırası: TermNotInPlanning > CourseUnassigned > NotOwner.
    /// </summary>
    private static string? ResolveBlockReason(bool isTermInPlanning, bool hasOwner, bool isOwner)
    {
        if (!isTermInPlanning) return "TermNotInPlanning";
        if (!hasOwner) return "CourseUnassigned";
        if (!isOwner) return "NotOwner";
        return null;
    }

    public async Task<List<InstructorOptionDto>> GetDepartmentInstructorsAsync(int departmentId, int? termId = null, CancellationToken ct = default)
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
            .Select(i => new { i.Id, i.Title, i.UserId, i.DepartmentId, i.MaxWeeklyHours })
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

        // Load workload summary if termId provided
        var workloadMap = new Dictionary<int, int>();
        if (termId.HasValue)
        {
            var instructorIds = instructors.Select(i => i.Id).ToList();
            workloadMap = await _context.CourseSections
                .AsNoTracking()
                .Where(cs => instructorIds.Contains(cs.InstructorId!.Value) && cs.TermId == termId.Value && cs.IsActive)
                .GroupBy(cs => cs.InstructorId!.Value)
                .Select(g => new { InstructorId = g.Key, TotalHours = g.Sum(cs => cs.Course.TheoryHours + cs.Course.LabHours) })
                .ToDictionaryAsync(x => x.InstructorId, x => x.TotalHours, ct);
        }

        return instructors.Select(i =>
        {
            var baseName = userMap.TryGetValue(i.UserId, out var u) ? $"{i.Title} {u.FirstName} {u.LastName}" : $"{i.Title} (#{i.Id})";
            // Append department code for instructors from other departments
            var name = i.DepartmentId != departmentId && deptNameMap.TryGetValue(i.DepartmentId, out var deptCode)
                ? $"{baseName} ({deptCode})"
                : baseName;
            workloadMap.TryGetValue(i.Id, out var assignedHours);
            return new InstructorOptionDto
            {
                InstructorId = i.Id,
                FullName = name,
                MaxWeeklyHours = i.MaxWeeklyHours,
                AssignedWeeklyHours = assignedHours,
            };
        })
        .OrderByDescending(i => instructors.First(x => x.Id == i.InstructorId).DepartmentId == departmentId ? 1 : 0)
        .ThenBy(i => i.FullName)
        .ToList();
    }

    public async Task<List<OpenedCourseSectionDto>> OpenCourseAsync(int departmentId, int termId, OpenCourseRequest request, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (term.Status != TermStatus.Planning)
            throw new InvalidOperationException(
                $"Ders açmak için dönem 'Planlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, ct)
            ?? throw new InvalidOperationException($"Ders bulunamadı (ID: {request.CourseId}).");

        // Madde 12: Sahiplik zorunlu — sahipsiz ders açılamaz
        if (!course.DepartmentId.HasValue)
            throw new InvalidOperationException($"Ders '{course.Code}' bir bölüme atanmamış; açılamaz. Önce bir birime atayın.");
        if (course.DepartmentId != departmentId)
            throw new InvalidOperationException($"Ders '{course.Code}' bu bölüme ait değil.");

        // Kapasite validasyonu
        if (request.CapacityPerSection <= 0)
            throw new InvalidOperationException("Kontenjan 0'dan büyük olmalıdır.");

        // Şube sayısı üst sınırı
        if (request.SectionCount <= 0 || request.SectionCount > 20)
            throw new InvalidOperationException("Şube sayısı 1 ile 20 arasında olmalıdır.");

        // Mevcut section'ları kontrol et (aktif/pasif fark etmez, SectionNumber çakışmasını önlemek için)
        var existingSections = await _context.CourseSections
            .Where(cs => cs.CourseId == request.CourseId && cs.TermId == termId)
            .ToListAsync(ct);

        // Mevcut en yüksek SectionNumber (approve'dan gelen pasif section'lar olabilir)
        var maxSectionNumber = existingSections.Count > 0
            ? existingSections.Max(s => s.SectionNumber)
            : 0;

        if (request.InstructorId.HasValue)
            await ValidateInstructorAssignmentAsync(request.InstructorId.Value, course.DepartmentId, ct);

        // Madde 14: Cross-list grup hazırlığı (IncludeCrossLists=true ise)
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
                IsActive = true,
                IsFinalized = false,
                CrossListGroupId = crossListGroupId,
            };
            _context.CourseSections.Add(section);
            created.Add(section);
        }

        // Cross-list'li dersler için paralel section'lar
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
                    IsActive = true,
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

        // Ana section DTO'ları
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

        // Madde 14: Cross-list linked section DTO'ları
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

        if (request.InternalQuota.HasValue)
            section.InternalQuota = request.InternalQuota.Value == 0 ? null : request.InternalQuota.Value;

        if (request.ExternalQuota.HasValue)
            section.ExternalQuota = request.ExternalQuota.Value == 0 ? null : request.ExternalQuota.Value;

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

    public async Task<CloseAllSectionsResultDto> CloseAllSectionsAsync(int courseId, int termId, CancellationToken ct)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (term.Status != TermStatus.Planning && term.Status != TermStatus.Scheduling)
            throw new InvalidOperationException(
                $"Ders kapatmak için dönem 'Planlama' veya 'Programlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        var sections = await _context.CourseSections
            .Where(cs => cs.CourseId == courseId && cs.TermId == termId && cs.IsActive)
            .ToListAsync(ct);

        if (sections.Count == 0)
            throw new InvalidOperationException("Bu ders için kapatılacak aktif şube bulunamadı.");

        // Aktif kayıtlı öğrenci kontrolü (tüm section'lar için toplu)
        var sectionIds = sections.Select(s => s.Id).ToList();
        var activeCount = await _context.CourseRegistrations
            .CountAsync(cr => sectionIds.Contains(cr.CourseSectionId) && cr.Status == RegistrationStatus.Active, ct);
        if (activeCount > 0)
            throw new InvalidOperationException($"Toplam {activeCount} aktif kayıtlı öğrenci bulunan ders kapatılamaz. Önce öğrencileri başka şubelere aktarın veya kayıtlarını düşürün.");

        // Draft/Pending kayıtları temizle
        var pendingRegistrations = await _context.CourseRegistrations
            .Where(cr => sectionIds.Contains(cr.CourseSectionId)
                && (cr.Status == RegistrationStatus.Draft || cr.Status == RegistrationStatus.PendingAdvisorApproval))
            .ToListAsync(ct);

        foreach (var reg in pendingRegistrations)
        {
            reg.Status = RegistrationStatus.Dropped;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        // Tüm section'ları kapat
        foreach (var section in sections)
        {
            section.IsActive = false;
            section.EnrolledCount = 0;
        }

        // Access policy temizliği
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

    public async Task ReactivateSectionAsync(int sectionId, CancellationToken ct)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, ct)
            ?? throw new InvalidOperationException("Şube bulunamadı.");

        if (section.IsActive)
            throw new InvalidOperationException("Şube zaten aktif.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.Status != TermStatus.Planning && term.Status != TermStatus.Scheduling)
            throw new InvalidOperationException(
                $"Şube yeniden açmak için dönem 'Planlama' veya 'Programlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        section.IsActive = true;
        // EnrolledCount 0 kalır — kapatılırken sıfırlanmıştı

        // Bu ders için CourseAccessPolicy yoksa yeniden oluştur
        var hasAccessPolicy = await _context.CourseAccessPolicies
            .AnyAsync(cap => cap.CourseId == section.CourseId && cap.TermId == section.TermId, ct);

        if (!hasAccessPolicy && section.Course.DepartmentId.HasValue)
        {
            // Müfredatında bu ders olan bölümleri bul
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
                _context.CourseAccessPolicies.Add(new Domain.Entities.CourseAccessPolicy
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
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.Id == instructorId, ct);

        if (instructor == null)
            throw new InvalidOperationException("Atanan öğretim üyesi bulunamadı.");

        if (owningDepartmentId.HasValue)
        {
            var courseFacultyId = await _context.Departments
                .AsNoTracking()
                .Where(d => d.Id == owningDepartmentId.Value)
                .Select(d => d.FacultyId)
                .FirstOrDefaultAsync(ct);

            if (instructor.Department?.FacultyId != courseFacultyId)
                throw new InvalidOperationException("Öğretim üyesi bu fakülteye ait değil.");
        }
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

    public async Task<BulkAssignInstructorResultDto> BulkAssignInstructorAsync(
        int departmentId, int termId, BulkAssignInstructorRequest request, CancellationToken ct)
    {
        var result = new BulkAssignInstructorResultDto();

        foreach (var assignment in request.Assignments)
        {
            try
            {
                await UpdateSectionAsync(new UpdateOpenedSectionRequest
                {
                    SectionId = assignment.SectionId,
                    InstructorId = assignment.InstructorId,
                }, ct);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Şube #{assignment.SectionId}: {ex.Message}");
            }
        }

        return result;
    }

    public async Task<List<PreviousTermCourseDto>> GetPreviousTermCoursesAsync(
        int departmentId, int sourceTermId, int targetTermId, CancellationToken ct)
    {
        // Courses opened in source term for this department
        var sourceSections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == sourceTermId && cs.IsActive && cs.Course.DepartmentId == departmentId)
            .OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.Id)
            .ToListAsync(ct);

        if (sourceSections.Count == 0)
            return [];

        // Which courses are already opened in target term
        var courseIds = sourceSections.Select(cs => cs.CourseId).Distinct().ToList();
        var alreadyOpenedCourseIds = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == targetTermId && cs.IsActive && courseIds.Contains(cs.CourseId))
            .Select(cs => cs.CourseId)
            .Distinct()
            .ToListAsync(ct);
        var alreadyOpenedSet = new HashSet<int>(alreadyOpenedCourseIds);

        // Suggested semesters from curriculum
        var suggestedSemesters = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId) && cc.Curriculum.IsActive && cc.Curriculum.Program.DepartmentId == departmentId)
            .GroupBy(cc => cc.CourseId)
            .Select(g => new { CourseId = g.Key, Semester = g.Min(cc => cc.SuggestedSemester), IsCompulsory = g.Any(cc => cc.IsCompulsory) })
            .ToDictionaryAsync(x => x.CourseId, ct);

        // Instructor names
        var instructorIds = sourceSections.Where(cs => cs.InstructorId.HasValue).Select(cs => cs.InstructorId!.Value).Distinct().ToList();
        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, ct);

        // Group by course and build DTOs
        return sourceSections
            .GroupBy(cs => cs.CourseId)
            .Select(g =>
            {
                var first = g.First();
                var sectionCount = g.Count();
                var totalCapacity = g.Sum(cs => cs.Capacity);
                // Use instructor from first section that has one
                var instrSection = g.FirstOrDefault(cs => cs.InstructorId.HasValue);
                int? instrId = instrSection?.InstructorId;
                instructorNameMap.TryGetValue(instrId ?? 0, out var instrName);
                suggestedSemesters.TryGetValue(first.CourseId, out var currInfo);

                return new PreviousTermCourseDto(
                    CourseId: first.CourseId,
                    CourseCode: first.Course.Code,
                    CourseName: first.Course.Name,
                    Ects: first.Course.Ects,
                    SuggestedSemester: currInfo?.Semester ?? 0,
                    IsCompulsory: currInfo?.IsCompulsory ?? false,
                    SectionCount: sectionCount,
                    TotalCapacity: totalCapacity,
                    InstructorName: instrName,
                    InstructorId: instrId,
                    AlreadyOpened: alreadyOpenedSet.Contains(first.CourseId));
            })
            .OrderBy(d => d.SuggestedSemester).ThenBy(d => d.CourseCode)
            .ToList();
    }

    public async Task<TransferFromTermResultDto> TransferFromTermAsync(
        TransferFromTermRequest request, CancellationToken ct = default)
    {
        var result = new TransferFromTermResultDto();
        if (request.Courses.Count == 0)
            return result;

        var courseIds = request.Courses.Select(c => c.CourseId).ToList();

        // Fetch source sections grouped by course
        var sourceSections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == request.SourceTermId
                && cs.IsActive
                && cs.Course.DepartmentId == request.DepartmentId
                && courseIds.Contains(cs.CourseId))
            .ToListAsync(ct);

        var grouped = sourceSections.GroupBy(cs => cs.CourseId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var item in request.Courses)
        {
            var courseId = item.CourseId;
            if (!grouped.TryGetValue(courseId, out var sections) || sections.Count == 0)
            {
                result.FailedCount++;
                result.Results.Add(new TransferCourseResultItem
                {
                    CourseId = courseId,
                    CourseCode = "",
                    CourseName = "",
                    Success = false,
                    Error = "Kaynak dönemde bu ders bulunamadı.",
                });
                continue;
            }

            var first = sections[0];
            var instrSection = sections.FirstOrDefault(s => s.InstructorId.HasValue);
            var defaultCapacity = sections.Count > 0 ? (int)Math.Round(sections.Average(s => s.Capacity)) : 40;

            var openRequest = new OpenCourseRequest
            {
                CourseId = courseId,
                SectionCount = item.SectionCount ?? sections.Count,
                CapacityPerSection = item.CapacityPerSection ?? defaultCapacity,
                InstructorId = item.InstructorId ?? instrSection?.InstructorId,
            };

            try
            {
                var created = await OpenCourseAsync(request.DepartmentId, request.TargetTermId, openRequest, ct);
                result.SuccessCount++;
                result.TotalSectionsCreated += created.Count;
                result.Results.Add(new TransferCourseResultItem
                {
                    CourseId = courseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    Success = true,
                    SectionsCreated = created.Count,
                });
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Results.Add(new TransferCourseResultItem
                {
                    CourseId = courseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    Success = false,
                    Error = ex.Message,
                });
            }
        }

        return result;
    }

    public async Task<List<ExternalCourseDto>> GetExternalCoursesAsync(int departmentId, int termId, CancellationToken ct = default)
    {
        // Find courses in our curriculum that are owned by OTHER departments
        var externalCourseIds = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Where(cc => cc.Curriculum.IsActive
                && cc.Curriculum.Program.DepartmentId == departmentId
                && cc.Course.DepartmentId.HasValue
                && cc.Course.DepartmentId != departmentId)
            .Select(cc => cc.CourseId)
            .Distinct()
            .ToListAsync(ct);

        if (externalCourseIds.Count == 0)
            return [];

        // Get active sections for these courses in the given term
        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course).ThenInclude(c => c.Department)
            .Where(cs => cs.TermId == termId && cs.IsActive && externalCourseIds.Contains(cs.CourseId))
            .ToListAsync(ct);

        // Load forecast data for demand
        var forecastSummary = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        var forecastMap = (forecastSummary?.CourseSummaries ?? [])
            .Where(s => externalCourseIds.Contains(s.CourseId))
            .ToDictionary(s => s.CourseId);

        return sections
            .GroupBy(cs => cs.CourseId)
            .Select(g =>
            {
                var first = g.First();
                forecastMap.TryGetValue(first.CourseId, out var forecast);
                return new ExternalCourseDto
                {
                    CourseId = first.CourseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    Ects = first.Course.Ects,
                    OwnerDepartmentName = first.Course.Department?.Name ?? "",
                    SectionCount = g.Count(),
                    TotalCapacity = g.Sum(s => s.Capacity),
                    TotalEnrolled = g.Sum(s => s.EnrolledCount),
                    PredictedDemand = forecast?.TotalPredictedDemand ?? 0,
                };
            })
            .OrderBy(d => d.CourseCode)
            .ToList();
    }

}
