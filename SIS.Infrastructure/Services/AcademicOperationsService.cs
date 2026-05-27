using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Bölüm bazlı akademik operasyonlar servisi. Büyüklüğü nedeniyle partial class olarak
/// alanlara bölünmüştür:
/// - AcademicOperationsService.cs           → kapsam çözümü + paylaşılan yardımcılar
/// - AcademicOperationsService.Planning.cs   → planlama özeti, sınıf listeleri, müfredat genel bakışı
/// - AcademicOperationsService.Students.cs   → öğrenci listesi + müfredat seçenekleri
/// - AcademicOperationsService.BulkOperations.cs → toplu danışman/müfredat atama
/// </summary>
public partial class AcademicOperationsService : IAcademicOperationsService
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

    private async Task<Dictionary<int, ApplicationUser>> LoadUsersForStudentsAsync(
        IEnumerable<Student> students,
        CancellationToken cancellationToken)
    {
        var userIds = students
            .Select(student => student.UserId)
            .Distinct()
            .ToList();

        return await _context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
    }

    private static string BuildUserFullName(ApplicationUser? user, string fallback)
    {
        if (user == null)
            return fallback;

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? fallback : fullName;
    }

    private static BulkAcademicOperationResultItemDto FailedResult(int studentId, string message) =>
        new()
        {
            StudentId = studentId,
            Success = false,
            Message = message
        };

    private static void FinalizeBulkResult(BulkAcademicOperationResultDto result)
    {
        result.SuccessCount = result.Results.Count(item => item.Success);
        result.FailedCount = result.Results.Count(item => !item.Success);
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
            .ActiveTerm()
            .FirstOrDefaultAsync(cancellationToken);
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

        if (roles.Contains("Dean"))
        {
            var facultyId = await _context.Instructors
                .AsNoTracking()
                .Where(instructor => instructor.UserId == userId)
                .Select(instructor => (int?)instructor.Department.FacultyId)
                .FirstOrDefaultAsync(cancellationToken);

            if (facultyId.HasValue)
            {
                var departments = await _context.Departments
                    .AsNoTracking()
                    .Where(department => department.FacultyId == facultyId.Value)
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
                    CanViewAllDepartments = false,
                    Departments = departments,
                    Notes = ["Dekan görünümü bağlı fakültenizdeki bölümlerle sınırlıdır."]
                };
            }

            return new DepartmentScopeResolution
            {
                CanViewAllDepartments = false,
                Notes = ["Dekan kullanıcısı için bağlı fakülte bulunamadı."]
            };
        }

        if (roles.Contains("DepartmentManager") || roles.Contains("Instructor") || roles.Contains("AcademicAdvisor"))
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
                var note = roles.Contains("DepartmentManager")
                    ? "Bölüm başkanı görünümü kendi bölümünüzle sınırlıdır."
                    : "Bölüm işlemleri görünümü bağlı olduğunuz bölümle sınırlıdır.";

                return new DepartmentScopeResolution
                {
                    CanViewAllDepartments = false,
                    Departments = [department],
                    Notes = [note]
                };
            }

            return new DepartmentScopeResolution
            {
                CanViewAllDepartments = false,
                Notes = ["Bu kullanıcı için bağlı bölüm bulunamadı."]
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

        if (roles.Contains("Dean"))
            return "Dekanlık";

        if (roles.Contains("DepartmentManager"))
            return "Bölüm Başkanlığı";

        if (roles.Contains("AcademicAdvisor"))
            return "Akademik Danışmanlık";

        if (roles.Contains("Instructor"))
            return "Öğretim Elemanı";

        return "Akademik Operasyonlar";
    }

    private sealed class DepartmentScopeResolution
    {
        public bool CanViewAllDepartments { get; init; }
        public List<AccessibleDepartmentDto> Departments { get; init; } = new();
        public List<string> Notes { get; init; } = new();
    }
}
