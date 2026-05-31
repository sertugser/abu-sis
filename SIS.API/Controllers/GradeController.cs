using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.API.Security;
using SIS.Application.Common.Time;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.Grading;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using ClosedXML.Excel;
using static SIS.Application.Grading.AttendanceAbsenceCalculator;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/instructor")]
[Authorize]
public class GradeController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaService;
    private readonly IDateTimeProvider _clock;
    private readonly IWebModuleControlService _webModuleControls;
    private readonly ISystemConfigProvider _systemConfig;
    private readonly IExamRegulationResolver _examRegulationResolver;
    private readonly IAttendancePolicyResolver _attendancePolicyResolver;

    public GradeController(
        AppDbContext context,
        IGpaCalculationService gpaService,
        IDateTimeProvider clock,
        IExamRegulationResolver examRegulationResolver,
        IAttendancePolicyResolver? attendancePolicyResolver = null,
        ISystemConfigProvider? systemConfig = null,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _gpaService = gpaService;
        _clock = clock;
        _examRegulationResolver = examRegulationResolver;
        _systemConfig = systemConfig ?? new NullSystemConfigProvider();
        _attendancePolicyResolver = attendancePolicyResolver
            ?? new SIS.Infrastructure.Services.AttendancePolicyResolver(context, _systemConfig);
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
    }

    // ══════════════════════════════════════════════════════════
    //  BILEŞEN YÖNETIMI (ders+dönem bazında, tüm section'lar ortak)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// GET api/instructor/section/{sectionId}/components
    /// Dersin not bileşenlerini listeler (Vize, Final, Ödev vb.)
    /// </summary>
    [HttpGet("section/{sectionId}/components")]
    [RequirePermission("menu.not-girisi.view")]
    public async Task<IActionResult> GetComponents(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var components = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .OrderBy(gc => gc.Order)
            .Select(gc => new
            {
                gc.Id,
                gc.Name,
                gc.ShortName,
                gc.Weight,
                gc.Order,
                gc.Type,
                gc.PublishedAt
            })
            .ToListAsync();

        var totalWeight = components.Sum(c => c.Weight);

        return Ok(new
        {
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TotalWeight = totalWeight,
            IsComplete = totalWeight == 100,
            Components = components
        });
    }

    /// <summary>
    /// POST api/instructor/section/{sectionId}/components
    /// Bileşen ekler veya günceller.
    /// Body: { "id": null, "name": "Vize", "weight": 30, "order": 1 }
    /// id verilirse günceller, verilmezse yeni ekler.
    /// </summary>
    [HttpPost("section/{sectionId}/components")]
    [RequirePermission("menu.not-girisi.edit")]
    public async Task<IActionResult> UpsertComponent(int sectionId, [FromBody] ComponentDto dto)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        // Guard: bileşen ekleme/düzenleme iptal edilmiş dönemde yapılamaz
        if (!await CanMutateSharedGradeConfigurationAsync(section))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu ders birden fazla hoca tarafından veriliyor; ortak sınav/not ayarını yalnızca koordinatör hoca değiştirebilir. Koordinatör, bölüm/admin tarafından Hoca Atama ekranından belirlenir." });

        var termIsCancelled = await GetTermIsCancelledAsync(section.TermId);
        if (termIsCancelled == true)
            return BadRequest(new { error = "İptal edilmiş dönemde bileşen değiştirilemez." });

        if (section.IsFinalized)
            return BadRequest(new { error = "Sonuclandirilmis ders icin bilesen degistirilemez. Once sonuclandirmayi iptal edin." });

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Bilesen adi bos olamaz." });

        if (dto.Weight <= 0 || dto.Weight > 100)
            return BadRequest(new { error = "Agirlik 0-100 arasinda olmalidir." });

        GradeComponent component;

        if (dto.Id.HasValue)
        {
            // Güncelle
            component = await _context.GradeComponents
                .FirstOrDefaultAsync(gc => gc.Id == dto.Id.Value
                    && gc.CourseId == section.CourseId
                    && gc.AcademicTermId == section.TermId)
                ?? throw new InvalidOperationException();

            component.Name = dto.Name;
            component.Weight = dto.Weight;
            component.Order = dto.Order;
            component.Type = dto.Type;
            component.ShortName = dto.ShortName ?? string.Empty;
        }
        else
        {
            // Yeni ekle
            component = new GradeComponent
            {
                CourseId = section.CourseId,
                AcademicTermId = section.TermId,
                Name = dto.Name,
                Weight = dto.Weight,
                Order = dto.Order,
                Type = dto.Type,
                ShortName = dto.ShortName ?? string.Empty
            };
            _context.GradeComponents.Add(component);
        }

        await _context.SaveChangesAsync();

        return Ok(new { component.Id, component.Name, component.Weight, component.Order });
    }

    /// <summary>
    /// DELETE api/instructor/section/{sectionId}/components/{componentId}
    /// Bileşeni ve ilişkili notları siler.
    /// </summary>
    [HttpDelete("section/{sectionId}/components/{componentId}")]
    [RequirePermission("menu.not-girisi.edit")]
    public async Task<IActionResult> DeleteComponent(int sectionId, int componentId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!await CanMutateSharedGradeConfigurationAsync(section))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu ders birden fazla hoca tarafından veriliyor; ortak sınav/not ayarını yalnızca koordinatör hoca değiştirebilir. Koordinatör, bölüm/admin tarafından Hoca Atama ekranından belirlenir." });

        var termIsCancelled2 = await GetTermIsCancelledAsync(section.TermId);
        if (termIsCancelled2 == true)
            return BadRequest(new { error = "İptal edilmiş dönemde bileşen silinemez." });

        if (section.IsFinalized)
            return BadRequest(new { error = "Sonuclandirilmis ders icin bilesen silinemez. Once sonuclandirmayi iptal edin." });

        var component = await _context.GradeComponents
            .FirstOrDefaultAsync(gc => gc.Id == componentId
                && gc.CourseId == section.CourseId
                && gc.AcademicTermId == section.TermId);

        if (component == null)
            return NotFound(new { error = "Bilesen bulunamadi." });

        // İlişkili notları sil
        var relatedGrades = await _context.StudentGrades
            .Where(sg => sg.GradeComponentId == componentId)
            .ToListAsync();

        _context.StudentGrades.RemoveRange(relatedGrades);
        _context.GradeComponents.Remove(component);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"'{component.Name}' bileseni ve {relatedGrades.Count} not silindi." });
    }

    // ══════════════════════════════════════════════════════════
    //  NOT GİRİŞİ (section bazında)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// GET api/instructor/section/{sectionId}/grades
    /// Section'daki tüm öğrencilerin tüm bileşen puanlarını getirir.
    /// </summary>
    [HttpGet("section/{sectionId}/grades")]
    [RequirePermission("menu.not-girisi.view")]
    public async Task<IActionResult> GetGrades(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var hideStatistics = await _systemConfig.GetBoolAsync("Instructor.HideGradeEntryStatistics", false);

        var components = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .OrderBy(gc => gc.Order)
            .ToListAsync();

        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.Student)
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn)
            .ToListAsync();

        var studentUserIds = registrations.Select(r => r.Student.UserId).ToList();
        var userMap = await _context.Users
            .Where(u => studentUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var registrationIds = registrations.Select(r => r.Id).ToList();
        var allGrades = await _context.StudentGrades
            .Where(sg => registrationIds.Contains(sg.CourseRegistrationId))
            .ToListAsync();

        var students = registrations.Select(r =>
        {
            var user = userMap.GetValueOrDefault(r.Student.UserId);
            var studentGrades = allGrades.Where(sg => sg.CourseRegistrationId == r.Id).ToList();

            // Ağırlıklı ortalamayı hesapla
            decimal? weightedAverage = null;
            if (!hideStatistics && components.Count > 0 && studentGrades.Any(sg => sg.Score != null))
            {
                decimal totalWeightedScore = 0;
                decimal totalWeight = 0;
                foreach (var comp in components)
                {
                    var grade = studentGrades.FirstOrDefault(sg => sg.GradeComponentId == comp.Id);
                    if (grade?.Score != null)
                    {
                        totalWeightedScore += grade.Score.Value * comp.Weight / 100m;
                        totalWeight += comp.Weight;
                    }
                }
                if (totalWeight > 0)
                    weightedAverage = Math.Round(totalWeightedScore * 100m / totalWeight, 2);
            }

            return new
            {
                StudentId = r.StudentId,
                StudentNumber = r.Student.StudentNumber,
                FirstName = user?.FirstName ?? "",
                LastName = user?.LastName ?? "",
                ClassYear = r.Student.CompletedSemesters / 2 + 1,
                EnrollmentType = r.AppliedElectivePoolId != null ? "Seçmeli" : "Zorunlu",
                LetterGrade = r.Grade?.ToString(),
                WeightedAverage = weightedAverage,
                Scores = components.ToDictionary(
                    c => c.Id.ToString(),
                    c => studentGrades.FirstOrDefault(g => g.GradeComponentId == c.Id)?.Score),
                ExamAttendances = components.ToDictionary(
                    c => c.Id.ToString(),
                    c => (studentGrades.FirstOrDefault(g => g.GradeComponentId == c.Id)?.ExamAttendance
                          ?? ExamAttendance.NotMarked).ToString())
            };
        })
        .OrderBy(s => s.LastName)
        .ThenBy(s => s.FirstName)
        .ToList();

        return Ok(new
        {
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            SectionId = section.Id,
            IsFinalized = section.IsFinalized,
            HideStatistics = hideStatistics,
            Components = components.Select(c => new { c.Id, c.Name, c.Weight, c.Order }),
            Students = students
        });
    }

    /// <summary>
    /// POST api/instructor/section/{sectionId}/grades
    /// Toplu puan girişi — bir bileşen için tüm öğrencilerin puanları.
    /// Body: { "componentId": 1, "grades": [{ "studentId": 5, "score": 75 }, ...] }
    /// </summary>
    [HttpPost("section/{sectionId}/grades")]
    [RequirePermission("menu.not-girisi.submit")]
    public async Task<IActionResult> SubmitGrades(int sectionId, [FromBody] GradeSubmitDto dto)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var term = await _context.AcademicTerms.AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == section.TermId);
        if (term == null)
            return NotFound(new { error = "Dönem bulunamadi." });

        var component = await _context.GradeComponents
            .FirstOrDefaultAsync(gc => gc.Id == dto.ComponentId
                && gc.CourseId == section.CourseId
                && gc.AcademicTermId == section.TermId);

        if (component == null)
            return NotFound(new { error = "Bilesen bulunamadi." });

        var gradeEntryGate = await EnsureGradeEntryModuleOpenAsync(term.Id, component.Name, "Not girişi sadece uygun takvim penceresinde yapılabilir.");
        if (gradeEntryGate is not null)
            return gradeEntryGate;

        if (section.IsFinalized)
            return BadRequest(new { error = "Sonuclandirilmis ders icin not girilemez. Once sonuclandirmayi iptal edin." });

        // Bu section'daki aktif kayıtları al
        var registrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn)
            .ToListAsync();

        var registrationMap = registrations.ToDictionary(r => r.StudentId);

        // Mevcut notları al
        var registrationIds = registrations.Select(r => r.Id).ToList();
        var existingGrades = await _context.StudentGrades
            .Where(sg => sg.GradeComponentId == dto.ComponentId
                && registrationIds.Contains(sg.CourseRegistrationId))
            .ToDictionaryAsync(sg => sg.CourseRegistrationId);

        int updated = 0;
        int created = 0;

        foreach (var entry in dto.Grades)
        {
            if (!registrationMap.TryGetValue(entry.StudentId, out var registration))
                continue; // Bu section'da olmayan öğrenciyi atla

            if (entry.Score < 0 || entry.Score > 100)
                return BadRequest(new { error = $"Ogrenci {entry.StudentId} icin puan 0-100 arasinda olmalidir." });

            if (existingGrades.TryGetValue(registration.Id, out var existing))
            {
                existing.Score = entry.Score;
                updated++;
            }
            else
            {
                _context.StudentGrades.Add(new StudentGrade
                {
                    CourseRegistrationId = registration.Id,
                    GradeComponentId = dto.ComponentId,
                    Score = entry.Score
                });
                created++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"{created} not eklendi, {updated} not guncellendi.",
            ComponentName = component.Name
        });
    }

    // ══════════════════════════════════════════════════════════
//  EXCEL IMPORT (section bazında)
// ══════════════════════════════════════════════════════════

