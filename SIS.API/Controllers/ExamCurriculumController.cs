using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/exam-curriculum")]
[Authorize]
public class ExamCurriculumController : ControllerBase
{
    private readonly AppDbContext _context;

    public ExamCurriculumController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("terms")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetTerms(CancellationToken ct)
    {
        var terms = await _context.AcademicTerms
            .AsNoTracking()
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.Id)
            .ToListAsync(ct);

        return Ok(terms.Select(t => new ExamCurriculumTermDto(
            t.Id,
            t.Name,
            t.ShortName,
            t.Year,
            t.TermType.ToString()
        )));
    }

    [HttpGet("curricula")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetCurricula([FromQuery] int departmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await HasDepartmentAccessAsync(userId, departmentId, ct))
            return Forbid();

        var curricula = await _context.Curricula
            .AsNoTracking()
            .Include(c => c.Program)
                .ThenInclude(p => p.Department)
            .Where(c => c.Program.DepartmentId == departmentId)
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Program.Name)
            .ThenByDescending(c => c.Version)
            .Select(c => new ExamCurriculumLookupDto(
                c.Id,
                c.Name ?? $"{c.Program.Name} {c.Version}",
                c.Version,
                c.ProgramId,
                c.Program.Name,
                c.Program.DepartmentId,
                c.Program.Department.Name,
                c.IsActive
            ))
            .ToListAsync(ct);

        return Ok(curricula);
    }

    [HttpGet("curriculum-courses")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetCurriculumCourses([FromQuery] int curriculumId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var departmentId = await _context.Curricula
            .AsNoTracking()
            .Where(c => c.Id == curriculumId)
            .Select(c => (int?)c.Program.DepartmentId)
            .FirstOrDefaultAsync(ct);

        if (departmentId == null)
            return NotFound(new { message = "Müfredat bulunamadı." });

        if (!await HasDepartmentAccessAsync(userId, departmentId.Value, ct))
            return Forbid();

        var courses = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Where(cc => cc.CurriculumId == curriculumId)
            .OrderBy(cc => cc.SuggestedSemester)
            .ThenBy(cc => cc.Course.Code)
            .Select(cc => new ExamCurriculumCourseLookupDto(
                cc.CourseId,
                cc.Course.Code,
                cc.Course.Name,
                cc.SuggestedSemester,
                cc.IsCompulsory,
                cc.Course.Ects
            ))
            .ToListAsync(ct);

        return Ok(courses);
    }

    [HttpGet("template")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetTemplate(
        [FromQuery] int curriculumId,
        [FromQuery] int courseId,
        [FromQuery] int academicTermId,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var accessResult = await ValidateCurriculumCourseAccessAsync(userId, curriculumId, courseId, ct);
        if (accessResult != null)
            return accessResult;

        try
        {
            var result = await BuildTemplateResponseAsync(curriculumId, courseId, academicTermId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("template")]
    [RequirePermission("menu.ders-acma-islemleri.edit")]
    public async Task<IActionResult> SaveTemplate(
        [FromBody] SaveExamCurriculumTemplateRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var accessResult = await ValidateCurriculumCourseAccessAsync(
            userId,
            request.CurriculumId,
            request.CourseId,
            ct);

        if (accessResult != null)
            return accessResult;

        try
        {
            ValidateSaveRequest(request);

            var termExists = await _context.AcademicTerms
                .AsNoTracking()
                .AnyAsync(t => t.Id == request.AcademicTermId, ct);

            if (!termExists)
                return NotFound(new { message = "Akademik dönem bulunamadı." });

            var existingComponents = await _context.GradeComponents
                .Where(gc =>
                    gc.CourseId == request.CourseId &&
                    gc.AcademicTermId == request.AcademicTermId)
                .OrderBy(gc => gc.Order)
                .ToListAsync(ct);

            var incomingExistingIds = request.Components
                .Where(c => c.Id.HasValue && c.Id.Value > 0)
                .Select(c => c.Id!.Value)
                .ToHashSet();

            foreach (var component in request.Components.Where(c => c.Id.HasValue && c.Id.Value > 0))
            {
                if (existingComponents.All(ec => ec.Id != component.Id!.Value))
                {
                    throw new InvalidOperationException(
                        $"Güncellenmek istenen sınav türü bu derse/döneme ait değil: {component.Name}");
                }
            }

            var componentsToRemove = existingComponents
                .Where(ec => !incomingExistingIds.Contains(ec.Id))
                .ToList();

            if (componentsToRemove.Count > 0)
            {
                var removeIds = componentsToRemove.Select(c => c.Id).ToList();

                var hasStudentGrades = await _context.StudentGrades
                    .AsNoTracking()
                    .AnyAsync(sg => removeIds.Contains(sg.GradeComponentId), ct);

                if (hasStudentGrades)
                {
                    throw new InvalidOperationException(
                        "Silmek istediğiniz sınav türlerinden en az birinde öğrenci notu var. Not girilmiş sınav türleri silinemez.");
                }

                foreach (var component in existingComponents)
                {
                    if (component.PrerequisiteGradeComponentId.HasValue &&
                        removeIds.Contains(component.PrerequisiteGradeComponentId.Value))
                    {
                        component.PrerequisiteGradeComponentId = null;
                    }
                }

                _context.GradeComponents.RemoveRange(componentsToRemove);
            }

            var componentMapByRequestIndex = new Dictionary<int, GradeComponent>();
            var existingById = existingComponents.ToDictionary(c => c.Id);

            for (var i = 0; i < request.Components.Count; i++)
            {
                var item = request.Components[i];

                GradeComponent component;

                if (item.Id.HasValue && item.Id.Value > 0)
                {
                    component = existingById[item.Id.Value];
                }
                else
                {
                    component = new GradeComponent
                    {
                        CourseId = request.CourseId,
                        AcademicTermId = request.AcademicTermId
                    };

                    _context.GradeComponents.Add(component);
                }

                component.Name = item.Name.Trim();
                component.ExamType = item.ExamType.Trim();
                component.Weight = item.Weight;
                component.Order = item.Order > 0 ? item.Order : i + 1;
                component.IsRequired = item.IsRequired;
                component.MinimumScore = item.MinimumScore;
                component.RequiresAttendanceEligibility = item.RequiresAttendanceEligibility;
                component.PrerequisiteGradeComponentId = null;

                componentMapByRequestIndex[i] = component;
            }

            var criteria = await _context.GradingCriteria
                .FirstOrDefaultAsync(gc =>
                    gc.CourseId == request.CourseId &&
                    gc.AcademicTermId == request.AcademicTermId,
                    ct);

            if (criteria == null)
            {
                criteria = new GradingCriteria
                {
                    CourseId = request.CourseId,
                    AcademicTermId = request.AcademicTermId
                };

                _context.GradingCriteria.Add(criteria);
            }

            criteria.CalculationMethod = NormalizeCalculationMethod(request.CalculationMethod);

            await _context.SaveChangesAsync(ct);

            var componentsById = componentMapByRequestIndex
                .Where(kv => kv.Value.Id > 0)
                .ToDictionary(kv => kv.Value.Id, kv => kv.Value);

            var componentsByClientKey = request.Components
                .Select((item, index) => new { item.ClientKey, Component = componentMapByRequestIndex[index] })
                .Where(x => !string.IsNullOrWhiteSpace(x.ClientKey))
                .ToDictionary(x => x.ClientKey!.Trim(), x => x.Component);

            for (var i = 0; i < request.Components.Count; i++)
            {
                var item = request.Components[i];
                var component = componentMapByRequestIndex[i];

                GradeComponent? prerequisite = null;

                if (item.PrerequisiteGradeComponentId.HasValue)
                {
                    componentsById.TryGetValue(item.PrerequisiteGradeComponentId.Value, out prerequisite);
                }
                else if (!string.IsNullOrWhiteSpace(item.PrerequisiteClientKey))
                {
                    componentsByClientKey.TryGetValue(item.PrerequisiteClientKey.Trim(), out prerequisite);
                }

                if (prerequisite != null)
                {
                    if (prerequisite.Id == component.Id)
                        throw new InvalidOperationException($"{component.Name} kendi kendisine bağlı kural olarak seçilemez.");

                    component.PrerequisiteGradeComponentId = prerequisite.Id;
                }
            }

            await _context.SaveChangesAsync(ct);

            var result = await BuildTemplateResponseAsync(
                request.CurriculumId,
                request.CourseId,
                request.AcademicTermId,
                ct);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private async Task<ExamCurriculumTemplateDto> BuildTemplateResponseAsync(
        int curriculumId,
        int courseId,
        int academicTermId,
        CancellationToken ct)
    {
        var curriculum = await _context.Curricula
            .AsNoTracking()
            .Include(c => c.Program)
                .ThenInclude(p => p.Department)
            .FirstOrDefaultAsync(c => c.Id == curriculumId, ct)
            ?? throw new KeyNotFoundException("Müfredat bulunamadı.");

        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId, ct)
            ?? throw new KeyNotFoundException("Ders bulunamadı.");

        var term = await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == academicTermId, ct)
            ?? throw new KeyNotFoundException("Akademik dönem bulunamadı.");

        var components = await _context.GradeComponents
            .AsNoTracking()
            .Where(gc => gc.CourseId == courseId && gc.AcademicTermId == academicTermId)
            .OrderBy(gc => gc.Order)
            .ThenBy(gc => gc.Id)
            .Select(gc => new ExamCurriculumComponentDto(
                gc.Id,
                gc.Name,
                gc.ExamType,
                gc.Weight,
                gc.Order,
                gc.IsRequired,
                gc.MinimumScore,
                gc.RequiresAttendanceEligibility,
                gc.PrerequisiteGradeComponentId
            ))
            .ToListAsync(ct);

        var criteria = await _context.GradingCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(gc => gc.CourseId == courseId && gc.AcademicTermId == academicTermId, ct);

        var totalWeight = components.Sum(c => c.Weight);

        return new ExamCurriculumTemplateDto(
            curriculum.Id,
            curriculum.Name ?? $"{curriculum.Program.Name} {curriculum.Version}",
            curriculum.ProgramId,
            curriculum.Program.Name,
            curriculum.Program.DepartmentId,
            curriculum.Program.Department.Name,
            course.Id,
            course.Code,
            course.Name,
            term.Id,
            term.Name,
            criteria?.CalculationMethod ?? "Absolute",
            totalWeight,
            totalWeight == 100,
            components
        );
    }

    private async Task<IActionResult?> ValidateCurriculumCourseAccessAsync(
        int userId,
        int curriculumId,
        int courseId,
        CancellationToken ct)
    {
        var curriculumDepartmentId = await _context.Curricula
            .AsNoTracking()
            .Where(c => c.Id == curriculumId)
            .Select(c => (int?)c.Program.DepartmentId)
            .FirstOrDefaultAsync(ct);

        if (curriculumDepartmentId == null)
            return new NotFoundObjectResult(new { message = "Müfredat bulunamadı." });

        if (!await HasDepartmentAccessAsync(userId, curriculumDepartmentId.Value, ct))
            return new ObjectResult(new
            {
                message = "Seçilen müfredatın bölümüne erişim yetkiniz bulunmuyor."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };

        var courseBelongsToCurriculum = await _context.CurriculumCourses
            .AsNoTracking()
            .AnyAsync(cc => cc.CurriculumId == curriculumId && cc.CourseId == courseId, ct);

        if (!courseBelongsToCurriculum)
        {
            return new BadRequestObjectResult(new
            {
                message = "Seçilen ders bu müfredatta bulunmuyor."
            });
        }

        return null;
    }

    private static void ValidateSaveRequest(SaveExamCurriculumTemplateRequest request)
    {
        if (request.CurriculumId <= 0)
            throw new InvalidOperationException("Lütfen müfredat seçin.");

        if (request.CourseId <= 0)
            throw new InvalidOperationException("Lütfen ders seçin.");

        if (request.AcademicTermId <= 0)
            throw new InvalidOperationException("Lütfen akademik dönem seçin.");

        var calculationMethod = NormalizeCalculationMethod(request.CalculationMethod);

        if (calculationMethod is not ("Absolute" or "Relative"))
            throw new InvalidOperationException("Hesaplama yöntemi Mutlak veya Bağıl olmalıdır.");

        if (request.Components == null || request.Components.Count == 0)
            throw new InvalidOperationException("En az bir sınav türü eklemelisiniz.");

        var duplicateNames = request.Components
            .GroupBy(c => c.Name.Trim().ToLocaleSafe())
            .Where(g => g.Count() > 1)
            .Select(g => g.First().Name)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Aynı isimde birden fazla sınav türü eklenemez: {string.Join(", ", duplicateNames)}");
        }

        foreach (var component in request.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Name))
                throw new InvalidOperationException("Sınav türü adı boş olamaz.");

            if (string.IsNullOrWhiteSpace(component.ExamType))
                throw new InvalidOperationException($"{component.Name} için sınav tipi seçilmelidir.");

            if (component.Weight <= 0 || component.Weight > 100)
                throw new InvalidOperationException($"{component.Name} ağırlığı 0 ile 100 arasında olmalıdır.");

            if (component.MinimumScore.HasValue &&
                (component.MinimumScore.Value < 0 || component.MinimumScore.Value > 100))
            {
                throw new InvalidOperationException($"{component.Name} baraj puanı 0 ile 100 arasında olmalıdır.");
            }
        }

        var totalWeight = request.Components.Sum(c => c.Weight);

        if (totalWeight != 100)
        {
            throw new InvalidOperationException(
                $"Sınav ağırlıkları toplamı 100 olmalıdır. Şu anki toplam: {totalWeight:0.##}");
        }
    }

    private static string NormalizeCalculationMethod(string? calculationMethod)
    {
        if (string.Equals(calculationMethod, "Relative", StringComparison.OrdinalIgnoreCase))
            return "Relative";

        return "Absolute";
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    private async Task<bool> HasDepartmentAccessAsync(int userId, int departmentId, CancellationToken ct)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return true;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null)
            return false;

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId, ct);

            return department != null && department.FacultyId == instructor.Department.FacultyId;
        }

        if (roles.Contains("DepartmentManager"))
            return instructor.DepartmentId == departmentId;

        return false;
    }
}

