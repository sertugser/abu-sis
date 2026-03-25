using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicOperationsService : IAcademicOperationsService
{
    private static readonly JsonSerializerOptions ScheduleJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _context;
    private readonly IEnrollmentForecastService _forecastService;
    private readonly ISchedulingService _schedulingService;

    public AcademicOperationsService(
        AppDbContext context,
        IEnrollmentForecastService forecastService,
        ISchedulingService schedulingService)
    {
        _context = context;
        _forecastService = forecastService;
        _schedulingService = schedulingService;
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

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(section => section.Course)
            .Include(section => section.Instructor)
            .Include(section => section.Classroom)
            .Include(section => section.Registrations)
                .ThenInclude(registration => registration.Student)
            .Where(section => section.TermId == term.Id)
            .Where(section =>
                section.Course.DepartmentId == departmentId
                || (section.Instructor != null && section.Instructor.DepartmentId == departmentId))
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

                return new DepartmentSectionClassListDto
                {
                    SectionId = section.Id,
                    CourseId = section.CourseId,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    InstructorName = instructorName,
                    ClassroomName = section.Classroom?.Name ?? string.Empty,
                    ScheduleSummary = FormatSchedule(section.ScheduleJson),
                    ScheduleJson = section.ScheduleJson,
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

    private static string FormatSchedule(string scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson) || scheduleJson == "[]")
            return string.Empty;

        try
        {
            var slots = JsonSerializer.Deserialize<List<ScheduleSlot>>(scheduleJson, ScheduleJsonOptions);
            if (slots == null || slots.Count == 0)
                return string.Empty;

            return string.Join(", ", slots
                .OrderBy(slot => slot.Day)
                .ThenBy(slot => slot.StartTime)
                .Select(slot => $"{DayOfWeekToTurkish(slot.Day)} {slot.StartTime:HH:mm}-{slot.EndTime:HH:mm}"));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildSemesterLabel(int semesterNumber)
    {
        var classYear = (semesterNumber - 1) / 2 + 1;
        var termLabel = semesterNumber % 2 == 1 ? "Guz" : "Bahar";
        return $"{classYear}. Sinif {termLabel}";
    }

    private static string DayOfWeekToTurkish(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Sali",
        DayOfWeek.Wednesday => "Carsamba",
        DayOfWeek.Thursday => "Persembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => day.ToString()
    };

    private sealed class DepartmentScopeResolution
    {
        public bool CanViewAllDepartments { get; init; }
        public List<AccessibleDepartmentDto> Departments { get; init; } = new();
        public List<string> Notes { get; init; } = new();
    }
}