/// <summary>
/// GET api/instructor/section/{sectionId}/grades/export-template
/// Excel şablonunu indirir.
/// </summary>
[HttpGet("section/{sectionId}/grades/export-template")]
[RequirePermission("menu.not-girisi.export")]
public async Task<IActionResult> ExportTemplate(int sectionId, [FromQuery] int componentId)
{
    var section = await GetAuthorizedSectionAsync(sectionId);
    if (section == null)
        return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

    var term = await _context.AcademicTerms.AsNoTracking()
        .Include(t => t.AcademicCalendar)
        .FirstOrDefaultAsync(t => t.Id == section.TermId);
    if (term == null)
        return NotFound(new { error = "Dönem bulunamadi." });

    var component = await _context.GradeComponents
        .FirstOrDefaultAsync(gc => gc.Id == componentId
            && gc.CourseId == section.CourseId
            && gc.AcademicTermId == section.TermId);

    if (component == null)
        return NotFound(new { error = "Bilesen bulunamadi." });

    var templateGate = await EnsureGradeEntryModuleOpenAsync(term.Id, component.Name, "Excel import sadece uygun takvim penceresinde yapılabilir.");
    if (templateGate is not null)
        return templateGate;

    if (section.IsFinalized)
        return BadRequest(new { error = "Sonuclandirilmis ders icin Excel sablonu olusturulamaz." });

    var registrations = await _context.CourseRegistrations
        .Include(cr => cr.Student)
        .Where(cr => cr.CourseSectionId == sectionId
            && cr.Status != RegistrationStatus.Dropped
            && cr.Status != RegistrationStatus.Draft
            && cr.Status != RegistrationStatus.Withdrawn)
        .ToListAsync();

    var studentUserIds = registrations.Select(r => r.Student.UserId).ToList();

    var userMap = await _context.Users
        .Where(u => studentUserIds.Contains(u.Id))
        .ToDictionaryAsync(u => u.Id);

    var registrationIds = registrations.Select(r => r.Id).ToList();

    var existingGrades = await _context.StudentGrades
        .Where(sg => sg.GradeComponentId == componentId
            && registrationIds.Contains(sg.CourseRegistrationId))
        .ToDictionaryAsync(sg => sg.CourseRegistrationId, sg => sg.Score);

    using var workbook = new XLWorkbook();
    var worksheet = workbook.Worksheets.Add("Grades");

    worksheet.Cell(1, 1).Value = "SectionId";
    worksheet.Cell(1, 2).Value = section.Id;

    worksheet.Cell(2, 1).Value = "ComponentId";
    worksheet.Cell(2, 2).Value = component.Id;

    worksheet.Cell(3, 1).Value = "ComponentName";
    worksheet.Cell(3, 2).Value = component.Name;

    worksheet.Cell(5, 1).Value = "StudentId";
    worksheet.Cell(5, 2).Value = "StudentNumber";
    worksheet.Cell(5, 3).Value = "FirstName";
    worksheet.Cell(5, 4).Value = "LastName";
    worksheet.Cell(5, 5).Value = "CurrentScore";
    worksheet.Cell(5, 6).Value = "NewScore";

    var row = 6;

    foreach (var reg in registrations
        .OrderBy(r => userMap.GetValueOrDefault(r.Student.UserId)?.LastName)
        .ThenBy(r => userMap.GetValueOrDefault(r.Student.UserId)?.FirstName))
    {
        var user = userMap.GetValueOrDefault(reg.Student.UserId);
        existingGrades.TryGetValue(reg.Id, out var currentScore);

        worksheet.Cell(row, 1).Value = reg.StudentId;
        worksheet.Cell(row, 2).Value = reg.Student.StudentNumber;
        worksheet.Cell(row, 3).Value = user?.FirstName ?? "";
        worksheet.Cell(row, 4).Value = user?.LastName ?? "";
        worksheet.Cell(row, 5).Value = currentScore;
        worksheet.Cell(row, 6).Value = "";

        row++;
    }

    worksheet.Columns().AdjustToContents();

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);

    var fileName = $"section_{sectionId}_component_{componentId}_grades.xlsx";

    return File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName);
}

/// <summary>
/// POST api/instructor/section/{sectionId}/grades/import-preview
/// Excel dosyasını parse edip önizleme döner.
/// </summary>
[HttpPost("section/{sectionId}/grades/import-preview")]
[RequirePermission("menu.not-girisi.import")]
[Consumes("multipart/form-data")]
[RequestSizeLimit(FileUploadValidation.MaxExcelImportBytes)]
public async Task<IActionResult> ImportPreview(int sectionId, [FromForm] ImportFileDto request)
{
    var file = request.File;
    var fileValidationError = FileUploadValidation.ValidateXlsx(file);
    if (fileValidationError is not null)
        return BadRequest(new { error = fileValidationError });

    var section = await GetAuthorizedSectionAsync(sectionId);
    if (section == null)
        return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

    if (file == null || file.Length == 0)
        return BadRequest(new { error = "Dosya secilmedi." });

    if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        return BadRequest(new { error = "Sadece .xlsx dosyalari kabul edilir." });

    if (file.Length > 5 * 1024 * 1024)
        return BadRequest(new { error = "Dosya boyutu 5MB'i gecemez." });

    var term = await _context.AcademicTerms.AsNoTracking()
        .Include(t => t.AcademicCalendar)
        .FirstOrDefaultAsync(t => t.Id == section.TermId);
    if (term == null)
        return NotFound(new { error = "Dönem bulunamadi." });

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    using var workbook = new XLWorkbook(stream);
    var workbookValidationError = FileUploadValidation.ValidateWorkbookShape(workbook);
    if (workbookValidationError is not null)
        return BadRequest(new { error = workbookValidationError });

    var worksheet = workbook.Worksheet("Grades");

    var templateSectionIdText = worksheet.Cell(1, 2).GetValue<string>();
    var templateComponentIdText = worksheet.Cell(2, 2).GetValue<string>();

    if (!int.TryParse(templateSectionIdText, out var templateSectionId) || templateSectionId != sectionId)
        return BadRequest(new { error = "Yanlis section sablonu yuklendi." });

    if (!int.TryParse(templateComponentIdText, out var componentId))
        return BadRequest(new { error = "Component bilgisi okunamadi." });

    var component = await _context.GradeComponents
        .FirstOrDefaultAsync(gc => gc.Id == componentId
            && gc.CourseId == section.CourseId
            && gc.AcademicTermId == section.TermId);

    if (component == null)
        return BadRequest(new { error = "Yuklenen sablondaki bilesen bu section icin gecersiz." });

    var importGate = await EnsureGradeEntryModuleOpenAsync(term.Id, component.Name, "Excel import sadece uygun takvim penceresinde yapılabilir.");
    if (importGate is not null)
        return importGate;

    if (section.IsFinalized)
        return BadRequest(new { error = "Sonuclandirilmis ders icin import yapilamaz." });

    // Parse Excel rows into (studentNumber, rawScore) pairs — col2=StudentNumber, col6=NewScore.
    var pairs = new List<(string StudentNumber, string RawScore)>();
    var xlRow = 6;
    while (true)
    {
        var allEmpty =
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 1).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 2).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 3).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 4).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 5).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 6).GetValue<string>());
        if (allEmpty) break;
        var studentNumber = worksheet.Cell(xlRow, 2).GetValue<string>()?.Trim() ?? string.Empty;
        var rawScore = worksheet.Cell(xlRow, 6).GetValue<string>()?.Trim() ?? string.Empty;
        pairs.Add((studentNumber, rawScore));
        xlRow++;
    }

    var preview = await BuildScoreImportPreviewAsync(section, componentId, pairs, default);
    return Ok(preview);
}

/// <summary>
/// POST api/instructor/section/{sectionId}/grades/import-apply
/// Onaylanan Excel notlarını uygular.
/// </summary>
[HttpPost("section/{sectionId}/grades/import-apply")]
[RequirePermission("menu.not-girisi.import")]
[Consumes("multipart/form-data")]
[RequestSizeLimit(FileUploadValidation.MaxExcelImportBytes)]
public async Task<IActionResult> ImportApply(int sectionId, [FromForm] ImportFileDto request)
{
    var file = request.File;
    var fileValidationError = FileUploadValidation.ValidateXlsx(file);
    if (fileValidationError is not null)
        return BadRequest(new { error = fileValidationError });

    var section = await GetAuthorizedSectionAsync(sectionId);

    if (section == null)
        return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

    if (file == null || file.Length == 0)
        return BadRequest(new { error = "Dosya secilmedi." });

    if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        return BadRequest(new { error = "Sadece .xlsx dosyalari kabul edilir." });

    if (file.Length > 5 * 1024 * 1024)
        return BadRequest(new { error = "Dosya boyutu 5MB'i gecemez." });

    var term = await _context.AcademicTerms.AsNoTracking()
        .Include(t => t.AcademicCalendar)
        .FirstOrDefaultAsync(t => t.Id == section.TermId);
    if (term == null)
        return NotFound(new { error = "Dönem bulunamadi." });

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    using var workbook = new XLWorkbook(stream);
    var workbookValidationError = FileUploadValidation.ValidateWorkbookShape(workbook);
    if (workbookValidationError is not null)
        return BadRequest(new { error = workbookValidationError });

    var worksheet = workbook.Worksheet("Grades");

    var templateSectionIdText = worksheet.Cell(1, 2).GetValue<string>();
    var templateComponentIdText = worksheet.Cell(2, 2).GetValue<string>();

    if (!int.TryParse(templateSectionIdText, out var templateSectionId) || templateSectionId != sectionId)
        return BadRequest(new { error = "Yanlis section sablonu yuklendi." });

    if (!int.TryParse(templateComponentIdText, out var componentId))
        return BadRequest(new { error = "Component bilgisi okunamadi." });

    var component = await _context.GradeComponents
        .FirstOrDefaultAsync(gc => gc.Id == componentId
            && gc.CourseId == section.CourseId
            && gc.AcademicTermId == section.TermId);

    if (component == null)
        return BadRequest(new { error = "Yuklenen sablondaki bilesen bu section icin gecersiz." });

    var secondImportGate = await EnsureGradeEntryModuleOpenAsync(term.Id, component.Name, "Excel import sadece uygun takvim penceresinde yapılabilir.");
    if (secondImportGate is not null)
        return secondImportGate;

    if (section.IsFinalized)
        return BadRequest(new { error = "Sonuclandirilmis ders icin import yapilamaz." });

    // Parse Excel rows into (studentNumber, rawScore) pairs — col2=StudentNumber, col6=NewScore.
    var pairs = new List<(string StudentNumber, string RawScore)>();
    var xlRow = 6;
    while (true)
    {
        var allEmpty =
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 1).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 2).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 3).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 4).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 5).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(xlRow, 6).GetValue<string>());
        if (allEmpty) break;
        var studentNumber = worksheet.Cell(xlRow, 2).GetValue<string>()?.Trim() ?? string.Empty;
        var rawScore = worksheet.Cell(xlRow, 6).GetValue<string>()?.Trim() ?? string.Empty;
        pairs.Add((studentNumber, rawScore));
        xlRow++;
    }

    var result = await ApplyScoreImportAsync(section, componentId, pairs, default);
    return Ok(result);
}

// ══════════════════════════════════════════════════════════
//  PASTE IMPORT (Toplu Not Aktar — metin yapıştırma)
// ══════════════════════════════════════════════════════════

/// <summary>
/// POST api/instructor/section/{sectionId}/grades/paste-preview
/// Yapıştırılan metin notlarının önizlemesini döner.
/// </summary>
[HttpPost("section/{sectionId}/grades/paste-preview")]
[RequirePermission("menu.not-girisi.import")]
public async Task<IActionResult> PastePreview(int sectionId, [FromBody] PasteScoresRequest req, CancellationToken ct)
{
    var section = await GetAuthorizedSectionAsync(sectionId);
    if (section == null)
        return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

    // Fix 3: validate componentId before hitting the shared core
    if (req.ComponentId <= 0)
        return BadRequest(new { error = "Geçersiz bileşen kimliği." });

    var component = await _context.GradeComponents
        .FirstOrDefaultAsync(gc => gc.Id == req.ComponentId
            && gc.CourseId == section.CourseId
            && gc.AcademicTermId == section.TermId, ct);

    if (component == null)
        return BadRequest(new { error = "Seçilen sınav kalemi bu derse ait değil." });

    // Fix 1: load term and apply the same guards as Excel ImportPreview
    var term = await _context.AcademicTerms.AsNoTracking()
        .Include(t => t.AcademicCalendar)
        .FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
    if (term == null)
        return NotFound(new { error = "Dönem bulunamadi." });

    var pastePreviewGate = await EnsureGradeEntryModuleOpenAsync(term.Id, component.Name, "Paste import sadece uygun takvim penceresinde yapılabilir.");
    if (pastePreviewGate is not null)
        return pastePreviewGate;

    // Excel ImportPreview does NOT block on IsFinalized (preview is read-only/informational)
    // so we intentionally mirror that behaviour here for PastePreview as well.

    var pairs = ParseScorePaste(req.Text, req.Separator);
    var preview = await BuildScoreImportPreviewAsync(section, req.ComponentId, pairs, ct);
    return Ok(preview);
}

/// <summary>
/// POST api/instructor/section/{sectionId}/grades/paste-apply
/// Yapıştırılan metin notlarını uygular.
/// </summary>
[HttpPost("section/{sectionId}/grades/paste-apply")]
[RequirePermission("menu.not-girisi.import")]
public async Task<IActionResult> PasteApply(int sectionId, [FromBody] PasteScoresRequest req, CancellationToken ct)
{
    var section = await GetAuthorizedSectionAsync(sectionId);
    if (section == null)
        return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

    // Fix 3: validate componentId before hitting the shared core
    if (req.ComponentId <= 0)
        return BadRequest(new { error = "Geçersiz bileşen kimliği." });

    var component = await _context.GradeComponents
        .FirstOrDefaultAsync(gc => gc.Id == req.ComponentId
            && gc.CourseId == section.CourseId
            && gc.AcademicTermId == section.TermId, ct);

    if (component == null)
        return BadRequest(new { error = "Seçilen sınav kalemi bu derse ait değil." });

    // Fix 1: load term and apply the same guards as Excel ImportApply
    var term = await _context.AcademicTerms.AsNoTracking()
        .Include(t => t.AcademicCalendar)
        .FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
    if (term == null)
        return NotFound(new { error = "Dönem bulunamadi." });

    var pasteApplyGate = await EnsureGradeEntryModuleOpenAsync(term.Id, component.Name, "Paste import sadece uygun takvim penceresinde yapılabilir.");
    if (pasteApplyGate is not null)
        return pasteApplyGate;

    if (section.IsFinalized)
        return BadRequest(new { error = "Sonuclandirilmis ders icin import yapilamaz." });

    var pairs = ParseScorePaste(req.Text, req.Separator);
    var result = await ApplyScoreImportAsync(section, req.ComponentId, pairs, ct);
    return Ok(result);
}

