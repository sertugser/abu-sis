using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Lazy section-picker: öğrenci "+" butonuna bastığında, seçilen dersin şubelerini
/// filtreli + sayfalı + kapı-farkında (IRegistrationGate) olarak döner.
/// N+1 yok: zamanlama + hoca adları birer toplu sorguda çekilir.
/// </summary>
public sealed class SectionPickerService : ISectionPickerService
{
    private readonly AppDbContext _context;
    private readonly IRegistrationGate _registrationGate;
    private readonly ISystemConfigProvider _config;

    public SectionPickerService(
        AppDbContext context,
        IRegistrationGate registrationGate,
        ISystemConfigProvider config)
    {
        _context = context;
        _registrationGate = registrationGate;
        _config = config;
    }

    public async Task<SectionPickerResultDto> QueryAsync(
        int studentId, SectionPickerQuery query, CancellationToken ct = default)
    {
        // 1. Resolve active term — if none, return empty
        var activeTerm = await _context.AcademicTerms
            .ActiveTerm()
            .FirstOrDefaultAsync(ct);

        if (activeTerm is null)
            return new SectionPickerResultDto([], 0, query.Page, query.PageSize);

        // 2. Clamp paging inputs — prevents negative OFFSET (Page=0) and unbounded Take
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // 2b. Yayın kapısı (publish gate): dersin programı (fakülte takvimi) henüz onaylanmadıysa
        // şubeler tamamen GİZLENİR (yayın öncesi gizli — taslak şube/program sızdırılmaz).
        var courseDeptId = await _context.Courses
            .Where(c => c.Id == query.CourseId)
            .Select(c => c.DepartmentId)
            .FirstOrDefaultAsync(ct);
        if (courseDeptId.HasValue)
        {
            var courseFacultyId = await _context.Departments
                .Where(d => d.Id == courseDeptId.Value)
                .Select(d => d.FacultyId)
                .FirstOrDefaultAsync(ct);
            var facultyApproved = await _context.FacultyScheduleApprovals
                .AnyAsync(a => a.FacultyId == courseFacultyId && a.TermId == activeTerm.Id, ct);
            if (!facultyApproved)
                return new SectionPickerResultDto([], 0, page, pageSize);
        }

        // 3. Base query: yalnızca Active (Draft/Cancelled gizli) şubeler, bu ders + aktif dönem
        var baseQuery = _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.CourseId == query.CourseId && cs.TermId == activeTerm.Id
                && cs.Status == CourseSectionStatus.Active);

        // 3. Apply optional filters
        if (query.InstructorId.HasValue)
            baseQuery = baseQuery.Where(cs => cs.InstructorId == query.InstructorId.Value);

        if (query.FacultyId.HasValue)
        {
            // Filter by course's department → faculty chain
            var deptIdsInFaculty = _context.Departments
                .Where(d => d.FacultyId == query.FacultyId.Value)
                .Select(d => d.Id);
            baseQuery = baseQuery.Where(cs =>
                cs.Course.DepartmentId != null
                && deptIdsInFaculty.Contains(cs.Course.DepartmentId.Value));
        }

