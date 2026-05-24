using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIS.API.Authorization;
using SIS.API.Filters;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/students")]
[Authorize]
public class StudentManagementController : ControllerBase
{
    // Config "StudentPhotos:MaxBytes" yoksa kullanılan varsayılan (2 MB).
    private const long DefaultMaxPhotoBytes = 2_000_000;

    // Pipeline-level hard ceiling (config'ten daha gevşek). Asıl boyut kontrolü
    // StudentPhotoValidator içinde config değeriyle yapılır; bu attribute sadece
    // aşırı büyük gövdeleri erken reddeder. Attribute argümanı compile-time sabit
    // olmak zorunda olduğu için ayrı tutuldu.
    private const long MaxPhotoRequestBytes = 10_000_000;

    // Config "StudentDocuments:MaxBytes" yoksa kullanılan varsayılan (5 MB).
    private const long DefaultMaxDocumentBytes = 5_000_000;

    // Pipeline-level hard ceiling for document uploads (config'ten daha gevşek).
    // Asıl boyut kontrolü StudentDocumentValidator içinde config değeriyle yapılır.
    private const long MaxDocumentRequestBytes = 20_000_000;

    private readonly IAdminStudentService _service;
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _systemConfig;
    private readonly IAcademicScopeGuard _scopeGuard;
    private readonly IStudentPersonalInfoService _personalInfoService;
    private readonly IStudentPhotoStorage _photoStorage;
    private readonly IStudentDocumentService _documentService;
    private readonly IStudentDocumentStorage _documentStorage;
    private readonly IConfiguration _configuration;

    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        ISystemConfigProvider systemConfig)
        : this(service, context, systemConfig, new AcademicScopeGuard(context), new StudentPersonalInfoService(context), new NullStudentPhotoStorage(), new StudentDocumentService(context), new NullStudentDocumentStorage(), new ConfigurationBuilder().Build())
    {
    }

    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard)
        : this(service, context, new NullSystemConfigProvider(), scopeGuard, new StudentPersonalInfoService(context), new NullStudentPhotoStorage(), new StudentDocumentService(context), new NullStudentDocumentStorage(), new ConfigurationBuilder().Build())
    {
    }

    // Test kolaylığı: scope guard + personal-info + photo storage enjekte edilebilir.
    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard,
        IStudentPersonalInfoService personalInfoService,
        IStudentPhotoStorage photoStorage)
        : this(service, context, new NullSystemConfigProvider(), scopeGuard, personalInfoService, photoStorage, new StudentDocumentService(context), new NullStudentDocumentStorage(), new ConfigurationBuilder().Build())
    {
    }

    // Test kolaylığı: belge servisi + belge depolaması enjekte edilebilir.
    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard,
        IStudentDocumentService documentService,
        IStudentDocumentStorage documentStorage,
        IConfiguration configuration)
        : this(service, context, new NullSystemConfigProvider(), scopeGuard, new StudentPersonalInfoService(context), new NullStudentPhotoStorage(), documentService, documentStorage, configuration)
    {
    }

    // Birden fazla constructor DI tarafından çözülebildiği için ambiguity oluşuyordu;
    // DI'nin kullanacağı kanonik constructor bu (diğerleri test kolaylığı için).
    [ActivatorUtilitiesConstructor]
    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        ISystemConfigProvider systemConfig,
        IAcademicScopeGuard scopeGuard,
        IStudentPersonalInfoService personalInfoService,
        IStudentPhotoStorage photoStorage,
        IStudentDocumentService documentService,
        IStudentDocumentStorage documentStorage,
        IConfiguration configuration)
    {
        _service = service;
        _context = context;
        _systemConfig = systemConfig;
        _scopeGuard = scopeGuard;
        _personalInfoService = personalInfoService;
        _photoStorage = photoStorage;
        _documentService = documentService;
        _documentStorage = documentStorage;
        _configuration = configuration;
    }

    [HttpGet]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student")]
    public async Task<IActionResult> GetStudents([FromQuery] AdminStudentFilterDto filter)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        // Dean/DepartmentManager: departmentId filtresi zorunlu kıl
        var scope = await _scopeGuard.ResolveAsync(User, userId);
        if (scope.IsForbidden) return Forbid();
        if (filter.DepartmentId.HasValue && !scope.IsGlobal && !scope.DepartmentIds.Contains(filter.DepartmentId.Value))
            return Forbid();

        var appliedFilter = scope.IsGlobal
            ? filter
            : filter with { DepartmentIds = scope.DepartmentIds };

        var result = await _service.GetStudentsAsync(appliedFilter);
        if (!await ShouldMaskTranscriptSummaryAsync())
            return Ok(result);

        var items = result.Items
            .Select(item => new
            {
                item.Id,
                item.StudentNumber,
                item.FirstName,
                item.LastName,
                item.Email,
                Cgpa = (decimal?)null,
                CompletedSemesters = (int?)null,
                item.Status,
                item.ProgramName,
                item.ProgramCode,
                item.DepartmentName,
                item.AdvisorName,
                TranscriptAccessible = false,
                TranscriptAccessMessage = "Akademisyen transkript erisimi sistem ayari nedeniyle kapali."
            })
            .Cast<object>()
            .ToList();

        return Ok(new PagedResult<object>(items, result.TotalCount, result.Page, result.PageSize));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student", EntityIdRouteParam = "id")]
    public async Task<IActionResult> GetStudent(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var result = await _service.GetStudentByIdAsync(id);
        if (result == null) return NotFound(new { message = "Öğrenci bulunamadı." });

        // Dean/DepartmentManager erişim kontrolü
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (!roles.Contains("SystemAdministrator") && !roles.Contains("AdministrativeStaff"))
        {
            var studentDeptId = await _context.StudentPrograms
                .AsNoTracking()
                .Include(sp => sp.Program)
                .Where(sp => sp.StudentId == id && sp.IsActive && sp.Type == Domain.Enums.StudentProgramType.Main)
                .Select(sp => (int?)sp.Program.DepartmentId)
                .FirstOrDefaultAsync();

            if (studentDeptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, studentDeptId.Value)) return Forbid();
        }

        if (!await ShouldMaskTranscriptSummaryAsync())
            return Ok(result);

        return Ok(new
        {
            result.Id,
            result.UserId,
            result.StudentNumber,
            result.FirstName,
            result.LastName,
            result.Email,
            Cgpa = (decimal?)null,
            Sgpa = (decimal?)null,
            CompletedSemesters = (int?)null,
            FrozenSemesterCount = (int?)null,
            result.Status,
            result.IsScholarshipStudent,
            result.HasFinancialBlock,
            result.ProgramName,
            result.ProgramCode,
            result.DepartmentName,
            result.AdvisorName,
            result.AdvisorId,
            result.ProgramId,
            result.CurriculumId,
            result.ProgramType,
            TranscriptAccessible = false,
            TranscriptAccessMessage = "Akademisyen transkript erisimi sistem ayari nedeniyle kapali."
        });
    }

    [HttpPost]
    [RequirePermission("menu.ogrenci-rehberi.create")]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
    {
        try
        {
            var result = await _service.CreateStudentAsync(dto);
            return CreatedAtAction(nameof(GetStudent), new { id = result.Id }, result);
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

[HttpPut("{id:int}/advisor")]
[RequirePermission("menu.toplu-danisman-atama.assign")]
public async Task<IActionResult> AssignAdvisor(int id, [FromBody] AssignAdvisorDto dto)
{
    try
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var studentDepartmentId = await GetStudentMainDepartmentIdAsync(id);
        if (studentDepartmentId == null)
            return NotFound(new { message = "Öğrenci bulunamadı." });

        if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, studentDepartmentId.Value))
            return Forbid();

        var advisorDepartmentId = await GetAdvisorDepartmentIdAsync(dto.AdvisorId);
        if (advisorDepartmentId == null)
            return NotFound(new { message = $"Danışman bulunamadı: {dto.AdvisorId}" });

        if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, advisorDepartmentId.Value))
            return Forbid();

        await _service.AssignAdvisorAsync(id, dto.AdvisorId);
        return NoContent();
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

    [HttpPut("{id:int}/academic")]
    [RequirePermission("menu.ogrenci-rehberi.edit-academic")]
    public async Task<IActionResult> UpdateAcademic(int id, [FromBody] UpdateStudentAcademicDto dto)
    {
        try
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var currentDeptId = await GetStudentMainDepartmentIdAsync(id);
            if (currentDeptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, currentDeptId.Value)) return Forbid();

            var targetDeptId = await _context.AcademicPrograms
                .Where(p => p.Id == dto.ProgramId)
                .Select(p => (int?)p.DepartmentId)
                .FirstOrDefaultAsync();
            if (targetDeptId == null) return NotFound(new { message = $"Program bulunamadı: {dto.ProgramId}" });
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, targetDeptId.Value)) return Forbid();

            await _service.UpdateAcademicAsync(id, dto);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/student-number")]
    [RequirePermission("menu.ogrenci-no-degistirme.change")]
    public async Task<IActionResult> ChangeStudentNumber(int id, [FromBody] ChangeStudentNumberDto dto)
    {
        try
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var deptId = await GetStudentMainDepartmentIdAsync(id);
            if (deptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, deptId.Value)) return Forbid();
            await _service.ChangeStudentNumberAsync(id, dto.NewStudentNumber);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:int}/personal-info")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student", EntityIdRouteParam = "id")]
    public async Task<IActionResult> GetPersonalInfo(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        // Dean/DepartmentManager erişim kontrolü (admin rolleri bypass eder)
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (!roles.Contains("SystemAdministrator") && !roles.Contains("AdministrativeStaff"))
        {
            var deptId = await GetStudentMainDepartmentIdAsync(id);
            if (deptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, deptId.Value)) return Forbid();
        }

        var result = await _personalInfoService.GetAsync(id);
        if (result == null) return NotFound(new { message = "Öğrenci bulunamadı." });

        return Ok(result);
    }

    [HttpPut("{id:int}/personal-info")]
    [RequirePermission("menu.ogrenci-rehberi.edit-personal")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student", EntityIdRouteParam = "id")]
    public async Task<IActionResult> UpdatePersonalInfo(int id, [FromBody] UpdateStudentPersonalInfoDto dto)
    {
        try
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            // Dean/DepartmentManager erişim kontrolü (admin rolleri bypass eder)
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            if (!roles.Contains("SystemAdministrator") && !roles.Contains("AdministrativeStaff"))
            {
                var deptId = await GetStudentMainDepartmentIdAsync(id);
                if (deptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
                if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, deptId.Value)) return Forbid();
            }

            await _personalInfoService.UpsertAsync(id, dto);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/photo")]
    [RequirePermission("menu.ogrenci-rehberi.edit-personal")]
    [RequestSizeLimit(MaxPhotoRequestBytes)]
    public async Task<IActionResult> UploadPhoto(int id, IFormFile? file, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var accessError = await CheckStudentAccessAsync(id, userId, ct);
        if (accessError is not null) return accessError;

        var maxBytes = ResolveMaxPhotoBytes();

        // Ucuz erken red: client'ın bildirdiği boyut zaten sınırı aşıyorsa buffer'lamaya gerek yok.
        // Bu yalnızca bir optimizasyon — asıl/yetkili kontrol aşağıda buffered.Length üzerinden yapılır
        // (forged düşük Content-Length bypass'ını engellemek için).
        if (file != null && file.Length > maxBytes)
            return BadRequest(new { message = $"Dosya boyutu üst sınırı ({maxBytes} byte) aşıyor." });

        // Header'ı okumak için akışı seekable bir buffer'a al.
        await using var buffered = new MemoryStream();
        if (file is not null && file.Length > 0)
        {
            await using var upload = file.OpenReadStream();
            await upload.CopyToAsync(buffered, ct);
        }
        buffered.Position = 0;

        var header = new byte[StudentPhotoValidator.HeaderSize];
        var headerLen = await ReadHeaderAsync(buffered, header, ct);
        buffered.Position = 0;

        // Yetkili boyut kontrolü: client-controlled file.Length değil, gerçekte buffer'a
        // kopyalanan byte sayısı (buffered.Length) kullanılır.
        var validationError = StudentPhotoValidator.Validate(
            hasFile: file is not null,
            sizeBytes: buffered.Length,
            maxBytes: maxBytes,
            contentType: file?.ContentType,
            header: header.AsSpan(0, headerLen));
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var extension = await _photoStorage.SaveAsync(id, buffered, file!.ContentType, ct);
        await SetPhotoExtensionAsync(id, extension, ct);

        return NoContent();
    }

    [HttpGet("{id:int}/photo")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student", EntityIdRouteParam = "id")]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var accessError = await CheckStudentAccessAsync(id, userId, ct);
        if (accessError is not null) return accessError;

        var extension = await _context.StudentPersonalInfos
            .AsNoTracking()
            .Where(p => p.StudentId == id)
            .Select(p => p.PhotoExtension)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(extension))
            return NotFound(new { message = "Fotoğraf bulunamadı." });

        var photo = await _photoStorage.GetAsync(id, extension, ct);
        if (photo is null)
            return NotFound(new { message = "Fotoğraf bulunamadı." });

        // Görsel asla indirme/çalıştırılabilir olarak ele alınmasın diye explicit inline.
        // (X-Content-Type-Options: nosniff zaten SecurityHeadersMiddleware'de global olarak ekleniyor.)
        Response.Headers["Content-Disposition"] = "inline";
        return File(photo.Value.Stream, photo.Value.ContentType);
    }

    [HttpDelete("{id:int}/photo")]
    [RequirePermission("menu.ogrenci-rehberi.edit-personal")]
    public async Task<IActionResult> DeletePhoto(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var accessError = await CheckStudentAccessAsync(id, userId, ct);
        if (accessError is not null) return accessError;

        var info = await _context.StudentPersonalInfos
            .FirstOrDefaultAsync(p => p.StudentId == id, ct);

        var extension = info?.PhotoExtension;
        if (!string.IsNullOrWhiteSpace(extension))
            await _photoStorage.DeleteAsync(id, extension, ct);

        if (info is not null && info.PhotoExtension is not null)
        {
            info.PhotoExtension = null;
            info.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    // ── Belge yükleme / sunma / silme ───────────────────────────────────────

    [HttpGet("{id:int}/documents")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    public async Task<IActionResult> GetDocuments(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var accessError = await CheckStudentAccessAsync(id, userId, ct);
        if (accessError is not null) return accessError;

        var status = await _documentService.GetStatusAsync(id, ct);
        if (status is null)
            return NotFound(new { message = "Öğrenci bulunamadı." });

        return Ok(status);
    }

    [HttpPost("{id:int}/documents/{documentTypeId:int}")]
    [RequirePermission("menu.ogrenci-rehberi.edit-documents")]
    [RequestSizeLimit(MaxDocumentRequestBytes)]
    public async Task<IActionResult> UploadDocument(int id, int documentTypeId, IFormFile? file, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var accessError = await CheckStudentAccessAsync(id, userId, ct);
        if (accessError is not null) return accessError;

        var maxBytes = ResolveMaxDocumentBytes();

        // Ucuz erken red: client'ın bildirdiği boyut zaten sınırı aşıyorsa buffer'lamaya gerek yok.
        // Asıl/yetkili kontrol aşağıda buffered.Length üzerinden yapılır
        // (forged düşük Content-Length bypass'ını engellemek için).
        if (file != null && file.Length > maxBytes)
            return BadRequest(new { message = $"Dosya boyutu üst sınırı ({maxBytes} byte) aşıyor." });

        // Header'ı okumak için akışı seekable bir buffer'a al.
        await using var buffered = new MemoryStream();
        if (file is not null && file.Length > 0)
        {
            await using var upload = file.OpenReadStream();
            await upload.CopyToAsync(buffered, ct);
        }
        buffered.Position = 0;

        var header = new byte[StudentDocumentValidator.HeaderSize];
        var headerLen = await ReadHeaderAsync(buffered, header, ct);
        buffered.Position = 0;

        // Yetkili boyut kontrolü: client-controlled file.Length değil, gerçekte buffer'a
        // kopyalanan byte sayısı (buffered.Length) kullanılır.
        var validationError = StudentDocumentValidator.Validate(
            hasFile: file is not null,
            sizeBytes: buffered.Length,
            maxBytes: maxBytes,
            contentType: file?.ContentType,
            header: header.AsSpan(0, headerLen));
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        // Dosya adı docId içerdiğinden önce kaydı upsert edip docId'yi al. RecordUploadAsync
        // öğrenci + belge tipinin varlığını doğrular (KeyNotFound → 404). Content-type'tan
        // türetilen uzantı placeholder olarak yazılır; SaveAsync aynı uzantıyı döner.
        var ext = ExtensionFromContentType(file!.ContentType);
        int docId;
        try
        {
            docId = await _documentService.RecordUploadAsync(id, documentTypeId, ext, file.FileName, userId, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        await _documentStorage.SaveAsync(id, docId, buffered, file.ContentType, ct);

        return NoContent();
    }

    [HttpGet("{id:int}/documents/{documentId:int}")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student", EntityIdRouteParam = "id")]
    public async Task<IActionResult> GetDocument(int id, int documentId, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var accessError = await CheckStudentAccessAsync(id, userId, ct);
        if (accessError is not null) return accessError;

        // Owner-scoped: belge yalnızca verilen öğrenciye aitse döner (IDOR koruması).
        var doc = await _documentService.GetDocumentAsync(id, documentId, ct);
        if (doc is null)
            return NotFound(new { message = "Belge bulunamadı." });

        var stored = await _documentStorage.GetAsync(id, documentId, doc.FileExtension, ct);
        if (stored is null)
            return NotFound(new { message = "Belge bulunamadı." });

        // Belge asla zorla indirme/çalıştırılabilir olarak ele alınmasın diye explicit inline.
        // (X-Content-Type-Options: nosniff zaten SecurityHeadersMiddleware'de global olarak ekleniyor.)
        Response.Headers["Content-Disposition"] = "inline";
        return File(stored.Value.Stream, stored.Value.ContentType);
    }

    [HttpDelete("{id:int}/documents/{documentId:int}")]
    [RequirePermission("menu.ogrenci-rehberi.edit-documents")]
    public async Task<IActionResult> DeleteDocument(int id, int documentId, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var accessError = await CheckStudentAccessAsync(id, userId, ct);
        if (accessError is not null) return accessError;

        // Owner-scoped: belge yalnızca verilen öğrenciye aitse silinir (IDOR koruması).
        var doc = await _documentService.GetDocumentAsync(id, documentId, ct);
        if (doc is null)
            return NotFound(new { message = "Belge bulunamadı." });

        await _documentStorage.DeleteAsync(id, documentId, doc.FileExtension, ct);
        await _documentService.RemoveAsync(id, documentId, ct);

        return NoContent();
    }

    /// <summary>
    /// Content-type'tan belge uzantısını türetir ("pdf"/"jpg"/"png"). Bu noktaya
    /// gelindiğinde content-type zaten StudentDocumentValidator tarafından whitelist'lendiği
    /// için fallback "pdf" pratikte oluşmaz.
    /// </summary>
    private static string ExtensionFromContentType(string? contentType) => contentType?.ToLowerInvariant() switch
    {
        StudentDocumentValidator.JpegContentType => "jpg",
        StudentDocumentValidator.PngContentType => "png",
        _ => "pdf",
    };

    private long ResolveMaxDocumentBytes()
    {
        var raw = _configuration["StudentDocuments:MaxBytes"];
        return long.TryParse(raw, out var parsed) && parsed > 0 ? parsed : DefaultMaxDocumentBytes;
    }

    /// <summary>
    /// GetStudent rol-bypass scope desenini uygular. Erişim varsa null döner;
    /// yoksa uygun IActionResult (NotFound/Forbid).
    /// </summary>
    private async Task<IActionResult?> CheckStudentAccessAsync(int studentId, int userId, CancellationToken ct)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
        {
            var exists = await _context.Students.AsNoTracking().AnyAsync(s => s.Id == studentId, ct);
            return exists ? null : NotFound(new { message = "Öğrenci bulunamadı." });
        }

        var deptId = await GetStudentMainDepartmentIdAsync(studentId);
        if (deptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
        if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, deptId.Value, ct)) return Forbid();
        return null;
    }

    /// <summary>
    /// PhotoExtension'ı diğer alanları silmeden ayrıca günceller (UpsertAsync tüm
    /// alanları DTO'dan ezdiği için kullanılmaz). Row yoksa oluşturur.
    /// </summary>
    private async Task SetPhotoExtensionAsync(int studentId, string extension, CancellationToken ct)
    {
        var info = await _context.StudentPersonalInfos
            .FirstOrDefaultAsync(p => p.StudentId == studentId, ct);

        var now = DateTime.UtcNow;
        if (info is null)
        {
            info = new Domain.Entities.StudentPersonalInfo
            {
                StudentId = studentId,
                PhotoExtension = extension,
                CreatedAt = now,
            };
            _context.StudentPersonalInfos.Add(info);
        }
        else
        {
            info.PhotoExtension = extension;
            info.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(ct);
    }

    private long ResolveMaxPhotoBytes()
    {
        var raw = _configuration["StudentPhotos:MaxBytes"];
        return long.TryParse(raw, out var parsed) && parsed > 0 ? parsed : DefaultMaxPhotoBytes;
    }

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
            if (n == 0) break;
            read += n;
        }
        return read;
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    private async Task<int?> GetStudentMainDepartmentIdAsync(int studentId)
    {
        return await _context.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
            .Where(sp => sp.StudentId == studentId
                && sp.IsActive
                && sp.Type == Domain.Enums.StudentProgramType.Main)
            .Select(sp => (int?)sp.Program.DepartmentId)
            .FirstOrDefaultAsync();
    }

    private async Task<int?> GetAdvisorDepartmentIdAsync(int advisorId)
    {
        return await _context.Instructors
            .AsNoTracking()
            .Where(instructor => instructor.Id == advisorId)
            .Select(instructor => (int?)instructor.DepartmentId)
            .FirstOrDefaultAsync();
    }

    private async Task<(int? DepartmentId, bool IsForbidden)> EnforceDepartmentFilterAsync(int userId, int? requestedDepartmentId)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return (requestedDepartmentId, false);

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (instructor == null) return (null, true);

        if (roles.Contains("DepartmentManager"))
        {
            if (requestedDepartmentId.HasValue && requestedDepartmentId.Value != instructor.DepartmentId)
                return (null, true);
            return (instructor.DepartmentId, false);
        }

        if (roles.Contains("Dean"))
        {
            if (requestedDepartmentId.HasValue)
            {
                var dept = await _context.Departments.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == requestedDepartmentId.Value);
                if (dept == null || dept.FacultyId != instructor.Department.FacultyId)
                    return (null, true);
            }
            // Dean: filtre olmadan kendi fakültesinin tüm bölümleri gelir (servis tarafında filtrelenir)
            return (requestedDepartmentId, false);
        }

        return (null, true);
    }

    private async Task<bool> HasDepartmentAccessAsync(int userId, int departmentId)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return true;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (instructor == null) return false;

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId);
            return department != null && department.FacultyId == instructor.Department.FacultyId;
        }

        if (roles.Contains("DepartmentManager"))
            return instructor.DepartmentId == departmentId;

        return false;
    }

    private async Task<bool> ShouldMaskTranscriptSummaryAsync(CancellationToken ct = default)
    {
        var transcriptClosed = await _systemConfig.GetBoolAsync("Instructor.TranscriptClosed", false, ct);
        if (!transcriptClosed)
            return false;

        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasCentralAdminRole = roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff");
        return !hasCentralAdminRole && roles.Overlaps(new[] { "Instructor", "AcademicAdvisor", "Dean", "DepartmentManager" });
    }
}

public record AssignAdvisorDto(int AdvisorId);