/// <summary>
/// "ÖğrenciNo&lt;ayraç&gt;Not" satırlarını çiftlere ayırır.
/// Ayraç boş/null → whitespace ile böl. İlk token = öğrenci no, son token = not.
/// </summary>
private static IReadOnlyList<(string StudentNumber, string RawScore)> ParseScorePaste(string? text, string? separator)
{
    var pairs = new List<(string, string)>();
    if (string.IsNullOrWhiteSpace(text)) return pairs;

    foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
    {
        var line = rawLine.Trim();
        if (line.Length == 0) continue;

        string[] parts = string.IsNullOrEmpty(separator)
            ? line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            : line.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2) { pairs.Add((parts.Length == 1 ? parts[0] : line, string.Empty)); continue; }
        pairs.Add((parts[0], parts[^1]));
    }

    return pairs;
}

/// <summary>
/// Ortak önizleme çekirdeği: (studentNumber, rawScore) çiftlerinden preview üretir.
/// Excel import ve paste import ORTAK — davranış değişmemeli.
/// </summary>
private async Task<object> BuildScoreImportPreviewAsync(
    CourseSection section,
    int componentId,
    IReadOnlyList<(string StudentNumber, string RawScore)> pairs,
    CancellationToken ct)
{
    var sectionId = section.Id;

    var component = await _context.GradeComponents
        .FirstOrDefaultAsync(gc => gc.Id == componentId
            && gc.CourseId == section.CourseId
            && gc.AcademicTermId == section.TermId, ct);

    var registrations = await _context.CourseRegistrations
        .Include(cr => cr.Student)
        .Where(cr => cr.CourseSectionId == sectionId
            && cr.Status != RegistrationStatus.Dropped
            && cr.Status != RegistrationStatus.Draft
            && cr.Status != RegistrationStatus.Withdrawn)
        .ToListAsync(ct);

    // Index by StudentNumber for pair matching
    var regByStudentNumber = registrations
        .ToDictionary(r => r.Student.StudentNumber, r => r, StringComparer.OrdinalIgnoreCase);

    var studentUserIds = registrations.Select(r => r.Student.UserId).ToList();
    var userMap = await _context.Users
        .Where(u => studentUserIds.Contains(u.Id))
        .ToDictionaryAsync(u => u.Id, ct);

    var registrationIds = registrations.Select(r => r.Id).ToList();
    var existingGrades = await _context.StudentGrades
        .Where(sg => sg.GradeComponentId == componentId
            && registrationIds.Contains(sg.CourseRegistrationId))
        .ToDictionaryAsync(sg => sg.CourseRegistrationId, sg => sg.Score, ct);

    var previewRows = new List<object>();
    int validCount = 0;
    int warningCount = 0;
    int errorCount = 0;
    int emptyCount = 0;

    for (var i = 0; i < pairs.Count; i++)
    {
        var (studentNumber, rawScore) = pairs[i];
        var rowNumber = i + 1;

        string status;
        string message;
        decimal? parsedScore = null;
        decimal? currentScore = null;
        // Fix 4: declare resolvedReg once; set it in the branch that succeeds, use below for both
        // StudentId and name resolution — eliminates the redundant second TryGetValue.
        CourseRegistration? resolvedReg = null;

        if (string.IsNullOrWhiteSpace(studentNumber))
        {
            status = "Error";
            message = "Öğrenci numarası boş.";
            errorCount++;
        }
        else if (!regByStudentNumber.TryGetValue(studentNumber, out var registration))
        {
            status = "Warning";
            message = "Kayitsiz ogrenci, bu satir atlanacak.";
            warningCount++;
        }
        else if (string.IsNullOrWhiteSpace(rawScore))
        {
            resolvedReg = registration;
            status = "Empty";
            message = "Bos hucre, mevcut not korunacak.";
            emptyCount++;
            existingGrades.TryGetValue(registration.Id, out var existingEmpty);
            currentScore = existingEmpty;
        }
        else if (!decimal.TryParse(rawScore, out var score))
        {
            resolvedReg = registration;
            status = "Error";
            message = "Not sayisal degil.";
            errorCount++;
        }
        else if (score < 0 || score > 100)
        {
            resolvedReg = registration;
            status = "Error";
            message = "Not 0-100 araliginda olmali.";
            errorCount++;
        }
        else
        {
            resolvedReg = registration;
            parsedScore = score;
            existingGrades.TryGetValue(registration.Id, out var existingScore);
            currentScore = existingScore;

            if (currentScore.HasValue && currentScore.Value != parsedScore.Value)
            {
                status = "Update";
                message = $"Mevcut not guncellenecek ({currentScore.Value} -> {parsedScore.Value}).";
                warningCount++;
            }
            else
            {
                status = "Valid";
                message = "Gecerli satir, kaydedilebilir.";
                validCount++;
            }
        }

        // Fix 2: include StudentId (nullable) and name — resolve from resolvedReg (single lookup)
        int? resolvedStudentId = null;
        string? firstName = null;
        string? lastName = null;
        if (resolvedReg != null)
        {
            resolvedStudentId = resolvedReg.StudentId;
            var user = userMap.GetValueOrDefault(resolvedReg.Student.UserId);
            firstName = user?.FirstName;
            lastName = user?.LastName;
        }

        previewRows.Add(new
        {
            RowNumber = rowNumber,
            StudentNumber = studentNumber,
            StudentId = resolvedStudentId,
            FirstName = firstName,
            LastName = lastName,
            CurrentScore = currentScore,
            NewScore = parsedScore,
            RawNewScore = rawScore,
            Status = status,
            Message = message
        });
    }

    return new
    {
        SectionId = sectionId,
        ComponentId = componentId,
        ComponentName = component?.Name ?? string.Empty,
        TotalRows = previewRows.Count,
        ValidCount = validCount,
        WarningCount = warningCount,
        ErrorCount = errorCount,
        EmptyCount = emptyCount,
        Rows = previewRows
    };
}

