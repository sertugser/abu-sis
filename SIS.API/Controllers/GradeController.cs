using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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

    public GradeController(
        AppDbContext context,
        IGpaCalculationService gpaService,
        IDateTimeProvider clock,
        IExamRegulationResolver examRegulationResolver,
        ISystemConfigProvider? systemConfig = null,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _gpaService = gpaService;
        _clock = clock;
        _examRegulationResolver = examRegulationResolver;
        _systemConfig = systemConfig ?? new NullSystemConfigProvider();
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
                gc.Weight,
                gc.Order
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
            return Forbid();

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
                Type = dto.Type
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
            return Forbid();

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
    var templateComponentName = worksheet.Cell(3, 2).GetValue<string>();

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

    var registrations = await _context.CourseRegistrations
        .Include(cr => cr.Student)
        .Where(cr => cr.CourseSectionId == sectionId
            && cr.Status != RegistrationStatus.Dropped
            && cr.Status != RegistrationStatus.Draft
            && cr.Status != RegistrationStatus.Withdrawn)
        .ToListAsync();

    var registrationMap = registrations.ToDictionary(r => r.StudentId, r => r);

    var registrationIds = registrations.Select(r => r.Id).ToList();

    var existingGrades = await _context.StudentGrades
        .Where(sg => sg.GradeComponentId == componentId
            && registrationIds.Contains(sg.CourseRegistrationId))
        .ToDictionaryAsync(sg => sg.CourseRegistrationId, sg => sg.Score);

    var previewRows = new List<object>();
    int validCount = 0;
    int warningCount = 0;
    int errorCount = 0;
    int emptyCount = 0;

    var row = 6;

    while (true)
    {
        var studentIdCell = worksheet.Cell(row, 1).GetValue<string>()?.Trim();
        var studentNumber = worksheet.Cell(row, 2).GetValue<string>()?.Trim();
        var firstName = worksheet.Cell(row, 3).GetValue<string>()?.Trim();
        var lastName = worksheet.Cell(row, 4).GetValue<string>()?.Trim();
        var currentScoreText = worksheet.Cell(row, 5).GetValue<string>()?.Trim();
        var newScoreText = worksheet.Cell(row, 6).GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(studentIdCell)
            && string.IsNullOrWhiteSpace(studentNumber)
            && string.IsNullOrWhiteSpace(firstName)
            && string.IsNullOrWhiteSpace(lastName)
            && string.IsNullOrWhiteSpace(currentScoreText)
            && string.IsNullOrWhiteSpace(newScoreText))
        {
            break;
        }

        string status;
        string message;
        decimal? parsedScore = null;
        decimal? currentScore = null;
        int? studentId = null;

        if (decimal.TryParse(currentScoreText, out var currentParsed))
            currentScore = currentParsed;

        if (!int.TryParse(studentIdCell, out var parsedStudentId))
        {
            status = "Error";
            message = "StudentId okunamadi.";
            errorCount++;
        }
        else if (!registrationMap.TryGetValue(parsedStudentId, out var registration))
        {
            status = "Warning";
            message = "Kayitsiz ogrenci, bu satir atlanacak.";
            warningCount++;
            studentId = parsedStudentId;
        }
        else if (string.IsNullOrWhiteSpace(newScoreText))
        {
            status = "Empty";
            message = "Bos hucre, mevcut not korunacak.";
            emptyCount++;
            studentId = parsedStudentId;

            existingGrades.TryGetValue(registration.Id, out var existingScore);
            currentScore = existingScore;
        }
        else if (!decimal.TryParse(newScoreText, out var score))
        {
            status = "Error";
            message = "Not sayisal degil.";
            errorCount++;
            studentId = parsedStudentId;
        }
        else if (score < 0 || score > 100)
        {
            status = "Error";
            message = "Not 0-100 araliginda olmali.";
            errorCount++;
            studentId = parsedStudentId;
        }
        else
        {
            studentId = parsedStudentId;
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

        previewRows.Add(new
        {
            RowNumber = row,
            StudentId = studentId,
            StudentNumber = studentNumber,
            FirstName = firstName,
            LastName = lastName,
            CurrentScore = currentScore,
            NewScore = parsedScore,
            RawNewScore = newScoreText,
            Status = status,
            Message = message
        });

        row++;
    }

    return Ok(new
    {
        SectionId = section.Id,
        ComponentId = component.Id,
        ComponentName = templateComponentName,
        TotalRows = previewRows.Count,
        ValidCount = validCount,
        WarningCount = warningCount,
        ErrorCount = errorCount,
        EmptyCount = emptyCount,
        Rows = previewRows
    });
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

    var registrations = await _context.CourseRegistrations
        .Where(cr => cr.CourseSectionId == sectionId
            && cr.Status != RegistrationStatus.Dropped
            && cr.Status != RegistrationStatus.Draft
            && cr.Status != RegistrationStatus.Withdrawn)
        .ToListAsync();

    var registrationMap = registrations.ToDictionary(r => r.StudentId, r => r);

    var registrationIds = registrations.Select(r => r.Id).ToList();

    var existingGrades = await _context.StudentGrades
        .Where(sg => sg.GradeComponentId == componentId
            && registrationIds.Contains(sg.CourseRegistrationId))
        .ToDictionaryAsync(sg => sg.CourseRegistrationId);

    int inserted = 0;
    int updated = 0;
    int skipped = 0;

    var row = 6;

    while (true)
    {
        var studentIdText = worksheet.Cell(row, 1).GetValue<string>()?.Trim();
        var newScoreText = worksheet.Cell(row, 6).GetValue<string>()?.Trim();

        var rowIsEmpty =
            string.IsNullOrWhiteSpace(worksheet.Cell(row, 1).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(row, 2).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(row, 3).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(row, 4).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(row, 5).GetValue<string>()) &&
            string.IsNullOrWhiteSpace(worksheet.Cell(row, 6).GetValue<string>());

        if (rowIsEmpty)
            break;

        if (!int.TryParse(studentIdText, out var studentId))
        {
            skipped++;
            row++;
            continue;
        }

        if (!registrationMap.TryGetValue(studentId, out var registration))
        {
            skipped++;
            row++;
            continue;
        }

        if (string.IsNullOrWhiteSpace(newScoreText))
        {
            skipped++;
            row++;
            continue;
        }

        if (!decimal.TryParse(newScoreText, out var newScore) || newScore < 0 || newScore > 100)
        {
            skipped++;
            row++;
            continue;
        }

        if (existingGrades.TryGetValue(registration.Id, out var existingGrade))
        {
            existingGrade.Score = newScore;
            updated++;
        }
        else
        {
            var grade = new StudentGrade
            {
                CourseRegistrationId = registration.Id,
                GradeComponentId = componentId,
                Score = newScore
            };

            _context.StudentGrades.Add(grade);
            inserted++;
        }

        row++;
    }

    await _context.SaveChangesAsync();

    return Ok(new
    {
        message = "Import tamamlandi.",
        SectionId = sectionId,
        ComponentId = componentId,
        Inserted = inserted,
        Updated = updated,
        Skipped = skipped
    });
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
            return Forbid();

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
        var criteria = await _context.GradingCriteria
            .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId);

        var letterPolicy = await _examRegulationResolver.ResolveCourseLetterPolicyAsync(
            section.CourseId, section.TermId, default);

        GradingCriteria effectiveCriteria;
        if (letterPolicy != null && letterPolicy.HasUniqueThresholds && (!letterPolicy.Editable || criteria == null))
        {
            // Regulation-driven: build a transient GradingCriteria from the policy thresholds.
            // Do NOT persist — used only for letter resolution in this finalize run.
            effectiveCriteria = new GradingCriteria
            {
                AAMin = letterPolicy.AAMin, BAMin = letterPolicy.BAMin, BBMin = letterPolicy.BBMin,
                CBMin = letterPolicy.CBMin, CCMin = letterPolicy.CCMin, DCMin = letterPolicy.DCMin,
                DDMin = letterPolicy.DDMin
            };
        }
        else if (criteria != null)
        {
            effectiveCriteria = criteria;
        }
        else
        {
            if (letterPolicy != null && !letterPolicy.Editable && !letterPolicy.HasUniqueThresholds)
                return BadRequest(new { error = "Bu dersin bağlı programları arasında çelişen harf aralıkları var. Sınav müfredatını (harf aralıklarını) düzeltin." });

            return BadRequest(new { error = "Harf notu aralıkları girilmeden ders sonuçlandırılamaz." });
        }

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

        // Config keys with defaults: 14 weeks / 30% theory / 20% lab
        var weekCount = Math.Max(1, await _systemConfig.GetIntAsync("Attendance.TermWeekCount", 14));
        var theoryLimitPercent = await _systemConfig.GetDecimalAsync("Attendance.TheoryAbsenceLimitPercent", 30m);
        var labLimitPercent = await _systemConfig.GetDecimalAsync("Attendance.LabAbsenceLimitPercent", 20m);

        var course = section.Course;
        var theoryTotalHours = weekCount * Math.Max(0, course.TheoryHours);
        var labTotalHours = weekCount * Math.Max(0, course.LabHours);
        var theoryLimitHours = CalculateLimitHours(theoryTotalHours, theoryLimitPercent);
        var labLimitHours = CalculateLimitHours(labTotalHours, labLimitPercent);

        var naRegistrationIds = new HashSet<int>();

        foreach (var reg in registrations)
        {
            if (!attendanceByRegistration.TryGetValue(reg.Id, out var records) || records.Count == 0)
                continue; // No attendance data → not auto-NA

            var (theoryAbs, labAbs) = CalculateAbsenceHours(
                records,
                course.TheoryHours,
                course.LabHours,
                course.ApplicationHours,
                sectionSlots);

            if (IsFailedByAbsence(theoryAbs, labAbs, theoryLimitHours, labLimitHours))
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

            // Base letter: from relative curve (if active) or absolute criteria
            var baseLetter = relativeLetters != null
                ? relativeLetters[reg.Id]
                : effectiveCriteria.ResolveGrade(roundedScore);

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

        var ffRegistrationMap = ffRegistrations.ToDictionary(r => r.StudentId);
        var ffRegistrationIds = ffRegistrations.Select(r => r.Id).ToList();

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
                continue; // only process FF students in this section

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

        // Resolve grading criteria (mirrors Finalize)
        var criteria = await _context.GradingCriteria
            .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId);

        var letterPolicy = await _examRegulationResolver.ResolveCourseLetterPolicyAsync(
            section.CourseId, section.TermId, default);

        GradingCriteria effectiveCriteria;
        if (letterPolicy != null && letterPolicy.HasUniqueThresholds && (!letterPolicy.Editable || criteria == null))
        {
            effectiveCriteria = new GradingCriteria
            {
                AAMin = letterPolicy.AAMin, BAMin = letterPolicy.BAMin, BBMin = letterPolicy.BBMin,
                CBMin = letterPolicy.CBMin, CCMin = letterPolicy.CCMin, DCMin = letterPolicy.DCMin,
                DDMin = letterPolicy.DDMin
            };
        }
        else if (criteria != null)
        {
            effectiveCriteria = criteria;
        }
        else
        {
            return BadRequest(new { error = "Harf notu aralıkları tanımlanmadan bütünleme sonuçlandırılamaz." });
        }

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

            // Recompute with useMakeupScore=true — büt replaces final
            var (weightedScore, roundedScore) = ComputeStudentWeightedScore(
                components, studentGrades, studentReg, useMakeupScore: true);

            var baseLetter = effectiveCriteria.ResolveGrade(roundedScore);
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
            return Forbid();

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
    /// Pre-condition: all gradeRow entries for non-Absent components have a non-null Score
    /// (guaranteed by the missing-grades check in Finalize before this helper is called).
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

        return !await _context.CourseSections
            .AsNoTracking()
            .AnyAsync(other => other.CourseId == section.CourseId
                && other.TermId == section.TermId
                && other.InstructorId != section.InstructorId);
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
}
