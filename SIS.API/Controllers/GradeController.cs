using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using ClosedXML.Excel;

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

    public GradeController(
        AppDbContext context,
        IGpaCalculationService gpaService,
        IDateTimeProvider clock,
        ISystemConfigProvider systemConfig,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _gpaService = gpaService;
        _clock = clock;
        _systemConfig = systemConfig;
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
                Order = dto.Order
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
                && cr.Status != RegistrationStatus.Draft)
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
                    c => studentGrades.FirstOrDefault(g => g.GradeComponentId == c.Id)?.Score)
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
                && cr.Status != RegistrationStatus.Draft)
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
            && cr.Status != RegistrationStatus.Draft)
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
public async Task<IActionResult> ImportPreview(int sectionId, [FromForm] ImportFileDto request)
{
    var file = request.File;

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
            && cr.Status != RegistrationStatus.Draft)
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
public async Task<IActionResult> ImportApply(int sectionId, [FromForm] ImportFileDto request)
{
    var file = request.File;
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
            && cr.Status != RegistrationStatus.Draft)
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

        // 2. Harf notu aralıkları sonuçlandırmadan önce kaydedilmiş olmalı.
        var criteria = await _context.GradingCriteria
            .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId);

        if (criteria == null)
            return BadRequest(new { error = "Harf notu aralıkları girilmeden ders sonuçlandırılamaz." });

        // 3. Section'daki aktif öğrencileri al
        var registrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft)
            .ToListAsync();

        var registrationIds = registrations.Select(r => r.Id).ToList();
        var allGrades = await _context.StudentGrades
            .Where(sg => registrationIds.Contains(sg.CourseRegistrationId))
            .ToListAsync();

        var gradesByRegistration = allGrades
            .GroupBy(grade => grade.CourseRegistrationId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var missingGrades = registrations
            .SelectMany(reg => components
                .Where(component => !gradesByRegistration
                    .GetValueOrDefault(reg.Id, new List<StudentGrade>())
                    .Any(grade => grade.GradeComponentId == component.Id && grade.Score != null))
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

        // 4. Her öğrenci için harf notu hesapla
        var results = new List<object>();

        foreach (var reg in registrations)
        {
            var studentGrades = gradesByRegistration.GetValueOrDefault(reg.Id, new List<StudentGrade>());

            // Ağırlıklı ortalama hesapla
            decimal weightedScore = 0;

            foreach (var comp in components)
            {
                var score = studentGrades.First(g => g.GradeComponentId == comp.Id).Score!.Value;
                weightedScore += score * comp.Weight / 100m;
            }

            var roundedScore = (int)Math.Round(weightedScore, 0, MidpointRounding.AwayFromZero);
            var letterGrade = criteria.ResolveGrade(roundedScore);

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
                && cr.Status != RegistrationStatus.Draft)
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

        var isDefault = criteria == null;
        criteria ??= new GradingCriteria();

        return Ok(new
        {
            criteria.AAMin,
            criteria.BAMin,
            criteria.BBMin,
            criteria.CBMin,
            criteria.CCMin,
            criteria.DCMin,
            criteria.DDMin
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
        var genericGate = await EnsureGradeEntryModuleOpenAsync(termId, fallbackMessage);
        if (genericGate is not null)
            return genericGate;

        var controlCode = ResolveGradeEntryControlCode(componentName);
        if (string.Equals(controlCode, "instructor-grade-entry", StringComparison.OrdinalIgnoreCase))
            return null;

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
}