/// <summary>
/// Ortak uygulama çekirdeği: (studentNumber, rawScore) çiftlerini veritabanına uygular.
/// Excel import ve paste import ORTAK — davranış değişmemeli.
/// </summary>
private async Task<object> ApplyScoreImportAsync(
    CourseSection section,
    int componentId,
    IReadOnlyList<(string StudentNumber, string RawScore)> pairs,
    CancellationToken ct)
{
    var sectionId = section.Id;

    var registrations = await _context.CourseRegistrations
        .Include(cr => cr.Student)
        .Where(cr => cr.CourseSectionId == sectionId
            && cr.Status != RegistrationStatus.Dropped
            && cr.Status != RegistrationStatus.Draft
            && cr.Status != RegistrationStatus.Withdrawn)
        .ToListAsync(ct);

    // Index by StudentNumber for pair matching
    var regByStudentNumber = registrations
        .ToDictionary(r => r.Student.StudentNumber, r => r, StringComparer.OrdinalIgnoreCase);

    var registrationIds = registrations.Select(r => r.Id).ToList();
    var existingGrades = await _context.StudentGrades
        .Where(sg => sg.GradeComponentId == componentId
            && registrationIds.Contains(sg.CourseRegistrationId))
        .ToDictionaryAsync(sg => sg.CourseRegistrationId, ct);

    int inserted = 0;
    int updated = 0;
    int skipped = 0;

    foreach (var (studentNumber, rawScore) in pairs)
    {
        if (string.IsNullOrWhiteSpace(studentNumber))
        {
            skipped++;
            continue;
        }

        if (!regByStudentNumber.TryGetValue(studentNumber, out var registration))
        {
            skipped++;
            continue;
        }

        if (string.IsNullOrWhiteSpace(rawScore))
        {
            skipped++;
            continue;
        }

        if (!decimal.TryParse(rawScore, out var newScore) || newScore < 0 || newScore > 100)
        {
            skipped++;
            continue;
        }

        if (existingGrades.TryGetValue(registration.Id, out var existingGrade))
        {
            existingGrade.Score = newScore;
            updated++;
        }
        else
        {
            _context.StudentGrades.Add(new StudentGrade
            {
                CourseRegistrationId = registration.Id,
                GradeComponentId = componentId,
                Score = newScore
            });
            inserted++;
        }
    }

    await _context.SaveChangesAsync(ct);

    return new
    {
        message = "Import tamamlandi.",
        SectionId = sectionId,
        ComponentId = componentId,
        Inserted = inserted,
        Updated = updated,
        Skipped = skipped
    };
}
    // ══════════════════════════════════════════════════════════
    //  SINAVLARA GENEL BAKIŞ (Tüm Sınavları Göster)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// GET api/instructor/exams-overview?termId
    /// Hocanın tüm şubelerinin sınav/kalem durumunu tek listede döner.
    /// termId verilmezse aktif dönem kullanılır.
    /// </summary>
    [HttpGet("exams-overview")]
    [RequirePermission("menu.not-girisi.view")]
    public async Task<IActionResult> ExamsOverview([FromQuery] int? termId, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var instructor = await _context.Instructors.AsNoTracking()
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);
        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        // Resolve term: explicit termId or active term
        int resolvedTermId;
        if (termId.HasValue)
        {
            var termExists = await _context.AcademicTerms.AsNoTracking()
                .AnyAsync(t => t.Id == termId.Value, ct);
            if (!termExists)
                return NotFound(new { error = "Donem bulunamadi." });
            resolvedTermId = termId.Value;
        }
        else
        {
            var activeTerm = await _context.AcademicTerms.AsNoTracking()
                .ActiveTerm().FirstOrDefaultAsync(ct);
            if (activeTerm == null)
                return NotFound(new { error = "Aktif donem bulunamadi." });
            resolvedTermId = activeTerm.Id;
        }

        // All sections this instructor teaches in the resolved term
        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.InstructorId == instructor.Id && cs.TermId == resolvedTermId)
            .OrderBy(cs => cs.Course.Code)
            .ThenBy(cs => cs.SectionNumber)
            .ToListAsync(ct);

        if (sections.Count == 0)
            return Ok(Array.Empty<ExamOverviewRowDto>());

        var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var sectionIds = sections.Select(s => s.Id).ToList();

        // Load all GradeComponents for these courses/term at once (shared across sections)
        var allComponents = await _context.GradeComponents
            .AsNoTracking()
            .Where(gc => courseIds.Contains(gc.CourseId) && gc.AcademicTermId == resolvedTermId)
            .OrderBy(gc => gc.Order)
            .ToListAsync(ct);

        var componentsByCourse = allComponents
            .GroupBy(gc => gc.CourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load SectionExam rows for all relevant sections and components in one query
        var allComponentIds = allComponents.Select(c => c.Id).ToList();
        var sectionExams = await _context.SectionExams
            .AsNoTracking()
            .Where(se => sectionIds.Contains(se.CourseSectionId)
                      && allComponentIds.Contains(se.GradeComponentId))
            .ToListAsync(ct);

        // Index by (sectionId, componentId) for O(1) lookup
        var examIndex = sectionExams
            .ToDictionary(se => (se.CourseSectionId, se.GradeComponentId));

        var rows = sections.Select(s =>
        {
            var courseComponents = componentsByCourse.GetValueOrDefault(s.CourseId)
                ?? new List<GradeComponent>();

            var componentDtos = courseComponents.Select(gc =>
            {
                examIndex.TryGetValue((s.Id, gc.Id), out var exam);
                return new ExamOverviewComponentDto(
                    Id: gc.Id,
                    Name: gc.Name,
                    Type: gc.Type,
                    Weight: gc.Weight,
                    PublishedAt: gc.PublishedAt,
                    ExamDate: exam?.ExamDate);
            }).ToList();

            return new ExamOverviewRowDto(
                SectionId: s.Id,
                CourseCode: s.Course.Code,
                CourseName: s.Course.Name,
                SectionNumber: s.SectionNumber,
                IsFinalized: s.IsFinalized,
                Components: componentDtos);
        }).ToList();

        return Ok(rows);
    }

    // ══════════════════════════════════════════════════════════
    //  SINAV YOKLAMASI
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// PUT api/instructor/section/{sectionId}/exam-attendance
    /// Bir bileşen için öğrencilerin sınav yoklamasını toplu işaretler.
    /// </summary>
    [HttpPut("section/{sectionId}/exam-attendance")]
    [RequirePermission("menu.not-girisi.edit")]
    public async Task<IActionResult> SetExamAttendance(int sectionId, [FromBody] SetExamAttendanceRequest dto, CancellationToken ct)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (section.IsFinalized)
            return BadRequest(new { error = "Sonuçlandırılmış derste değiştirilemez. Önce sonuçlandırmayı iptal edin." });

        if (!await CanMutateSharedGradeConfigurationAsync(section))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu ders birden fazla hoca tarafından veriliyor; ortak sınav/not ayarını yalnızca koordinatör hoca değiştirebilir. Koordinatör, bölüm/admin tarafından Hoca Atama ekranından belirlenir." });

        var termIsCancelled = await GetTermIsCancelledAsync(section.TermId);
        if (termIsCancelled == true)
            return BadRequest(new { error = "İptal edilmiş dönemde yoklama değiştirilemez." });

        // Bileşenin bu dersin bu dönemi için geçerli olduğunu doğrula
        var component = await _context.GradeComponents
            .FirstOrDefaultAsync(gc => gc.Id == dto.ComponentId
                && gc.CourseId == section.CourseId
                && gc.AcademicTermId == section.TermId, ct);

        if (component == null)
            return BadRequest(new { error = "Bileşen bu section için geçerli değil." });

        // Bu section'daki aktif kayıtları al (çekilenler hariç)
        var registrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn)
            .ToListAsync(ct);

        var registrationMap = registrations.ToDictionary(r => r.StudentId);

        var registrationIds = registrations.Select(r => r.Id).ToList();

        // Mevcut StudentGrade satırlarını al (bu bileşen için)
        var existingGrades = await _context.StudentGrades
            .Where(sg => sg.GradeComponentId == dto.ComponentId
                && registrationIds.Contains(sg.CourseRegistrationId))
            .ToDictionaryAsync(sg => sg.CourseRegistrationId, ct);

        int updatedCount = 0;

        foreach (var item in dto.Items)
        {
            if (!registrationMap.TryGetValue(item.StudentId, out var registration))
                continue; // Bu section'da aktif kaydı olmayan öğrenciyi atla

            if (existingGrades.TryGetValue(registration.Id, out var existing))
            {
                existing.ExamAttendance = item.Attendance;
            }
            else
            {
                // Satır yok — oluştur; Score null kalır, sadece yoklama set edilir
                _context.StudentGrades.Add(new StudentGrade
                {
                    CourseRegistrationId = registration.Id,
                    GradeComponentId = dto.ComponentId,
                    Score = null,
                    ExamAttendance = item.Attendance
                });
            }

            updatedCount++;
        }

        await _context.SaveChangesAsync(ct);

        return Ok(new
        {
            message = $"{updatedCount} öğrencinin sınav yoklaması güncellendi.",
            ComponentId = dto.ComponentId,
            UpdatedCount = updatedCount
        });
    }

    // ══════════════════════════════════════════════════════════
    //  SONUÇLANDIRMA
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// POST api/instructor/section/{sectionId}/finalize
    /// Section'ı sonuçlandırır:
    /// 1. Ağırlıkların %100 olduğunu kontrol eder
    /// 2. Her öğrencinin ağırlıklı ortalamasını hesaplar
    /// 3. GradingCriteria'ya göre harf notuna çevirir
    /// 4. CourseRegistration.Grade'i yazar
    /// 5. Her öğrencinin CGPA/SGPA'sını günceller
    /// </summary>
    [HttpPost("section/{sectionId}/finalize")]
    [RequirePermission("menu.not-girisi.finalize")]
    public async Task<IActionResult> Finalize(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var term = await _context.AcademicTerms.AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == section.TermId);
        if (term == null)
            return NotFound(new { error = "Dönem bulunamadi." });

        var finalizeGate = await EnsureGradeEntryModuleOpenAsync(term.Id, "Sonuçlandırma sadece 'Not Girişi' döneminde yapılabilir.");
        if (finalizeGate is not null)
            return finalizeGate;

        if (section.IsFinalized)
            return BadRequest(new { error = "Bu section zaten sonuclandirilmis." });

        // 1. Bileşenleri kontrol et
        var components = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .ToListAsync();

        if (components.Count == 0)
            return BadRequest(new { error = "Not bileseni tanimlanmamis. En az bir bilesen ekleyin." });

        var totalWeight = components.Sum(c => c.Weight);
        if (totalWeight != 100)
            return BadRequest(new { error = $"Bilesen agirliklarinin toplami %100 olmalidir. Simdi: %{totalWeight}" });

        // 1b. Sınav yönergesi ağırlık sınırları kontrolü
        var midtermSum = components
            .Where(c => c.Type == GradeComponentType.Midterm)
            .Sum(c => c.Weight);
        var finalSum = components
            .Where(c => c.Type == GradeComponentType.Final)
            .Sum(c => c.Weight);

        var bounds = await _examRegulationResolver.ResolveCourseWeightBoundsAsync(
            section.CourseId, section.TermId, default);

        // Skip weight-bounds enforcement when no component has been classified as Midterm or Final
        // (midtermSum==0 && finalSum==0 means all components are Type=Other — the default after
        // migration). Enforcing bounds in that state would block finalize with a misleading message.
        // When at least one component IS classified, both midterm AND final bounds are enforced.
        if (bounds != null && (midtermSum > 0 || finalSum > 0))
        {
            if (bounds.IsEmpty)
                return BadRequest(new { error = "Bu dersin programları arasında çelişen sınav ağırlık sınırları var. Sınav müfredatını düzeltin." });

            if (midtermSum < bounds.MinMidterm || midtermSum > bounds.MaxMidterm)
                return BadRequest(new { error = $"Toplam ara sınav ağırlığı %{bounds.MinMidterm:0.##}-%{bounds.MaxMidterm:0.##} arasında olmalı (şu an %{midtermSum:0.##})." });

            if (finalSum < bounds.MinFinal || finalSum > bounds.MaxFinal)
                return BadRequest(new { error = $"Toplam final ağırlığı %{bounds.MinFinal:0.##}-%{bounds.MaxFinal:0.##} arasında olmalı (şu an %{finalSum:0.##})." });
        }

        // 2. Harf notu aralıkları: yönerge kilitliyse ya da section criteria yoksa regulation eşikleri kullanılır.
        var (effectiveCriteria, criteriaError) = await ResolveEffectiveCriteriaAsync(section, cancellationToken: default);
        if (criteriaError != null)
            return criteriaError;

        // 3. Section'daki aktif öğrencileri al (çekilenler hariç — GetGrades ile tutarlı)
        var registrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn)
            .ToListAsync();

        var registrationIds = registrations.Select(r => r.Id).ToList();
        var allGrades = await _context.StudentGrades
            .Where(sg => registrationIds.Contains(sg.CourseRegistrationId))
            .ToListAsync();

        var gradesByRegistration = allGrades
            .GroupBy(grade => grade.CourseRegistrationId)
            .ToDictionary(group => group.Key, group => group.ToList());

        // 3b. Devamsızlık NA kontrolü — eksik yoklama girişi olan kayıtlar atlanır, sadece
        //     gerçek devamsızlık kaydı bulunan ve limiti aşan öğrenciler NA alır.
        var attendanceRecordsForSection = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => registrationIds.Contains(ar.CourseRegistrationId))
            .ToListAsync();

        var attendanceByRegistration = attendanceRecordsForSection
            .GroupBy(ar => ar.CourseRegistrationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<AttendanceRecord>)g.ToList());

        // Build ordered schedule slots for this section (mirrors GetStudentAttendanceStatusAsync)
        var sectionScheduleRows = await _context.SectionSchedules
            .AsNoTracking()
            .Where(ss => ss.CourseSectionId == sectionId)
            .Select(ss => new
            {
                ss.BlockType,
                ss.TimeSlot.DayOfWeek,
                ss.TimeSlot.StartTime,
                ss.TimeSlot.SlotOrder
            })
            .ToListAsync();

        var sectionSlots = sectionScheduleRows
            .OrderBy(s => DaysFromMonday(s.DayOfWeek))
            .ThenBy(s => s.StartTime)
            .ThenBy(s => s.SlotOrder)
            .Select((s, index) => new AttendanceSlotInfo(index + 1, s.BlockType))
            .ToList();

        var course = section.Course;
        // Uygulama saatleri devamsızlık sayımında Lab kovasına yazıldığı için lab toplamı onları da içerir
        // (öğrenci Devamsızlık Durumu sayfasıyla birebir aynı hesap).
        var theoryWeeklyHours = Math.Max(0, course.TheoryHours);
        var labWeeklyHours = Math.Max(0, course.LabHours) + Math.Max(0, course.ApplicationHours ?? 0);

        // Devamsızlık politikası dönem/program bazlı: aynı şubedeki farklı program öğrencileri
        // kendi programlarının limitiyle değerlendirilir (global değil, geçmiş dönem dondurulmuş).
        var attendanceStudentIds = registrations.Select(r => r.StudentId).Distinct().ToList();
        var policiesByStudent = await _attendancePolicyResolver.ResolveForStudentsAsync(attendanceStudentIds, section.TermId, default);
        var fallbackPolicy = await _attendancePolicyResolver.ResolveAsync(section.TermId, null, default);

        var naRegistrationIds = new HashSet<int>();

        foreach (var reg in registrations)
        {
            if (!attendanceByRegistration.TryGetValue(reg.Id, out var records) || records.Count == 0)
                continue; // No attendance data → not auto-NA

            var policy = policiesByStudent.GetValueOrDefault(reg.StudentId) ?? fallbackPolicy;
            var theoryTotalHours = policy.TermWeekCount * theoryWeeklyHours;
            var labTotalHours = policy.TermWeekCount * labWeeklyHours;
            var theoryLimitHours = CalculateLimitHours(theoryTotalHours, policy.TheoryAbsenceLimitPercent);
            var labLimitHours = CalculateLimitHours(labTotalHours, policy.LabAbsenceLimitPercent);
            // TotalHours yönteminde birleşik limit teori yüzdesiyle hesaplanır.
            var totalLimitHours = CalculateLimitHours(theoryTotalHours + labTotalHours, policy.TheoryAbsenceLimitPercent);

            var (theoryAbs, labAbs) = CalculateAbsenceHours(
                records,
                course.TheoryHours,
                course.LabHours,
                course.ApplicationHours,
                sectionSlots);

            if (IsFailedByAbsence(policy.Method, theoryAbs, labAbs, theoryLimitHours, labLimitHours, totalLimitHours))
                naRegistrationIds.Add(reg.Id);
        }

        // Apply NA grade to over-limit registrations immediately; exclude from subsequent checks.
        var naResults = new List<object>();
        foreach (var reg in registrations.Where(r => naRegistrationIds.Contains(r.Id)))
        {
            reg.Grade = GradeCode.NA;
            reg.UpdatedAt = DateTime.UtcNow;
            naResults.Add(new { StudentId = reg.StudentId, LetterGrade = GradeCode.NA.ToString() });
        }

        var nonNaRegistrations = registrations.Where(r => !naRegistrationIds.Contains(r.Id)).ToList();

        // 3c. Excused (mazeretli) öğrenciler: bileşenlerinden herhangi birinde ExamAttendance==Excused
        //     olan kayıtlara GradeCode.I (Incomplete) atanır; weighted-average/relative/missing-grade
        //     hesaplarına dahil edilmezler (Faz 4c'de tamamlanacak).
        var excusedRegistrationIds = new HashSet<int>();
        foreach (var reg in nonNaRegistrations)
        {
            var studentGrades = gradesByRegistration.GetValueOrDefault(reg.Id, new List<StudentGrade>());
            if (studentGrades.Any(g => g.ExamAttendance == ExamAttendance.Excused))
                excusedRegistrationIds.Add(reg.Id);
        }

        foreach (var reg in nonNaRegistrations.Where(r => excusedRegistrationIds.Contains(r.Id)))
        {
            reg.Grade = GradeCode.I;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        // Graded registrations = non-NA minus excused; these are the only ones scored/relative-graded.
        var gradedRegistrations = nonNaRegistrations.Where(r => !excusedRegistrationIds.Contains(r.Id)).ToList();

        // Missing-grades check: a component is "missing" for a student only when
        // Score==null AND ExamAttendance != Absent (Absent is a deliberate 0, not a gap).
        var missingGrades = gradedRegistrations
            .SelectMany(reg => components
                .Where(component =>
                {
                    var grade = gradesByRegistration
                        .GetValueOrDefault(reg.Id, new List<StudentGrade>())
                        .FirstOrDefault(g => g.GradeComponentId == component.Id);
                    // Missing = no row OR (Score is null AND not deliberately Absent)
                    return grade == null
                        || (grade.Score == null && grade.ExamAttendance != ExamAttendance.Absent);
                })
                .Select(component => new
                {
                    reg.StudentId,
                    ComponentName = component.Name
                }))
            .ToList();

        if (missingGrades.Count > 0)
        {
            return BadRequest(new
            {
                error = "Tüm öğrencilerin tüm not bileşenleri girilmeden ders sonuçlandırılamaz.",
                missingCount = missingGrades.Count,
                missing = missingGrades.Take(10)
            });
        }

        // 4. Her öğrenci için harf notu hesapla (NA + Excused olanlar zaten işaretlendi, atla)
        // naResults already contains NA entries; add I entries for excused.
        var excusedResults = nonNaRegistrations
            .Where(r => excusedRegistrationIds.Contains(r.Id))
            .Select(r => (object)new { StudentId = r.StudentId, LetterGrade = GradeCode.I.ToString() })
            .ToList();

        var results = new List<object>(naResults.Count + excusedResults.Count + gradedRegistrations.Count);
        results.AddRange(naResults);
        results.AddRange(excusedResults);

        // Bulk-resolve exam regulations for graded (non-NA, non-Excused) students (no per-student N+1)
        var gradedStudentIds = gradedRegistrations.Select(r => r.StudentId).Distinct().ToList();
        var examRegByStudent = await _examRegulationResolver.ResolveForStudentsAsync(
            gradedStudentIds, section.TermId, default);

        // ── Pre-pass: compute roundedScore for every graded registration ────────
        // For Absent components: score is treated as 0 (deliberate, not a gap).
        // roundedScores: regId → (weightedScore, roundedScore, studentGrades list)
        var roundedScores = new Dictionary<int, (decimal WeightedScore, int RoundedScore, List<StudentGrade> Grades)>(gradedRegistrations.Count);
        foreach (var reg in gradedRegistrations)
        {
            var studentGrades = gradesByRegistration.GetValueOrDefault(reg.Id, new List<StudentGrade>());
            examRegByStudent.TryGetValue(reg.StudentId, out var studentRegForPrePass);

            var (weightedScore, roundedScore) = ComputeStudentWeightedScore(
                components, studentGrades, studentRegForPrePass, useMakeupScore: false);

            roundedScores[reg.Id] = (weightedScore, roundedScore, studentGrades);
        }

        // ── Resolve relative policy and decide letter source ─────────────────────
        var relPolicy = await _examRegulationResolver.ResolveCourseRelativePolicyAsync(
            section.CourseId, section.TermId, default);

        // Build relative letters dict if the policy applies and enough students are present.
        IReadOnlyDictionary<int, GradeCode>? relativeLetters = null;
        if (relPolicy != null && !relPolicy.IsConflicting && relPolicy.EvaluationType == GradeEvaluationType.Relative)
        {
            var inputs = gradedRegistrations
                .Select(r => new RelativeGradeInput(r.Id, (decimal)roundedScores[r.Id].RoundedScore))
                .ToList();

            var outcome = RelativeGradingService.Evaluate(inputs, new RelativeGradeParams(
                MinStudentCount: relPolicy.MinStudentCount,
                InclusionLimit: relPolicy.InclusionLimit,
                AbsoluteFloor: relPolicy.AbsoluteFloor,
                TAAMin: relPolicy.TAAMin,
                TBAMin: relPolicy.TBAMin,
                TBBMin: relPolicy.TBBMin,
                TCBMin: relPolicy.TCBMin,
                TCCMin: relPolicy.TCCMin,
                TDCMin: relPolicy.TDCMin,
                TDDMin: relPolicy.TDDMin,
                BelowFloorGrade: relPolicy.BelowFloorGrade));

            if (!outcome.FellBackToAbsolute)
                relativeLetters = outcome.Letters;
            // If FellBackToAbsolute: relativeLetters stays null → absolute path used below
        }

        // ── Per-student assign: base letter + floors + rules ────────────────────
        foreach (var reg in gradedRegistrations)
        {
            var (weightedScore, roundedScore, studentGrades) = roundedScores[reg.Id];

            // Base letter: from relative curve (if active) or absolute criteria.
            // effectiveCriteria is guaranteed non-null here (criteriaError guard above).
            var baseLetter = relativeLetters != null
                ? relativeLetters[reg.Id]
                : effectiveCriteria!.ResolveGrade(roundedScore);

            // Sınav yönergesi: final barajı + geçme notu + Rule C (bulk dict lookup, no N+1)
            examRegByStudent.TryGetValue(reg.StudentId, out var studentReg);

            var finalRaw = ComputeStudentFinalRaw(components, studentGrades, useMakeupScore: false);
            var letterGrade = ApplyGradeRules(baseLetter, roundedScore, finalRaw, studentGrades, components, studentReg);

            reg.Grade = letterGrade;
            reg.UpdatedAt = DateTime.UtcNow;

            results.Add(new
            {
                StudentId = reg.StudentId,
                WeightedAverage = Math.Round(weightedScore, 2),
                LetterGrade = letterGrade.ToString()
            });
        }

        // 5. Section'ı sonuçlandır
        section.IsFinalized = true;
        await _context.SaveChangesAsync();

        // 6. Her öğrencinin CGPA/SGPA'sını güncelle
        var studentIds = registrations.Select(r => r.StudentId).Distinct().ToList();
        foreach (var studentId in studentIds)
        {
            await _gpaService.RecalculateAndUpdateAsync(studentId);
        }

        return Ok(new
        {
            message = $"Section sonuclandirildi. {results.Count} ogrencinin notu kesinlesti.",
            Results = results
        });
    }

    /// <summary>
    /// POST api/instructor/section/{sectionId}/unfinalize
    /// Sonuçlandırmayı iptal eder — harf notları ve CGPA geri alınır.
    /// </summary>
    [HttpPost("section/{sectionId}/unfinalize")]
    [RequirePermission("menu.not-girisi.unfinalize")]
    public async Task<IActionResult> Unfinalize(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var term = await _context.AcademicTerms.AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == section.TermId);
        if (term == null)
            return NotFound(new { error = "Dönem bulunamadi." });

        var unfinalizeGate = await EnsureGradeEntryModuleOpenAsync(term.Id, "Sonuçlandırma iptali sadece 'Not Girişi' döneminde yapılabilir.");
        if (unfinalizeGate is not null)
            return unfinalizeGate;

        if (!section.IsFinalized)
            return BadRequest(new { error = "Bu section zaten sonuclandirilmamis." });

        // Harf notlarını temizle
        var registrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn)
            .ToListAsync();

        foreach (var reg in registrations)
        {
            reg.Grade = null;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        section.IsFinalized = false;
        await _context.SaveChangesAsync();

        // CGPA/SGPA'ları yeniden hesapla (bu section'ın notları artık null olduğu için düşecek)
        var studentIds = registrations.Select(r => r.StudentId).Distinct().ToList();
        foreach (var studentId in studentIds)
        {
            await _gpaService.RecalculateAndUpdateAsync(studentId);
        }

        return Ok(new
        {
            message = $"Sonuclandirma iptal edildi. {registrations.Count} ogrencinin harf notu temizlendi ve ortalamalari yeniden hesaplandi."
        });
    }

    // ══════════════════════════════════════════════════════════
    //  KOMPONENT İLAN AKIŞI (hoca-seviyesi yayın)
    //  Tek-kaynak görünürlük kapısı: WebModuleControl (admin) AND PublishedAt (hoca).
    //  Bkz. StudentController.BuildComponentVisibilityMapAsync.
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// POST api/instructor/components/{componentId}/publish
    /// Bu bileşenin notlarını öğrencilere "ilan eder" — PublishedAt = UtcNow.
    /// Bileşen, dersin TÜM section'larına ortak olduğundan etki ortaktır (Proliz davranışı).
    /// Yetki: hoca, bileşenin (CourseId, AcademicTermId) çiftine ait section'lardan
    /// en az birine atanmış olmalı.
    /// </summary>
    [HttpPost("components/{componentId}/publish")]
    [RequirePermission("menu.not-girisi.publish")]
    public async Task<IActionResult> PublishComponent(int componentId)
    {
        var (component, authError) = await GetAuthorizedComponentAsync(componentId);
        if (authError is not null)
            return authError;

        var publishGate = await EnsureGradeEntryModuleOpenAsync(
            component!.AcademicTermId,
            component.Name,
            "Komponent ilanı sadece uygun takvim penceresinde yapılabilir.");
        if (publishGate is not null)
            return publishGate;

        if (component.PublishedAt.HasValue)
            return Ok(new { message = "Bileşen zaten ilan edilmiş.", publishedAt = component.PublishedAt });

        component.PublishedAt = _clock.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Bileşen ilan edildi.", publishedAt = component.PublishedAt });
    }

    /// <summary>
    /// POST api/instructor/components/{componentId}/unpublish
    /// İlanı geri alır — PublishedAt = null. Öğrenci bu bileşenin notunu ve sınıf
    /// istatistiklerini görmeyi durdurur.
    /// </summary>
    [HttpPost("components/{componentId}/unpublish")]
    [RequirePermission("menu.not-girisi.publish")]
    public async Task<IActionResult> UnpublishComponent(int componentId)
    {
        var (component, authError) = await GetAuthorizedComponentAsync(componentId);
        if (authError is not null)
            return authError;

        var publishGate = await EnsureGradeEntryModuleOpenAsync(
            component!.AcademicTermId,
            component.Name,
            "İlan iptali sadece uygun takvim penceresinde yapılabilir.");
        if (publishGate is not null)
            return publishGate;

        if (!component.PublishedAt.HasValue)
            return Ok(new { message = "Bileşen zaten ilan edilmemiş." });

        component.PublishedAt = null;
        await _context.SaveChangesAsync();

        return Ok(new { message = "İlan geri alındı." });
    }

    /// <summary>
    /// Komponent yetki çözümü: hoca komponentin dersine ait bir section'a atanmış olmalı.
    /// Komponent (CourseId, AcademicTermId) çiftine bağlıdır; aynı dersin tüm section'ları
    /// tek komponent paylaşır. Hoca herhangi bir section'ın hocasıysa yetkilidir.
    /// </summary>
    private async Task<(GradeComponent? Component, IActionResult? Error)> GetAuthorizedComponentAsync(int componentId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return (null, Unauthorized(new { error = "Kullanici bilgisi bulunamadi." }));

        var instructor = await _context.Instructors.AsNoTracking().FirstOrDefaultAsync(i => i.UserId == userId);
        if (instructor == null)
            return (null, Forbid());

        var component = await _context.GradeComponents
            .FirstOrDefaultAsync(c => c.Id == componentId);
        if (component == null)
            return (null, NotFound(new { error = "Bileşen bulunamadı." }));

        var isAuthorized = await _context.CourseSections
            .AsNoTracking()
            .AnyAsync(cs => cs.CourseId == component.CourseId
                         && cs.TermId == component.AcademicTermId
                         && cs.InstructorId == instructor.Id);
        if (!isAuthorized)
            return (null, Forbid());

        return (component, null);
    }

    // ══════════════════════════════════════════════════════════
    //  BÜTÜNLEME (RESIT MAKEUP) AKIŞI
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// GET api/instructor/section/{sectionId}/makeup/butunleme
    /// Bütünleme listesini döner: FF notu almış ve bütünlemeye hak kazanmış öğrenciler.
    ///
    /// Filtre kuralları (sınav yönergesinden, per-student):
    ///   F = AutoTransferFailedToMakeup — false ise öğrenci listeye girmez
    ///   B = BlockMakeupIfNoFinal — true ise finale girmeyen (Absent) öğrenci hariç tutulur
    /// </summary>
    [HttpGet("section/{sectionId}/makeup/butunleme")]
    [RequirePermission("menu.not-girisi.view")]
    public async Task<IActionResult> GetButunlemeRoster(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!section.IsFinalized)
            return BadRequest(new { error = "Bütünleme sadece sonuçlandırılmış section'larda uygulanabilir." });

        // Find the Final-type component for this course/term
        var components = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .OrderBy(gc => gc.Order)
            .ToListAsync();

        var finalComponents = components.Where(c => c.Type == GradeComponentType.Final).ToList();
        var finalComponentId = finalComponents.FirstOrDefault()?.Id;

        // Get all active registrations with Grade=FF (not NA — NA is attendance-fail)
        var ffRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.Student)
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn
                && cr.Grade == GradeCode.FF)
            .ToListAsync();

        if (!ffRegistrations.Any())
        {
            return Ok(new
            {
                SectionId = sectionId,
                FinalComponentId = finalComponentId,
                Students = Array.Empty<object>()
            });
        }

        var ffStudentIds = ffRegistrations.Select(r => r.StudentId).ToList();

        // Bulk-resolve exam regulations per student
        var examRegByStudent = await _examRegulationResolver.ResolveForStudentsAsync(
            ffStudentIds, section.TermId, default);

        // Load StudentGrade rows for these registrations
        var ffRegistrationIds = ffRegistrations.Select(r => r.Id).ToList();
        var allGrades = await _context.StudentGrades
            .Where(sg => ffRegistrationIds.Contains(sg.CourseRegistrationId))
            .ToListAsync();

        var gradesByRegistration = allGrades
            .GroupBy(sg => sg.CourseRegistrationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load user display names
        var studentUserIds = ffRegistrations.Select(r => r.Student.UserId).ToList();
        var userMap = await _context.Users
            .Where(u => studentUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var eligibleStudents = new List<object>();

        foreach (var reg in ffRegistrations)
        {
            examRegByStudent.TryGetValue(reg.StudentId, out var studentReg);

            // F = AutoTransferFailedToMakeup: if false for this student, skip
            if (studentReg == null || !studentReg.AutoTransferFailedToMakeup)
                continue;

            var studentGrades = gradesByRegistration.GetValueOrDefault(reg.Id, new List<StudentGrade>());

            // B = BlockMakeupIfNoFinal: if true and student's Final attendance is Absent, exclude
            if (studentReg.BlockMakeupIfNoFinal && finalComponents.Count > 0)
            {
                var absentFromFinal = finalComponents.Any(fc =>
                {
                    var g = studentGrades.FirstOrDefault(gi => gi.GradeComponentId == fc.Id);
                    return g == null || g.ExamAttendance == ExamAttendance.Absent;
                });
                if (absentFromFinal)
                    continue;
            }

            var user = userMap.GetValueOrDefault(reg.Student.UserId);

            // Build per-component score summary for display
            var vizeScores = components
                .Where(c => c.Type == GradeComponentType.Midterm)
                .Select(c => new
                {
                    ComponentId = c.Id,
                    ComponentName = c.Name,
                    Score = studentGrades.FirstOrDefault(g => g.GradeComponentId == c.Id)?.Score
                })
                .ToList();

            var finalGradeRows = finalComponents.Select(fc =>
            {
                var g = studentGrades.FirstOrDefault(gi => gi.GradeComponentId == fc.Id);
                return new
                {
                    ComponentId = fc.Id,
                    ComponentName = fc.Name,
                    Score = g?.Score,
                    MakeupScore = g?.MakeupScore
                };
            }).ToList();

            eligibleStudents.Add(new
            {
                StudentId = reg.StudentId,
                StudentNumber = reg.Student.StudentNumber,
                FirstName = user?.FirstName ?? string.Empty,
                LastName = user?.LastName ?? string.Empty,
                CurrentGrade = reg.Grade?.ToString(),
                VizeScores = vizeScores,
                FinalScores = finalGradeRows
            });
        }

        return Ok(new
        {
            SectionId = sectionId,
            FinalComponentId = finalComponentId,
            Students = eligibleStudents
        });
    }

    /// <summary>
    /// PUT api/instructor/section/{sectionId}/makeup/butunleme-scores
    /// Bütünleme puanlarını kaydeder: Final bileşenindeki StudentGrade.MakeupScore güncellenir.
    /// Orijinal Score değişmez. Puan 0-100 arasında olmalıdır.
    /// </summary>
    [HttpPut("section/{sectionId}/makeup/butunleme-scores")]
    [RequirePermission("menu.not-girisi.edit")]
    public async Task<IActionResult> SetButunlemeScores(int sectionId, [FromBody] ButunlemeScoresRequest dto)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!section.IsFinalized)
            return BadRequest(new { error = "Bütünleme sadece sonuçlandırılmış section'larda uygulanabilir." });

        var termIsCancelled = await GetTermIsCancelledAsync(section.TermId);
        if (termIsCancelled == true)
            return BadRequest(new { error = "İptal edilmiş dönemde bütünleme puanı girilemez." });

        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest(new { error = "Puan listesi boş olamaz." });

        // Find the Final-type component
        var finalComponent = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId
                && gc.AcademicTermId == section.TermId
                && gc.Type == GradeComponentType.Final)
            .OrderBy(gc => gc.Order)
            .FirstOrDefaultAsync();

        if (finalComponent == null)
            return BadRequest(new { error = "Bu ders için Final türünde bileşen bulunamadı." });

        // Validate scores
        foreach (var item in dto.Items)
        {
            if (item.MakeupScore < 0 || item.MakeupScore > 100)
                return BadRequest(new { error = $"Öğrenci {item.StudentId} için bütünleme puanı 0-100 arasında olmalıdır." });
        }

        // Get eligible FF registrations for this section
        var ffRegistrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn
                && cr.Grade == GradeCode.FF)
            .ToListAsync();

        // F guard (AutoTransferFailedToMakeup): resolve exam regulations and filter out students
        // whose regulation has F=false. Mirrors the same check in FinalizeButunleme and the roster.
        // Storing an orphan MakeupScore for an ineligible student would be silently ignored at
        // finalize time but creates confusing DB state.
        var ffStudentIds = ffRegistrations.Select(r => r.StudentId).Distinct().ToList();
        var examRegByStudent = await _examRegulationResolver.ResolveForStudentsAsync(
            ffStudentIds, section.TermId, default);

        var ffRegistrationMap = ffRegistrations
            .Where(r => examRegByStudent.TryGetValue(r.StudentId, out var reg) && reg!.AutoTransferFailedToMakeup)
            .ToDictionary(r => r.StudentId);
        var ffRegistrationIds = ffRegistrationMap.Values.Select(r => r.Id).ToList();

        // Load existing StudentGrade rows for Final component
        var existingGradeRows = await _context.StudentGrades
            .Where(sg => sg.GradeComponentId == finalComponent.Id
                && ffRegistrationIds.Contains(sg.CourseRegistrationId))
            .ToDictionaryAsync(sg => sg.CourseRegistrationId);

        int updated = 0;
        int created = 0;

        foreach (var item in dto.Items)
        {
            if (!ffRegistrationMap.TryGetValue(item.StudentId, out var reg))
                continue; // only process F-eligible FF students in this section

            if (existingGradeRows.TryGetValue(reg.Id, out var existing))
            {
                existing.MakeupScore = item.MakeupScore;
                updated++;
            }
            else
            {
                // Final StudentGrade row might not exist (edge case: student was Absent → no row)
                // Create a new row with MakeupScore set; Score remains null
                _context.StudentGrades.Add(new StudentGrade
                {
                    CourseRegistrationId = reg.Id,
                    GradeComponentId = finalComponent.Id,
                    Score = null,
                    MakeupScore = item.MakeupScore,
                    ExamAttendance = ExamAttendance.NotMarked
                });
                created++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Bütünleme puanları kaydedildi: {updated} güncellendi, {created} eklendi.",
            FinalComponentId = finalComponent.Id
        });
    }

    /// <summary>
    /// POST api/instructor/section/{sectionId}/makeup/butunleme-finalize
    /// Bütünleme sonuçlarını kesinleştirir:
    ///   - FF olan ve bütünlemeye uygun her öğrenci için:
    ///     E (MakeupReplacesFinal) açıksa: büt puanı Final'ın yerine geçirerek notu yeniden hesaplar
    ///     D (ZeroIfMissedMakeup) açıksa: büt puanı girilmemişse 0 kabul edilir
    ///   - Yeni harf notu CourseRegistration.Grade'e yazılır
    ///   - Her öğrencinin GPA'sı güncellenir
    /// </summary>
    [HttpPost("section/{sectionId}/makeup/butunleme-finalize")]
    [RequirePermission("menu.not-girisi.edit")]
    public async Task<IActionResult> FinalizeButunleme(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!section.IsFinalized)
            return BadRequest(new { error = "Bütünleme sadece sonuçlandırılmış section'larda uygulanabilir." });

        var termIsCancelled = await GetTermIsCancelledAsync(section.TermId);
        if (termIsCancelled == true)
            return BadRequest(new { error = "İptal edilmiş dönemde bütünleme sonuçlandırılamaz." });

        // Load components
        var components = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .OrderBy(gc => gc.Order)
            .ToListAsync();

        var finalComponents = components.Where(c => c.Type == GradeComponentType.Final).ToList();

        // Resolve grading criteria (mirrors Finalize — shared helper).
        // INTENTIONAL: makeup recompute uses the ABSOLUTE effectiveCriteria only.
        // The relative (curve) grading was applied once at the original Finalize run.
        // For makeup (büt), the instructor's set absolute table is used per the regulation
        // model — the curve is NOT re-run.
        var (effectiveCriteria, criteriaError) = await ResolveEffectiveCriteriaAsync(section, cancellationToken: default);
        if (criteriaError != null)
            return criteriaError;

        // Get FF registrations eligible for bütünleme
        var ffRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.Student)
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn
                && cr.Grade == GradeCode.FF)
            .ToListAsync();

        if (!ffRegistrations.Any())
        {
            return Ok(new
            {
                message = "Bütünleme uygulanacak FF öğrenci bulunamadı.",
                Results = Array.Empty<object>()
            });
        }

        var ffStudentIds = ffRegistrations.Select(r => r.StudentId).Distinct().ToList();

        // Bulk-resolve exam regulations
        var examRegByStudent = await _examRegulationResolver.ResolveForStudentsAsync(
            ffStudentIds, section.TermId, default);

        var ffRegistrationIds = ffRegistrations.Select(r => r.Id).ToList();
        var allGrades = await _context.StudentGrades
            .Where(sg => ffRegistrationIds.Contains(sg.CourseRegistrationId))
            .ToListAsync();

        var gradesByRegistration = allGrades
            .GroupBy(sg => sg.CourseRegistrationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<object>();
        var affectedStudentIds = new List<int>();

        foreach (var reg in ffRegistrations)
        {
            examRegByStudent.TryGetValue(reg.StudentId, out var studentReg);

            // F: only process if AutoTransferFailedToMakeup is on
            if (studentReg == null || !studentReg.AutoTransferFailedToMakeup)
                continue;

            // B: BlockMakeupIfNoFinal — if student's final attendance is Absent, skip
            var studentGrades = gradesByRegistration.GetValueOrDefault(reg.Id, new List<StudentGrade>());
            if (studentReg.BlockMakeupIfNoFinal && finalComponents.Count > 0)
            {
                var absentFromFinal = finalComponents.Any(fc =>
                {
                    var g = studentGrades.FirstOrDefault(gi => gi.GradeComponentId == fc.Id);
                    return g == null || g.ExamAttendance == ExamAttendance.Absent;
                });
                if (absentFromFinal)
                    continue;
            }

            // E: MakeupReplacesFinal — gates whether büt score replaces final
            // Known limitation: if E is off, scores are stored but recompute is skipped.
            if (!studentReg.MakeupReplacesFinal)
                continue;

            // D: ZeroIfMissedMakeup — if büt score missing, treat as 0 on the Final component row(s)
            if (studentReg.ZeroIfMissedMakeup)
            {
                foreach (var fc in finalComponents)
                {
                    var gradeRow = studentGrades.FirstOrDefault(g => g.GradeComponentId == fc.Id);
                    if (gradeRow != null && gradeRow.MakeupScore == null)
                    {
                        gradeRow.MakeupScore = 0m;
                    }
                    else if (gradeRow == null)
                    {
                        // Create a row with MakeupScore=0
                        var newRow = new StudentGrade
                        {
                            CourseRegistrationId = reg.Id,
                            GradeComponentId = fc.Id,
                            Score = null,
                            MakeupScore = 0m,
                            ExamAttendance = ExamAttendance.NotMarked
                        };
                        _context.StudentGrades.Add(newRow);
                        studentGrades.Add(newRow);
                    }
                }
            }

            var oldGrade = reg.Grade;

            // Recompute with useMakeupScore=true — büt replaces final.
            // effectiveCriteria is guaranteed non-null here (criteriaError guard above).
            var (weightedScore, roundedScore) = ComputeStudentWeightedScore(
                components, studentGrades, studentReg, useMakeupScore: true);

            var baseLetter = effectiveCriteria!.ResolveGrade(roundedScore);
            var finalRaw = ComputeStudentFinalRaw(components, studentGrades, useMakeupScore: true);
            var newGrade = ApplyGradeRules(baseLetter, roundedScore, finalRaw, studentGrades, components, studentReg);

            reg.Grade = newGrade;
            reg.UpdatedAt = DateTime.UtcNow;
            affectedStudentIds.Add(reg.StudentId);

            results.Add(new
            {
                StudentId = reg.StudentId,
                OldGrade = oldGrade?.ToString(),
                NewGrade = newGrade.ToString(),
                WeightedAverage = Math.Round(weightedScore, 2)
            });
        }

        await _context.SaveChangesAsync();

        // Recalculate GPA for each affected student
        foreach (var studentId in affectedStudentIds.Distinct())
        {
            await _gpaService.RecalculateAndUpdateAsync(studentId);
        }

        return Ok(new
        {
            message = $"Bütünleme sonuçlandırıldı. {results.Count} öğrencinin notu güncellendi.",
            Results = results
        });
    }

    // ══════════════════════════════════════════════════════════
    //  MAZERET (EXCUSE) MAKEUP AKIŞI
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// GET api/instructor/section/{sectionId}/makeup/mazeret
    /// Mazeret listesini döner: Grade==I olan ve G (AutoTransferExcusedToMakeup) açık öğrenciler.
    /// Her öğrenci için mazeretli olduğu bileşenler (bileşen id + ad + orijinal Score + MakeupScore) döner.
    /// </summary>
    [HttpGet("section/{sectionId}/makeup/mazeret")]
    [RequirePermission("menu.not-girisi.view")]
    public async Task<IActionResult> GetMazeretRoster(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!section.IsFinalized)
            return BadRequest(new { error = "Mazeret sadece sonuçlandırılmış section'larda uygulanabilir." });

        // Load all components for this course/term
        var components = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .OrderBy(gc => gc.Order)
            .ToListAsync();

        // Get active registrations with Grade=I (Incomplete)
        var incompleteRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.Student)
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn
                && cr.Grade == GradeCode.I)
            .ToListAsync();

        if (!incompleteRegistrations.Any())
        {
            return Ok(new
            {
                SectionId = sectionId,
                Students = Array.Empty<object>()
            });
        }

        var incompleteStudentIds = incompleteRegistrations.Select(r => r.StudentId).ToList();

        // Bulk-resolve exam regulations per student to check G toggle
        var examRegByStudent = await _examRegulationResolver.ResolveForStudentsAsync(
            incompleteStudentIds, section.TermId, default);

        // Load StudentGrade rows for these registrations
        var incompleteRegistrationIds = incompleteRegistrations.Select(r => r.Id).ToList();
        var allGrades = await _context.StudentGrades
            .Where(sg => incompleteRegistrationIds.Contains(sg.CourseRegistrationId))
            .ToListAsync();

        var gradesByRegistration = allGrades
            .GroupBy(sg => sg.CourseRegistrationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load user display names
        var studentUserIds = incompleteRegistrations.Select(r => r.Student.UserId).ToList();
        var userMap = await _context.Users
            .Where(u => studentUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var eligibleStudents = new List<object>();

        foreach (var reg in incompleteRegistrations)
        {
            examRegByStudent.TryGetValue(reg.StudentId, out var studentReg);

            // G = AutoTransferExcusedToMakeup: if null reg or false → skip
            if (studentReg == null || !studentReg.AutoTransferExcusedToMakeup)
                continue;

            var studentGrades = gradesByRegistration.GetValueOrDefault(reg.Id, new List<StudentGrade>());

            // Find the components this student was Excused on
            var excusedComponents = components
                .Where(c =>
                {
                    var g = studentGrades.FirstOrDefault(gi => gi.GradeComponentId == c.Id);
                    return g != null && g.ExamAttendance == ExamAttendance.Excused;
                })
                .Select(c =>
                {
                    var g = studentGrades.First(gi => gi.GradeComponentId == c.Id);
                    return new
                    {
                        ComponentId = c.Id,
                        ComponentName = c.Name,
                        OriginalScore = g.Score,
                        MakeupScore = g.MakeupScore
                    };
                })
                .ToList<object>();

            var user = userMap.GetValueOrDefault(reg.Student.UserId);

            eligibleStudents.Add(new
            {
                StudentId = reg.StudentId,
                StudentNumber = reg.Student.StudentNumber,
                FirstName = user?.FirstName ?? string.Empty,
                LastName = user?.LastName ?? string.Empty,
                CurrentGrade = reg.Grade?.ToString(),
                ExcusedComponents = excusedComponents
            });
        }

        return Ok(new
        {
            SectionId = sectionId,
            Students = eligibleStudents
        });
    }

    /// <summary>
    /// PUT api/instructor/section/{sectionId}/makeup/mazeret-scores
    /// Mazeret puanlarını kaydeder. Her item'da studentId + componentId + makeupScore.
    /// MakeupScore StudentGrade.MakeupScore'a yazılır; orijinal Score değişmez.
    /// Puan 0-100 arasında olmalıdır. Bileşen bu dersin bu dönemine ait olmalıdır.
    /// </summary>
    [HttpPut("section/{sectionId}/makeup/mazeret-scores")]
    [RequirePermission("menu.not-girisi.edit")]
    public async Task<IActionResult> SetMazeretScores(int sectionId, [FromBody] MazeretScoresRequest dto)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!section.IsFinalized)
            return BadRequest(new { error = "Mazeret sadece sonuçlandırılmış section'larda uygulanabilir." });

        var termIsCancelled = await GetTermIsCancelledAsync(section.TermId);
        if (termIsCancelled == true)
            return BadRequest(new { error = "İptal edilmiş dönemde mazeret puanı girilemez." });

        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest(new { error = "Puan listesi boş olamaz." });

        // Validate scores: 0-100
        foreach (var item in dto.Items)
        {
            if (item.MakeupScore < 0 || item.MakeupScore > 100)
                return BadRequest(new { error = $"Öğrenci {item.StudentId} için mazeret puanı 0-100 arasında olmalıdır." });
        }

        // Validate all referenced componentIds belong to this course/term
        var componentIds = dto.Items.Select(i => i.ComponentId).Distinct().ToList();
        var validComponents = await _context.GradeComponents
            .Where(gc => componentIds.Contains(gc.Id)
                && gc.CourseId == section.CourseId
                && gc.AcademicTermId == section.TermId)
            .ToDictionaryAsync(gc => gc.Id);

        foreach (var componentId in componentIds)
        {
            if (!validComponents.ContainsKey(componentId))
                return BadRequest(new { error = $"Bileşen {componentId} bu section için geçerli değil." });
        }

        // Get I-registrations for this section
        var incompleteRegistrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn
                && cr.Grade == GradeCode.I)
            .ToListAsync();

        var registrationMap = incompleteRegistrations.ToDictionary(r => r.StudentId);
        var registrationIds = incompleteRegistrations.Select(r => r.Id).ToList();

        // Load existing StudentGrade rows for referenced components
        var existingGradeRows = await _context.StudentGrades
            .Where(sg => componentIds.Contains(sg.GradeComponentId)
                && registrationIds.Contains(sg.CourseRegistrationId))
            .ToListAsync();

        var gradeRowLookup = existingGradeRows
            .ToDictionary(sg => (sg.CourseRegistrationId, sg.GradeComponentId));

        int updated = 0;
        int created = 0;

        foreach (var item in dto.Items)
        {
            if (!registrationMap.TryGetValue(item.StudentId, out var reg))
                continue; // only process I-students in this section

            var key = (reg.Id, item.ComponentId);
            if (gradeRowLookup.TryGetValue(key, out var existing))
            {
                existing.MakeupScore = item.MakeupScore;
                updated++;
            }
            else
            {
                // Row might not exist if student never got a grade row for this component
                var newRow = new StudentGrade
                {
                    CourseRegistrationId = reg.Id,
                    GradeComponentId = item.ComponentId,
                    Score = null,
                    MakeupScore = item.MakeupScore,
                    ExamAttendance = ExamAttendance.Excused
                };
                _context.StudentGrades.Add(newRow);
                created++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Mazeret puanları kaydedildi: {updated} güncellendi, {created} eklendi."
        });
    }

    /// <summary>
    /// POST api/instructor/section/{sectionId}/makeup/mazeret-finalize
    /// Mazeret sonuçlarını kesinleştirir:
    ///   - Grade==I olan ve G açık her öğrenci için:
    ///     Tüm bileşenlerde kullanılabilir skor (Score veya MakeupScore) var mı kontrol edilir.
    ///     Excused bileşende Score null ve MakeupScore null ise → öğrenci hazır değil → I olarak kalır, atlanır.
    ///     Hazır olan öğrenciler: useMakeupScore=true ile yeniden hesap → Grade=I'dan çıkar.
    ///   - Yeni harf notu CourseRegistration.Grade'e yazılır.
    ///   - Her etkilenen öğrencinin GPA'sı güncellenir.
    /// </summary>
    [HttpPost("section/{sectionId}/makeup/mazeret-finalize")]
    [RequirePermission("menu.not-girisi.edit")]
    public async Task<IActionResult> FinalizeMazeret(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!section.IsFinalized)
            return BadRequest(new { error = "Mazeret sadece sonuçlandırılmış section'larda uygulanabilir." });

        var termIsCancelled = await GetTermIsCancelledAsync(section.TermId);
        if (termIsCancelled == true)
            return BadRequest(new { error = "İptal edilmiş dönemde mazeret sonuçlandırılamaz." });

        // Load components
        var components = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .OrderBy(gc => gc.Order)
            .ToListAsync();

        // Resolve grading criteria (mirrors Finalize — shared helper).
        // INTENTIONAL: makeup recompute uses the ABSOLUTE effectiveCriteria only.
        // The relative (curve) grading was applied once at the original Finalize run.
        // For mazeret, the instructor's set absolute table is used per the regulation
        // model — the curve is NOT re-run.
        var (effectiveCriteria, criteriaError) = await ResolveEffectiveCriteriaAsync(section, cancellationToken: default);
        if (criteriaError != null)
            return criteriaError;

        // Get I-registrations eligible for mazeret
        var incompleteRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.Student)
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Withdrawn
                && cr.Grade == GradeCode.I)
            .ToListAsync();

        if (!incompleteRegistrations.Any())
        {
            return Ok(new
            {
                message = "Mazeret uygulanacak I (Incomplete) öğrenci bulunamadı.",
                Results = Array.Empty<object>()
            });
        }

        var incompleteStudentIds = incompleteRegistrations.Select(r => r.StudentId).Distinct().ToList();

        // Bulk-resolve exam regulations
        var examRegByStudent = await _examRegulationResolver.ResolveForStudentsAsync(
            incompleteStudentIds, section.TermId, default);

        var incompleteRegistrationIds = incompleteRegistrations.Select(r => r.Id).ToList();
        var allGrades = await _context.StudentGrades
            .Where(sg => incompleteRegistrationIds.Contains(sg.CourseRegistrationId))
            .ToListAsync();

        var gradesByRegistration = allGrades
            .GroupBy(sg => sg.CourseRegistrationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<object>();
        var affectedStudentIds = new List<int>();

        foreach (var reg in incompleteRegistrations)
        {
            examRegByStudent.TryGetValue(reg.StudentId, out var studentReg);

            // G: only process if AutoTransferExcusedToMakeup is on
            if (studentReg == null || !studentReg.AutoTransferExcusedToMakeup)
                continue;

            var studentGrades = gradesByRegistration.GetValueOrDefault(reg.Id, new List<StudentGrade>());

            // Readiness check: every component must have a usable score.
            // For Excused components: Score is null, so MakeupScore must be present.
            // For all other components: Score must be present (unless Absent, which contributes 0).
            var isReady = components.All(comp =>
            {
                var gradeRow = studentGrades.FirstOrDefault(g => g.GradeComponentId == comp.Id);

                if (gradeRow == null)
                    return false; // no row at all → not ready

                if (gradeRow.ExamAttendance == ExamAttendance.Absent)
                    return true; // Absent contributes 0, always usable

                if (gradeRow.ExamAttendance == ExamAttendance.Excused)
                    return gradeRow.MakeupScore != null; // Excused needs a makeup score

                // Normal attendance: Score must be present
                return gradeRow.Score != null;
            });

            if (!isReady)
                continue; // skip — leave as I

            var oldGrade = reg.Grade;

            // Recompute with useMakeupScore=true — makeup replaces excused component score.
            // effectiveCriteria is guaranteed non-null here (criteriaError guard above).
            var (weightedScore, roundedScore) = ComputeStudentWeightedScore(
                components, studentGrades, studentReg, useMakeupScore: true);

            var baseLetter = effectiveCriteria!.ResolveGrade(roundedScore);
            var finalRaw = ComputeStudentFinalRaw(components, studentGrades, useMakeupScore: true);
            var newGrade = ApplyGradeRules(baseLetter, roundedScore, finalRaw, studentGrades, components, studentReg);

            reg.Grade = newGrade;
            reg.UpdatedAt = DateTime.UtcNow;
            affectedStudentIds.Add(reg.StudentId);

            results.Add(new
            {
                StudentId = reg.StudentId,
                OldGrade = oldGrade?.ToString(),
                NewGrade = newGrade.ToString(),
                WeightedAverage = Math.Round(weightedScore, 2)
            });
        }

        await _context.SaveChangesAsync();

        // Recalculate GPA for each affected student
        foreach (var studentId in affectedStudentIds.Distinct())
        {
            await _gpaService.RecalculateAndUpdateAsync(studentId);
        }

        return Ok(new
        {
            message = $"Mazeret sonuçlandırıldı. {results.Count} öğrencinin notu güncellendi.",
            Results = results
        });
    }

    // ══════════════════════════════════════════════════════════
    //  ÇAN EĞRİSİ (GradingCriteria — ders+dönem bazında)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// GET api/instructor/section/{sectionId}/grading-criteria
    /// Dersin mevcut not aralıklarını getirir.
    /// </summary>
    [HttpGet("section/{sectionId}/grading-criteria")]
    [RequirePermission("menu.not-girisi.view")]
    public async Task<IActionResult> GetGradingCriteria(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var criteria = await _context.GradingCriteria
            .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId);

        var policy = await _examRegulationResolver.ResolveCourseLetterPolicyAsync(
            section.CourseId, section.TermId, default);

        var editable = policy == null || policy.Editable;

        // When the regulation supplies unique thresholds AND either it locks editing or no
        // section GradingCriteria row exists yet, surface the regulation thresholds so the
        // displayed values match what finalize will use (mirrors Finalize's own selection logic).
        GradingCriteria effective;
        if (policy != null && policy.HasUniqueThresholds && (!policy.Editable || criteria == null))
        {
            effective = new GradingCriteria
            {
                AAMin = policy.AAMin, BAMin = policy.BAMin, BBMin = policy.BBMin,
                CBMin = policy.CBMin, CCMin = policy.CCMin, DCMin = policy.DCMin,
                DDMin = policy.DDMin
            };
        }
        else
        {
            effective = criteria ?? new GradingCriteria();
        }

        return Ok(new
        {
            effective.AAMin,
            effective.BAMin,
            effective.BBMin,
            effective.CBMin,
            effective.CCMin,
            effective.DCMin,
            effective.DDMin,
            Editable = editable
        });
    }

    /// <summary>
    /// PUT api/instructor/section/{sectionId}/grading-criteria
    /// Çan eğrisi uygular — not aralıklarını günceller.
    /// Dersin tüm section'larını etkiler.
    /// </summary>
    [HttpPut("section/{sectionId}/grading-criteria")]
    [RequirePermission("menu.not-girisi.edit")]
    public async Task<IActionResult> UpdateGradingCriteria(int sectionId, [FromBody] GradingCriteriaDto dto)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        // Aralıkların tutarlılığını kontrol et (AA > BA > BB > ... > DD)
        if (!await CanMutateSharedGradeConfigurationAsync(section))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu ders birden fazla hoca tarafından veriliyor; ortak sınav/not ayarını yalnızca koordinatör hoca değiştirebilir. Koordinatör, bölüm/admin tarafından Hoca Atama ekranından belirlenir." });

        // Sınav yönergesi harf aralıklarını kilitlemişse güncelleme engellenir.
        var policy = await _examRegulationResolver.ResolveCourseLetterPolicyAsync(
            section.CourseId, section.TermId, default);
        if (policy != null && !policy.Editable)
            return BadRequest(new { error = "Bu dersin sınav yönergesinde harf aralıkları kilitli, değiştirilemez." });

        if (dto.AAMin <= dto.BAMin || dto.BAMin <= dto.BBMin || dto.BBMin <= dto.CBMin
            || dto.CBMin <= dto.CCMin || dto.CCMin <= dto.DCMin || dto.DCMin <= dto.DDMin
            || dto.DDMin <= 0)
        {
            return BadRequest(new { error = "Not araliklari sirali olmalidir: AA > BA > BB > CB > CC > DC > DD > 0" });
        }

        var criteria = await _context.GradingCriteria
            .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId);

        if (criteria == null)
        {
            criteria = new GradingCriteria
            {
                CourseId = section.CourseId,
                AcademicTermId = section.TermId
            };
            _context.GradingCriteria.Add(criteria);
        }

        criteria.AAMin = dto.AAMin;
        criteria.BAMin = dto.BAMin;
        criteria.BBMin = dto.BBMin;
        criteria.CBMin = dto.CBMin;
        criteria.CCMin = dto.CCMin;
        criteria.DCMin = dto.DCMin;
        criteria.DDMin = dto.DDMin;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Not araliklari guncellendi. Sonuclandirilmis sectionlar etkilenmez, yeniden sonuclandirma gerekir." });
    }

    // ══════════════════════════════════════════════════════════
    //  PER-STUDENT GRADE COMPUTATION HELPERS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Computes the weighted score (decimal) and rounded integer score for one student.
    /// Absent components contribute 0. Rule A (BlockFinalIfNoMidterm) zeroes Final-type
    /// components when triggered. When <paramref name="useMakeupScore"/> is true and a
    /// component's StudentGrade.MakeupScore is non-null, MakeupScore is used in place of Score.
    /// </summary>
    /// <remarks>
    /// Pre-condition — callers MUST ensure every component has a StudentGrade row for this student
    /// before calling this method:
    ///   - Finalize: enforces this via the missing-grades check (returns 400 if any row is absent).
    ///   - Makeup (büt/mazeret): enforces this via the readiness check (skips the student if not
    ///     all required scores are present).
    /// The <c>.First(...)</c> call below will throw <see cref="InvalidOperationException"/> on
    /// violation BY DESIGN — a loud failure is strongly preferred over silently producing a wrong
    /// grade from incomplete data.  Do NOT change .First to .FirstOrDefault here.
    /// </remarks>
    private static (decimal WeightedScore, int RoundedScore) ComputeStudentWeightedScore(
        IReadOnlyList<GradeComponent> components,
        IReadOnlyList<StudentGrade> studentGrades,
        ExamRegulation? studentReg,
        bool useMakeupScore)
    {
        // Rule A — BlockFinalIfNoMidterm (opt-in): when enabled and the student attended
        // NO midterm component, treat all Final-type component scores as 0.
        // "Attended a midterm" = ExamAttendance==Present OR (NotMarked AND Score!=null).
        var blockFinalScores = false;
        if (studentReg != null && studentReg.BlockFinalIfNoMidterm)
        {
            var midtermComponents = components.Where(c => c.Type == GradeComponentType.Midterm).ToList();
            // Rule A is only meaningful when midterm components actually exist.
            // If there are no midterm components, "attended no midterm" is trivially true
            // but penalising is wrong — the rule must be moot in this case.
            if (midtermComponents.Count > 0)
            {
                var attendedAnyMidterm = midtermComponents.Any(mc =>
                {
                    var g = studentGrades.FirstOrDefault(sg => sg.GradeComponentId == mc.Id);
                    if (g == null) return false;
                    return g.ExamAttendance == ExamAttendance.Present
                        || (g.ExamAttendance == ExamAttendance.NotMarked && g.Score != null);
                });
                blockFinalScores = !attendedAnyMidterm;
            }
        }

        decimal weightedScore = 0;
        foreach (var comp in components)
        {
            // gradeRow is guaranteed present by the missing-grades check (for graded registrations).
            var gradeRow = studentGrades.First(g => g.GradeComponentId == comp.Id);

            decimal score;
            if (gradeRow.ExamAttendance == ExamAttendance.Absent)
            {
                score = 0m;
            }
            else
            {
                // When useMakeupScore is true and MakeupScore is available, use it; otherwise Score.
                score = (useMakeupScore && gradeRow.MakeupScore != null)
                    ? gradeRow.MakeupScore.Value
                    : gradeRow.Score!.Value;
            }

            // Rule A: override Final-type score to 0 when BlockFinalIfNoMidterm fired.
            if (blockFinalScores && comp.Type == GradeComponentType.Final)
                score = 0m;

            weightedScore += score * comp.Weight / 100m;
        }

        var roundedScore = (int)Math.Round(weightedScore, 0, MidpointRounding.AwayFromZero);
        return (weightedScore, roundedScore);
    }

    /// <summary>
    /// Computes the raw average score of all Final-type components for one student.
    /// Absent components contribute 0. When <paramref name="useMakeupScore"/> is true and a
    /// component's StudentGrade.MakeupScore is non-null, MakeupScore is used in place of Score.
    /// Returns null when there are no Final-type components.
    /// <para>
    /// Pre-condition — callers MUST ensure every Final-type component has a StudentGrade row for
    /// this student before calling (same guarantee as <see cref="ComputeStudentWeightedScore"/>).
    /// The <c>.First(...)</c> call inside will throw <see cref="InvalidOperationException"/> on
    /// violation BY DESIGN.  Do NOT change .First to .FirstOrDefault here.
    /// </para>
    /// </summary>
    private static decimal? ComputeStudentFinalRaw(
        IReadOnlyList<GradeComponent> components,
        IReadOnlyList<StudentGrade> studentGrades,
        bool useMakeupScore)
    {
        var finalComponents = components
            .Where(c => c.Type == GradeComponentType.Final)
            .ToList();

        if (finalComponents.Count == 0)
            return null;

        var finalRaw = finalComponents
            .Select(fc =>
            {
                var g = studentGrades.First(gi => gi.GradeComponentId == fc.Id);
                if (g.ExamAttendance == ExamAttendance.Absent)
                    return 0m;
                return (useMakeupScore && g.MakeupScore != null)
                    ? g.MakeupScore.Value
                    : g.Score!.Value;
            })
            .Average();

        return finalRaw;
    }

    /// <summary>
    /// Applies exam-regulation floors and rule C on top of a base letter grade.
    /// Returns the final <see cref="GradeCode"/> to assign.
    /// </summary>
    /// <param name="baseLetter">Letter resolved from absolute criteria or relative curve.</param>
    /// <param name="roundedScore">Integer weighted score for PassingGrade check.</param>
    /// <param name="finalRaw">Average raw score of Final-type components (null when none exist).</param>
    /// <param name="studentGrades">All StudentGrade rows for this registration (for Rule C absent check).</param>
    /// <param name="components">All GradeComponent rows for the course/term (for Rule C absent check).</param>
    /// <param name="studentReg">Student's resolved exam regulation (null → no floors/rules applied).</param>
    private static GradeCode ApplyGradeRules(
        GradeCode baseLetter,
        int roundedScore,
        decimal? finalRaw,
        IReadOnlyList<StudentGrade> studentGrades,
        IReadOnlyList<GradeComponent> components,
        ExamRegulation? studentReg)
    {
        if (studentReg == null)
            return baseLetter;

        var letterGrade = baseLetter;

        // Final barajı: final bileşenlerinin ham puanları ortalaması barajın altındaysa → BelowFloorGrade
        if (finalRaw.HasValue && studentReg.FinalExamFloor > 0)
        {
            if (finalRaw.Value < studentReg.FinalExamFloor)
                letterGrade = studentReg.BelowFloorGrade;
        }

        // Geçme notu: ağırlıklı ortalama geçme notunun altındaysa → BelowFloorGrade
        if (studentReg.PassingGrade > 0 && roundedScore < studentReg.PassingGrade)
            letterGrade = studentReg.BelowFloorGrade;

        // Rule C — FailIfNoFinal (opt-in): if the student's regulation has this flag
        // enabled AND the student's Final component attendance is Absent (unexcused
        // non-attendance), assign BelowFloorGrade regardless of the numeric average.
        // Excused students are already assigned GradeCode.I above and excluded from
        // gradedRegistrations, so only truly absent students reach this point.
        if (studentReg.FailIfNoFinal)
        {
            var finalComps = components
                .Where(c => c.Type == GradeComponentType.Final)
                .ToList();
            var absentFromFinal = finalComps.Count > 0
                && finalComps.Any(fc =>
                {
                    var g = studentGrades.First(gi => gi.GradeComponentId == fc.Id);
                    return g.ExamAttendance == ExamAttendance.Absent;
                });
            if (absentFromFinal)
                letterGrade = studentReg.BelowFloorGrade;
        }

        return letterGrade;
    }

    // ══════════════════════════════════════════════════════════
    //  YARDIMCI METOTLAR
    // ══════════════════════════════════════════════════════════

    private async Task<IActionResult?> EnsureGradeEntryModuleOpenAsync(int termId, string fallbackMessage)
    {
        var access = await _webModuleControls.EvaluateAsync("instructor-grade-entry", termId);
        if (access.IsOpen)
            return null;

        return BadRequest(new { error = access.Message ?? fallbackMessage });
    }

    private async Task<IActionResult?> EnsureGradeEntryModuleOpenAsync(int termId, string componentName, string fallbackMessage)
    {
        var genericAccess = await _webModuleControls.EvaluateAsync("instructor-grade-entry", termId);
        if (string.Equals(genericAccess.SelectedMode, WebModuleControlModes.Closed, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = genericAccess.Message ?? fallbackMessage });

        var controlCode = ResolveGradeEntryControlCode(componentName);
        if (string.Equals(controlCode, "instructor-grade-entry", StringComparison.OrdinalIgnoreCase))
            return genericAccess.IsOpen
                ? null
                : BadRequest(new { error = genericAccess.Message ?? fallbackMessage });

        var access = await _webModuleControls.EvaluateAsync(controlCode, termId);
        if (access.IsOpen)
            return null;

        return BadRequest(new { error = access.Message ?? fallbackMessage });
    }

    private static string ResolveGradeEntryControlCode(string componentName)
    {
        var normalized = componentName.Trim().ToLowerInvariant();

        if (normalized.Contains("mazeret") || normalized.Contains("excuse"))
        {
            return normalized.Contains("ara") || normalized.Contains("midterm") || normalized.Contains("vize")
                ? "midterm-excuse-grade-entry"
                : "makeup-grade-entry";
        }

        if (normalized.Contains("ara") || normalized.Contains("midterm") || normalized.Contains("vize"))
            return "midterm-grade-entry";

        if (normalized.Contains("bütünleme") || normalized.Contains("butunleme") || normalized.Contains("makeup") || normalized.Contains("yys"))
            return "makeup-grade-entry";

        if (normalized.Contains("yarıyıl") || normalized.Contains("yariyil") || normalized.Contains("final"))
            return "final-grade-entry";

        return "instructor-grade-entry";
    }

    /// <summary>
    /// Giriş yapan hocaya ait section'ı doğrular ve döner.
    /// Hocanın o dersin herhangi bir section'ına atanmış olması yeterlidir.
    /// </summary>
    private async Task<bool> CanMutateSharedGradeConfigurationAsync(CourseSection section)
    {
        if (!section.InstructorId.HasValue)
            return false;

        var hasOtherInstructor = await _context.CourseSections
            .AsNoTracking()
            .AnyAsync(other => other.CourseId == section.CourseId
                && other.TermId == section.TermId
                && other.InstructorId != section.InstructorId);

        // Tek hoca (veya hepsi aynı hoca) → serbest. Çok hoca → yalnız koordinatör şube.
        return !hasOtherInstructor || section.IsExamCoordinator;
    }

    /// <summary>
    /// Resolves the effective <see cref="GradingCriteria"/> for the given section's course/term.
    /// Priority order (mirrors what Finalize uses):
    ///   1. If the letter policy supplies unique thresholds AND (editing is locked OR no section
    ///      GradingCriteria row exists yet) → build a transient GradingCriteria from the policy.
    ///      The transient object is NOT persisted.
    ///   2. Section-level GradingCriteria row (instructor-editable, persisted).
    ///   3. Neither applies → error:
    ///      - Conflicting policy (non-editable, no unique thresholds) → conflict error message.
    ///      - No criteria at all → generic missing-criteria error message.
    /// </summary>
    /// <returns>
    /// A tuple where <c>Criteria</c> is non-null iff <c>Error</c> is null.
    /// Callers MUST check <c>Error != null</c> and return it before using <c>Criteria</c>.
    /// </returns>
    private async Task<(GradingCriteria? Criteria, IActionResult? Error)> ResolveEffectiveCriteriaAsync(
        CourseSection section, CancellationToken cancellationToken)
    {
        var criteria = await _context.GradingCriteria
            .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId,
                cancellationToken);

        var letterPolicy = await _examRegulationResolver.ResolveCourseLetterPolicyAsync(
            section.CourseId, section.TermId, cancellationToken);

        if (letterPolicy != null && letterPolicy.HasUniqueThresholds && (!letterPolicy.Editable || criteria == null))
        {
            // Regulation-driven: build a transient GradingCriteria from the policy thresholds.
            // Do NOT persist — used only for letter resolution in this finalize run.
            var transient = new GradingCriteria
            {
                AAMin = letterPolicy.AAMin, BAMin = letterPolicy.BAMin, BBMin = letterPolicy.BBMin,
                CBMin = letterPolicy.CBMin, CCMin = letterPolicy.CCMin, DCMin = letterPolicy.DCMin,
                DDMin = letterPolicy.DDMin
            };
            return (transient, null);
        }

        if (criteria != null)
            return (criteria, null);

        // Neither path yielded criteria — return an appropriate error.
        if (letterPolicy != null && !letterPolicy.Editable && !letterPolicy.HasUniqueThresholds)
            return (null, BadRequest(new { error = "Bu dersin bağlı programları arasında çelişen harf aralıkları var. Sınav müfredatını (harf aralıklarını) düzeltin." }));

        return (null, BadRequest(new { error = "Harf notu aralıkları girilmeden ders sonuçlandırılamaz." }));
    }

    private async Task<CourseSection?> GetAuthorizedSectionAsync(int sectionId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
        if (instructor == null) return null;

        // Section'ı al
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId);

        if (section == null) return null;

        // Hocanın tam olarak bu section'a atanmış olması gerekir
        if (section.InstructorId != instructor.Id)
            return null;

        return section;
    }

    private async Task<bool?> GetTermIsCancelledAsync(int termId)
    {
        var term = await _context.AcademicTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == termId);
        return term?.IsCancelled;
    }


    // ══════════════════════════════════════════════════════════
    //  DTO'lar
    // ══════════════════════════════════════════════════════════

    public class ComponentDto
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public int Order { get; set; }
        public GradeComponentType Type { get; set; } = GradeComponentType.Other;
        public string? ShortName { get; set; }
    }

    public class GradeSubmitDto
    {
        public int ComponentId { get; set; }
        public List<StudentScoreDto> Grades { get; set; } = new();
    }

    public class StudentScoreDto
    {
        public int StudentId { get; set; }
        public decimal Score { get; set; }
    }

    public class GradingCriteriaDto
    {
        public int AAMin { get; set; }
        public int BAMin { get; set; }
        public int BBMin { get; set; }
        public int CBMin { get; set; }
        public int CCMin { get; set; }
        public int DCMin { get; set; }
        public int DDMin { get; set; }
    }
    public class ImportFileDto
{
    public IFormFile File { get; set; } = null!;
}

    public class SetExamAttendanceRequest
    {
        public int ComponentId { get; set; }
        public List<ExamAttendanceItem> Items { get; set; } = new();
    }

    public class ExamAttendanceItem
    {
        public int StudentId { get; set; }
        public ExamAttendance Attendance { get; set; }
    }

    // ── Bütünleme DTOs ──────────────────────────────────────

    public class ButunlemeScoresRequest
    {
        public List<ButunlemeScoreItem> Items { get; set; } = new();
    }

    public class ButunlemeScoreItem
    {
        public int StudentId { get; set; }
        public decimal MakeupScore { get; set; }
    }

    // ── Mazeret DTOs ─────────────────────────────────────────

    public class MazeretScoresRequest
    {
        public List<MazeretScoreItem> Items { get; set; } = new();
    }

    public class MazeretScoreItem
    {
        public int StudentId { get; set; }
        public int ComponentId { get; set; }
        public decimal MakeupScore { get; set; }
    }

    // ── Paste import DTO ─────────────────────────────────────

    /// <summary>Yapıştırma tabanlı toplu not aktarım isteği.</summary>
    public record PasteScoresRequest(int ComponentId, string? Text, string? Separator);

    // ── Exams overview DTOs ──────────────────────────────────

    /// <summary>Hocanın bir şubesi için sınav/kalem genel bakış satırı.</summary>
    public record ExamOverviewRowDto(
        int SectionId,
        string CourseCode,
        string CourseName,
        int SectionNumber,
        bool IsFinalized,
        List<ExamOverviewComponentDto> Components);

    /// <summary>Bir şubeye ait sınav kalemi + varsa sınav tarihi.</summary>
    public record ExamOverviewComponentDto(
        int Id,
        string Name,
        GradeComponentType Type,
        decimal Weight,
        DateTime? PublishedAt,
        DateTime? ExamDate);
}