        if (query.ProgramId.HasValue)
        {
            // Filter by courses accessible to the program via CourseAccessPolicy
            var programDeptId = await _context.AcademicPrograms
                .AsNoTracking()
                .Where(p => p.Id == query.ProgramId.Value)
                .Select(p => p.DepartmentId)
                .FirstOrDefaultAsync(ct);

            if (programDeptId != default)
            {
                var programFacultyId = await _context.Departments
                    .AsNoTracking()
                    .Where(d => d.Id == programDeptId)
                    .Select(d => d.FacultyId)
                    .FirstOrDefaultAsync(ct);

                var accessibleCourseIds = _context.CourseAccessPolicies
                    .Where(cap => cap.TermId == activeTerm.Id
                        && (cap.DepartmentId == null || cap.DepartmentId == programDeptId)
                        && (cap.FacultyId == null || cap.FacultyId == programFacultyId))
                    .Select(cap => cap.CourseId)
                    .Distinct();

                // Show sections that are accessible to the program, or course-dept matches program-dept
                baseQuery = baseQuery.Where(cs =>
                    accessibleCourseIds.Contains(cs.CourseId)
                    || cs.Course.DepartmentId == programDeptId
                    || cs.Course.DepartmentId == null);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(cs =>
                cs.Course.Code.ToLower().Contains(search)
                || cs.Course.Name.ToLower().Contains(search));
        }

        // 4. TotalCount before paging
        var totalCount = await baseQuery.CountAsync(ct);

        // 5. Page
        var pagedSections = await baseQuery
            .OrderBy(cs => cs.SectionNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (pagedSections.Count == 0)
            return new SectionPickerResultDto([], totalCount, page, pageSize);

        // 5b. Gate değerlendirmesi tek toplu sorguda (N+1 yok; kural tek kaynak IRegistrationGate).
        var gateResults = await _registrationGate.EvaluateSectionsAsync(pagedSections, ct);

        // 5c. Cross-list grup kapasitesi: gösterim write-path ile tutarlı olsun diye grup toplamı.
        var crossListGroupIds = pagedSections
            .Where(s => s.CrossListGroupId.HasValue)
            .Select(s => s.CrossListGroupId!.Value)
            .Distinct()
            .ToList();
        var crossListEnrolled = crossListGroupIds.Count == 0
            ? new Dictionary<int, int>()
            : await _context.CourseSections
                .Where(cs => cs.CrossListGroupId != null && crossListGroupIds.Contains(cs.CrossListGroupId.Value))
                .GroupBy(cs => cs.CrossListGroupId!.Value)
                .Select(g => new { GroupId = g.Key, Sum = g.Sum(x => x.EnrolledCount) })
                .ToDictionaryAsync(x => x.GroupId, x => x.Sum, ct);

        // 6. Load schedules in one query (no N+1)
        var sectionIds = pagedSections.Select(s => s.Id).ToList();
        var scheduleRecords = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => sectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(ct);

        var scheduleMap = scheduleRecords
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.FormatSchedule(g));

        // 7. Load instructor user names in one query (no N+1)
        var instructorUserIds = pagedSections
            .Where(s => s.Instructor != null)
            .Select(s => s.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNameMap = await _context.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct);

        // 8. Quota visibility config
        var hideQuota = await _config.GetBoolAsync("Registration.HideQuotaInCatalog", false, ct);

        // 9. Take-type: look up the student's grade history for this specific course
        var takeType = await ResolveTakeTypeAsync(studentId, query.CourseId, ct);

        // 10. Build DTO rows
        var items = new List<SectionPickerRowDto>(pagedSections.Count);
        foreach (var section in pagedSections)
        {
            var gate = gateResults[section.Id];
            var effectiveEnrolled = section.CrossListGroupId.HasValue
                ? crossListEnrolled.GetValueOrDefault(section.CrossListGroupId.Value, section.EnrolledCount)
                : section.EnrolledCount;

            var instructorName = section.Instructor is { } instr
                && userNameMap.TryGetValue(instr.UserId, out var uName)
                    ? $"{instr.Title} {uName}".Trim()
                    : string.Empty;

            var schedule = scheduleMap.GetValueOrDefault(section.Id, string.Empty);

            var course = section.Course;
            var classYear = ResolveClassYear(course.Code);

            var row = new SectionPickerRowDto(
                SectionId: section.Id,
                SectionNumber: section.SectionNumber,
                Code: course.Code,
                Name: course.Name,
                Schedule: schedule,
                IsCompulsory: false,          // caller can override; picker doesn't have curriculum context
                TheoryHours: course.TheoryHours,
                LabHours: course.LabHours,
                NationalCredit: course.NationalCredit,
                Ects: course.Ects,
                InstructorName: instructorName,
                ClassYear: classYear,
                OpeningReasonName: null,       // optional: not available without curriculum context
                TakeTypeLabel: takeType,
                Capacity: hideQuota ? 0 : section.Capacity,
                EnrolledCount: hideQuota ? 0 : effectiveEnrolled,
                AvailableSeats: hideQuota ? 0 : Math.Max(0, section.Capacity - effectiveEnrolled),
                QuotaVisible: !hideQuota,
                GroupCode: null,               // optional: not loaded without curriculum pool context
                IsEnrollmentSuspended: section.IsEnrollmentSuspended,
                IsActive: section.IsActive,
                IsEligible: gate.IsOpen,
                IneligibleReason: gate.IsOpen ? null : gate.Message,
                PrerequisiteNote: null         // optional: prerequisite check is done in catalog layer
            );

            items.Add(row);
        }

        return new SectionPickerResultDto(items, totalCount, page, pageSize);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Light past-registration lookup for this single course.
    /// Returns "Alttan" (failed), "Yükseltme" (passed, improvement), or "İlk" (never taken).
    /// </summary>
    private async Task<string> ResolveTakeTypeAsync(int studentId, int courseId, CancellationToken ct)
    {
        var pastGrades = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == studentId
                && cr.CourseSection.CourseId == courseId
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped
                && cr.Grade != null)
            .Select(cr => cr.Grade!.Value)
            .ToListAsync(ct);

        if (pastGrades.Count == 0)
            return "İlk";

        bool anyFailed = pastGrades.Any(g => g == GradeCode.FF || g == GradeCode.NA || g == GradeCode.U);
        if (anyFailed)
            return "Alttan";

        return "Yükseltme";
    }

    /// <summary>
    /// Derives the class year from the course code digit convention:
    /// 1xx → year 1, 2xx → year 2, 3xx → year 3, 4xx → year 4.
    /// Falls back to 0 when no digit can be parsed.
    /// </summary>
    private static int ResolveClassYear(string code)
    {
        foreach (var ch in code)
        {
            if (char.IsDigit(ch) && ch != '0')
                return ch - '0';
        }
        return 0;
    }
}