public record ExamCurriculumTermDto(
    int Id,
    string Name,
    string ShortName,
    int Year,
    string TermType
);

public record ExamCurriculumLookupDto(
    int Id,
    string Name,
    string Version,
    int ProgramId,
    string ProgramName,
    int DepartmentId,
    string DepartmentName,
    bool IsActive
);

public record ExamCurriculumCourseLookupDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int SuggestedSemester,
    bool IsCompulsory,
    int Ects
);

public record ExamCurriculumTemplateDto(
    int CurriculumId,
    string CurriculumName,
    int ProgramId,
    string ProgramName,
    int DepartmentId,
    string DepartmentName,
    int CourseId,
    string CourseCode,
    string CourseName,
    int AcademicTermId,
    string AcademicTermName,
    string CalculationMethod,
    decimal TotalWeight,
    bool IsComplete,
    IReadOnlyList<ExamCurriculumComponentDto> Components
);

public record ExamCurriculumComponentDto(
    int Id,
    string Name,
    string ExamType,
    decimal Weight,
    int Order,
    bool IsRequired,
    decimal? MinimumScore,
    bool RequiresAttendanceEligibility,
    int? PrerequisiteGradeComponentId
);

public record SaveExamCurriculumTemplateRequest(
    int CurriculumId,
    int CourseId,
    int AcademicTermId,
    string CalculationMethod,
    List<SaveExamCurriculumComponentRequest> Components
);

public record SaveExamCurriculumComponentRequest(
    int? Id,
    string? ClientKey,
    string Name,
    string ExamType,
    decimal Weight,
    int Order,
    bool IsRequired,
    decimal? MinimumScore,
    bool RequiresAttendanceEligibility,
    int? PrerequisiteGradeComponentId,
    string? PrerequisiteClientKey
);

file static class StringExtensions
{
    public static string ToLocaleSafe(this string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
