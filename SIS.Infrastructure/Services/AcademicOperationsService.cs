using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicOperationsService : IAcademicOperationsService
{
    private readonly AppDbContext _context;
    private readonly IEnrollmentForecastService _forecastService;
    private readonly IEnrollmentForecastCacheInvalidator _forecastCacheInvalidator;
    private readonly ISchedulingService _schedulingService;
    private readonly ILogger<AcademicOperationsService> _logger;

    public AcademicOperationsService(
        AppDbContext context,
        IEnrollmentForecastService forecastService,
        IEnrollmentForecastCacheInvalidator forecastCacheInvalidator,
        ISchedulingService schedulingService,
        ILogger<AcademicOperationsService> logger)
    {
        _context = context;
        _forecastService = forecastService;
        _forecastCacheInvalidator = forecastCacheInvalidator;
        _schedulingService = schedulingService;
        _logger = logger;
    }

    public async Task<AcademicOperationsContextDto> GetContextAsync(int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);

        return new AcademicOperationsContextDto
        {
            RoleLabel = ResolveRoleLabel(roles),
            CanViewAllDepartments = scope.CanViewAllDepartments,
            DefaultDepartmentId = scope.Departments.FirstOrDefault()?.DepartmentId,
            AccessibleDepartments = scope.Departments,
            Notes = scope.Notes
        };
    }

    public async Task<AcademicPlanningSummaryDto?> GetPlanningSummaryAsync(
        int departmentId,
        int? termId,
        int userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);
        if (scope.Departments.All(department => department.DepartmentId != departmentId))
            return null;

        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(currentDepartment => currentDepartment.Id == departmentId, cancellationToken);

        if (department == null)
            return null;

        var term = await ResolveTermAsync(termId, cancellationToken);
        if (term == null)
            return new AcademicPlanningSummaryDto
            {
                DepartmentId = departmentId,
                DepartmentName = department.Name,
                Notes = { "Degerlendirilecek akademik donem bulunamadi." }
            };

        var forecastSummary = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        var scheduleValidation = await _schedulingService.ValidateExistingByDepartmentAsync(departmentId, term.Id, cancellationToken);

        var notes = new List<string>(scope.Notes);
        if (forecastSummary == null)
            notes.Add("Bu bolum icin tahminleme ozeti olusturulamadi.");

        return new AcademicPlanningSummaryDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TermId = term.Id,
            TermName = term.Name,
            ForecastSummary = forecastSummary,
            ScheduleValidation = scheduleValidation,
            Notes = notes.Distinct().ToList()
        };
    }

    public async Task<DepartmentClassListDto?> GetDepartmentClassListAsync(
        int departmentId,
        int? termId,
        int userId,
        IReadOnlyCollection<string> roles,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);
        if (scope.Departments.All(department => department.DepartmentId != departmentId))
            return null;

        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(currentDepartment => currentDepartment.Id == departmentId, cancellationToken);

        var term = await ResolveTermAsync(termId, cancellationToken);
        if (department == null || term == null)
            return null;

        var sectionsQuery = _context.CourseSections
            .AsNoTracking()
            .Include(section => section.Course)
            .Include(section => section.Instructor)
            .Include(section => section.Classroom)
            .Include(section => section.Registrations)
                .ThenInclude(registration => registration.Student)
            .Where(section => section.TermId == term.Id)
            .Where(section =>
                section.Course.DepartmentId == departmentId
                || (section.Instructor != null && section.Instructor.DepartmentId == departmentId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            sectionsQuery = sectionsQuery.Where(section =>
                section.Course.Code.ToLower().Contains(normalizedSearch)
                || section.Course.Name.ToLower().Contains(normalizedSearch)
                || (section.Classroom != null && section.Classroom.Name.ToLower().Contains(normalizedSearch)));
        }

        var sections = await sectionsQuery
            .OrderBy(section => section.Course.Code)
            .ThenBy(section => section.Id)
            .ToListAsync(cancellationToken);

        var instructorUserIds = sections
            .Where(section => section.Instructor != null)
            .Select(section => section.Instructor!.UserId)
            .Distinct()
            .ToList();

        var studentUserIds = sections
            .SelectMany(section => section.Registrations.Select(registration => registration.Student.UserId))
            .Distinct()
            .ToList();

        var userMap = await _context.Users
            .AsNoTracking()
            .Where(user => instructorUserIds.Contains(user.Id) || studentUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        // Load SectionSchedule records for schedule display
        var classSectionIds = sections.Select(s => s.Id).ToList();
        var sectionScheduleRecords = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => classSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(cancellationToken);
        var scheduleMap = sectionScheduleRecords
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sectionDtos = sections
            .Select(section =>
            {
                var activeRegistrations = section.Registrations
                    .Where(registration =>
                        registration.Status != RegistrationStatus.Dropped
                        && registration.Status != RegistrationStatus.Draft)
                    .ToList();

                var studentDtos = activeRegistrations
                    .Select(registration =>
                    {
                        var user = userMap.GetValueOrDefault(registration.Student.UserId);
                        return new DepartmentSectionStudentDto
                        {
                            StudentId = registration.StudentId,
                            StudentNumber = registration.Student.StudentNumber,
                            StudentName = user == null
                                ? registration.Student.StudentNumber
                                : $"{user.FirstName} {user.LastName}".Trim(),
                            Email = user?.Email ?? string.Empty,
                            Status = registration.Status.ToString(),
                            Grade = registration.Grade?.ToString()
                        };
                    })
                    .OrderBy(student => student.StudentName)
                    .ThenBy(student => student.StudentNumber)
                    .ToList();

                var instructorName = string.Empty;
                if (section.Instructor != null)
                {
                    var instructorUser = userMap.GetValueOrDefault(section.Instructor.UserId);
                    instructorName = instructorUser == null
                        ? section.Instructor.Title
                        : $"{section.Instructor.Title} {instructorUser.FirstName} {instructorUser.LastName}".Trim();
                }

                var sectionScheds = scheduleMap.GetValueOrDefault(section.Id) ?? new List<SectionSchedule>();

                return new DepartmentSectionClassListDto
                {
                    SectionId = section.Id,
                    CourseId = section.CourseId,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    InstructorName = instructorName,
                    ClassroomName = section.Classroom?.Name ?? string.Empty,
                    ScheduleSummary = ScheduleHelper.FormatSchedule(sectionScheds),
                    Capacity = section.Capacity,
                    EnrolledCount = section.EnrolledCount,
                    ActiveStudentCount = activeRegistrations.Count,
                    IsActive = section.IsActive,
                    Students = studentDtos
                };
            })
            .ToList();

        var notes = new List<string>(scope.Notes);
        if (sectionDtos.Any(section => string.IsNullOrWhiteSpace(section.ScheduleSummary)))
            notes.Add("Bazi sectionlarda schedule tanimi henuz tamamlanmamis.");
        if (sectionDtos.Any(section => string.IsNullOrWhiteSpace(section.ClassroomName)))
            notes.Add("Bazi sectionlarda derslik atamasi eksik.");

        return new DepartmentClassListDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TermId = term.Id,
            TermName = term.Name,
            TotalSections = sectionDtos.Count,
            TotalStudents = sectionDtos.Sum(section => section.ActiveStudentCount),
            Sections = sectionDtos,
            Notes = notes.Distinct().ToList()
        };
    }

    public async Task<DepartmentCurriculumOverviewDto?> GetCurriculumOverviewAsync(
        int departmentId,
        int userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);
        if (scope.Departments.All(department => department.DepartmentId != departmentId))
            return null;

        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(currentDepartment => currentDepartment.Id == departmentId, cancellationToken);

        if (department == null)
            return null;

        var programs = await _context.AcademicPrograms
            .AsNoTracking()
            .Include(program => program.Curricula)
                .ThenInclude(curriculum => curriculum.CurriculumCourses)
                    .ThenInclude(curriculumCourse => curriculumCourse.Course)
            .Where(program => program.DepartmentId == departmentId)
            .OrderBy(program => program.Name)
            .ToListAsync(cancellationToken);

        var programDtos = programs
            .SelectMany(program => program.Curricula.Select(curriculum => new { Program = program, Curriculum = curriculum }))
            .Select(item =>
            {
                var curriculumCourses = item.Curriculum.CurriculumCourses
                    .Where(curriculumCourse => curriculumCourse.SuggestedSemester > 0)
                    .OrderBy(curriculumCourse => curriculumCourse.SuggestedSemester)
                    .ThenBy(curriculumCourse => curriculumCourse.Course.Code)
                    .ToList();

                var semesterSummaries = curriculumCourses
                    .GroupBy(curriculumCourse => curriculumCourse.SuggestedSemester)
                    .Select(group => new CurriculumSemesterOverviewDto
                    {
                        SemesterNumber = group.Key,
                        Label = BuildSemesterLabel(group.Key),
                        CourseCount = group.Count(),
                        Credits = group.Sum(curriculumCourse => curriculumCourse.Course.TheoryHours + curriculumCourse.Course.LabHours),
                        Ects = group.Sum(curriculumCourse => curriculumCourse.Course.Ects),
                        CompulsoryCourseCount = group.Count(curriculumCourse => curriculumCourse.IsCompulsory),
                        ElectiveCourseCount = group.Count(curriculumCourse => !curriculumCourse.IsCompulsory)
                    })
                    .OrderBy(summary => summary.SemesterNumber)
                    .ToList();

                return new ProgramCurriculumOverviewDto
                {
                    ProgramId = item.Program.Id,
                    ProgramName = item.Program.Name,
                    ProgramCode = item.Program.Code,
                    CurriculumId = item.Curriculum.Id,
                    CurriculumVersion = item.Curriculum.Version,
                    IsActiveCurriculum = item.Curriculum.IsActive,
                    TotalCourses = curriculumCourses.Count,
                    TotalCredits = curriculumCourses.Sum(curriculumCourse => curriculumCourse.Course.TheoryHours + curriculumCourse.Course.LabHours),
                    TotalEcts = curriculumCourses.Sum(curriculumCourse => curriculumCourse.Course.Ects),
                    CompulsoryCourseCount = curriculumCourses.Count(curriculumCourse => curriculumCourse.IsCompulsory),
                    ElectiveCourseCount = curriculumCourses.Count(curriculumCourse => !curriculumCourse.IsCompulsory),
                    SemesterSummaries = semesterSummaries
                };
            })
            .OrderBy(program => program.ProgramName)
            .ThenByDescending(program => program.IsActiveCurriculum)
            .ThenByDescending(program => program.CurriculumVersion)
            .ToList();

        var notes = new List<string>(scope.Notes);
        if (programDtos.All(program => !program.IsActiveCurriculum))
            notes.Add("Bolumde aktif isaretlenmis mufredat bulunmuyor.");

        return new DepartmentCurriculumOverviewDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            ProgramCount = programs.Count,
            ActiveCurriculumCount = programDtos.Count(program => program.IsActiveCurriculum),
            TotalCourses = programDtos.Sum(program => program.TotalCourses),
            TotalEcts = programDtos.Sum(program => program.TotalEcts),
            Programs = programDtos,
            Notes = notes.Distinct().ToList()
        };
    }

    private async Task<AcademicTerm?> ResolveTermAsync(int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue)
        {
            return await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(term => term.Id == termId.Value, cancellationToken);
        }

        return await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(term => term.IsActive, cancellationToken);
    }

    private async Task<DepartmentScopeResolution> ResolveAccessibleDepartmentsAsync(
        int userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .OrderBy(department => department.Name)
                .Select(department => new AccessibleDepartmentDto
                {
                    DepartmentId = department.Id,
                    DepartmentName = department.Name,
                    DepartmentCode = department.Code
                })
                .ToListAsync(cancellationToken);

            return new DepartmentScopeResolution
            {
                CanViewAllDepartments = true,
                Departments = departments
            };
        }

        if (roles.Contains("DepartmentManager"))
        {
            var department = await _context.Instructors
                .AsNoTracking()
                .Where(instructor => instructor.UserId == userId)
                .Join(
                    _context.Departments.AsNoTracking(),
                    instructor => instructor.DepartmentId,
                    department => department.Id,
                    (_, department) => new AccessibleDepartmentDto
                    {
                        DepartmentId = department.Id,
                        DepartmentName = department.Name,
                        DepartmentCode = department.Code
                    })
                .FirstOrDefaultAsync(cancellationToken);

            if (department != null)
            {
                return new DepartmentScopeResolution
                {
                    CanViewAllDepartments = false,
                    Departments = [department],
                    Notes = ["Bolum baskani gorunumu kendi bolumunuzle sinirlidir."]
                };
            }

            return new DepartmentScopeResolution
            {
                CanViewAllDepartments = false,
                Notes = ["DepartmentManager kullanicisi icin bagli bolum bulunamadi."]
            };
        }

        return new DepartmentScopeResolution
        {
            CanViewAllDepartments = false,
            Notes = ["Bu kullanici icin akademik operasyon kapsamı tanimlanmamis."]
        };
    }

    private static string ResolveRoleLabel(IReadOnlyCollection<string> roles)
    {
        if (roles.Contains("SystemAdministrator"))
            return "Sistem Yönetimi";

        if (roles.Contains("AdministrativeStaff"))
            return "Akademik İşler";

        if (roles.Contains("DepartmentManager"))
            return "Bölüm Başkanlığı";

        return "Akademik Operasyonlar";
    }

    private static string BuildSemesterLabel(int semesterNumber)
    {
        var classYear = (semesterNumber - 1) / 2 + 1;
        var termLabel = semesterNumber % 2 == 1 ? "Güz" : "Bahar";
        return $"{classYear}. Sınıf {termLabel}";
    }

    // ── Section Approval ──────────────────────────────────────

    public async Task<List<CourseSectionApprovalDto>> GetApprovalsAsync(int departmentId, int termId, CancellationToken cancellationToken = default)
    {
        var openedSectionCounts = await _context.CourseSections
            .AsNoTracking()
            .Where(section =>
                section.TermId == termId
                && section.IsActive
                && section.Course.DepartmentId == departmentId)
            .GroupBy(section => section.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CourseId, item => item.Count, cancellationToken);

        var approvals = await _context.CourseSectionApprovals
            .AsNoTracking()
            .Where(a => a.DepartmentId == departmentId && a.TermId == termId)
            .Select(a => new CourseSectionApprovalDto
            {
                Id = a.Id,
                CourseId = a.CourseId,
                CourseCode = a.Course.Code,
                CourseName = a.Course.Name,
                RecommendedSectionCount = a.RecommendedSectionCount,
                ApprovedSectionCount = a.ApprovedSectionCount,
                PredictedDemand = a.PredictedDemand,
                Status = a.Status,
                Note = a.Note,
                CreatedAt = a.CreatedAt,
                DecidedAt = a.DecidedAt
            })
            .OrderBy(a => a.CourseCode)
            .ToListAsync(cancellationToken);

        foreach (var approval in approvals)
        {
            approval.OpenedSectionCount = openedSectionCounts.GetValueOrDefault(approval.CourseId);
            approval.RemainingSectionCount = Math.Max(0, (approval.ApprovedSectionCount ?? 0) - approval.OpenedSectionCount);
            approval.WorkflowStatus = ResolveWorkflowStatus(
                approval.Status,
                approval.ApprovedSectionCount,
                approval.OpenedSectionCount);
        }

        return approvals;
    }

    public async Task<CourseSectionApprovalDto> ApproveCourseSectionsAsync(
        int departmentId, int termId, ApproveCourseSectionsRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var approveTerm = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, cancellationToken);
        if (approveTerm != null && approveTerm.Status != TermStatus.Planning)
            throw new InvalidOperationException("Ders onaylama sadece 'Planlama' aşamasında yapılabilir.");

        if (request.SectionCount <= 0)
            throw new ArgumentException("Şube sayısı en az 1 olmalıdır.");

        var approval = await GetOrCreateApprovalAsync(departmentId, termId, request.CourseId, cancellationToken);

        approval.Status = SectionApprovalStatus.Approved;
        approval.ApprovedSectionCount = request.SectionCount;
        approval.Note = request.Note;
        approval.DecidedByUserId = userId;
        approval.DecidedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _forecastCacheInvalidator.InvalidateForecastContext();

        var openedSectionCount = await GetOpenedSectionCountAsync(departmentId, termId, request.CourseId, cancellationToken);
        return MapApprovalToDto(approval, openedSectionCount);
    }

    public async Task<CourseSectionApprovalDto> RejectCourseSectionsAsync(
        int departmentId, int termId, RejectCourseSectionsRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var rejectTerm = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, cancellationToken);
        if (rejectTerm != null && rejectTerm.Status != TermStatus.Planning)
            throw new InvalidOperationException("Ders reddetme sadece 'Planlama' aşamasında yapılabilir.");

        var approval = await GetOrCreateApprovalAsync(departmentId, termId, request.CourseId, cancellationToken);

        approval.Status = SectionApprovalStatus.Rejected;
        approval.ApprovedSectionCount = null;
        approval.Note = request.Note;
        approval.DecidedByUserId = userId;
        approval.DecidedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _forecastCacheInvalidator.InvalidateForecastContext();

        var openedSectionCount = await GetOpenedSectionCountAsync(departmentId, termId, request.CourseId, cancellationToken);
        return MapApprovalToDto(approval, openedSectionCount);
    }

    public async Task<BulkApprovalResultDto> BulkApproveAsync(
        int departmentId, int termId, BulkApprovalRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkApprovalResultDto();
        var totalSectionsCreated = 0;

        foreach (var item in request.Approvals)
        {
            try
            {
                var dto = await ApproveCourseSectionsAsync(departmentId, termId, item, userId, cancellationToken);
                result.ApprovedCount++;
                totalSectionsCreated += 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{item.CourseId}: {ex.Message}");
            }
        }

        result.SectionsCreated = totalSectionsCreated;
        return result;
    }

    private async Task<CourseSectionApproval> GetOrCreateApprovalAsync(int departmentId, int termId, int courseId, CancellationToken cancellationToken)
    {
        var existing = await _context.CourseSectionApprovals
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.CourseId == courseId && a.DepartmentId == departmentId && a.TermId == termId, cancellationToken);

        if (existing != null)
            return existing;

        // Forecast'ten tahmini veri çek
        var forecast = await _forecastService.GetCourseDemandForecastAsync(courseId);

        var approval = new CourseSectionApproval
        {
            CourseId = courseId,
            DepartmentId = departmentId,
            TermId = termId,
            RecommendedSectionCount = forecast?.RecommendedSectionCount ?? 1,
            PredictedDemand = forecast?.TotalPredictedDemand ?? 0
        };

        // Course navigation property'yi yükle
        approval.Course = (await _context.Courses.FindAsync([courseId], cancellationToken))!;

        _context.CourseSectionApprovals.Add(approval);
        return approval;
    }

    private async Task<int> GetOpenedSectionCountAsync(int departmentId, int termId, int courseId, CancellationToken cancellationToken)
    {
        return await _context.CourseSections
            .AsNoTracking()
            .Where(section =>
                section.CourseId == courseId
                && section.TermId == termId
                && section.IsActive
                && section.Course.DepartmentId == departmentId)
            .CountAsync(cancellationToken);
    }

    private static CourseSectionApprovalDto MapApprovalToDto(CourseSectionApproval approval, int openedSectionCount)
    {
        return new CourseSectionApprovalDto
        {
            Id = approval.Id,
            CourseId = approval.CourseId,
            CourseCode = approval.Course.Code,
            CourseName = approval.Course.Name,
            RecommendedSectionCount = approval.RecommendedSectionCount,
            ApprovedSectionCount = approval.ApprovedSectionCount,
            OpenedSectionCount = openedSectionCount,
            RemainingSectionCount = Math.Max(0, (approval.ApprovedSectionCount ?? 0) - openedSectionCount),
            PredictedDemand = approval.PredictedDemand,
            Status = approval.Status,
            WorkflowStatus = ResolveWorkflowStatus(approval.Status, approval.ApprovedSectionCount, openedSectionCount),
            Note = approval.Note,
            CreatedAt = approval.CreatedAt,
            DecidedAt = approval.DecidedAt
        };
    }

    private static string ResolveWorkflowStatus(
        SectionApprovalStatus status,
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

    private sealed class DepartmentScopeResolution
    {
        public bool CanViewAllDepartments { get; init; }
        public List<AccessibleDepartmentDto> Departments { get; init; } = new();
        public List<string> Notes { get; init; } = new();
    }
}
