using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.API.Security;
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

    public GradeController(AppDbContext context, IGpaCalculationService gpaService, IDateTimeProvider clock)
    {
        _context = context;
        _gpaService = gpaService;
        _clock = clock;
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

        if (section != null)
            return BadRequest(new
        {
            error = "Not girisi ekraninda sinav bileseni eklenemez veya duzenlenemez. Bu islem Ders Islemleri > Sinav Mufredati ekranindan yapilmalidir."
        });

        // Guard: bileşen ekleme/düzenleme iptal edilmiş dönemde yapılamaz
        if (!await CanMutateSharedGradeConfigurationAsync(section!))
            return Forbid();

        var termIsCancelled = await GetTermIsCancelledAsync(section!.TermId);
        if (termIsCancelled == true)
            return BadRequest(new { error = "İptal edilmiş dönemde bileşen değiştirilemez." });

        if (section.AreGradesPublished)
            return BadRequest(new { error = "Yayinlanmis notlarda bilesen degistirilemez. Once notlari ogrencilerden geri cekin." });

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

        if (section != null)
            return BadRequest(new
        {
            error = "Not girisi ekraninda sinav bileseni silinemez. Bu islem Ders Islemleri > Sinav Mufredati ekranindan yapilmalidir."
        });

        if (!await CanMutateSharedGradeConfigurationAsync(section!))
            return Forbid();

        var termIsCancelled2 = await GetTermIsCancelledAsync(section!.TermId);
        if (termIsCancelled2 == true)
            return BadRequest(new { error = "İptal edilmiş dönemde bileşen silinemez." });

        if (section.AreGradesPublished)
            return BadRequest(new { error = "Yayinlanmis notlarda bilesen silinemez. Once notlari ogrencilerden geri cekin." });

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

        var components = await _context.GradeComponents
            .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
            .OrderBy(gc => gc.Order)
            .ToListAsync();

        var criteria = await _context.GradingCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId);

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
            var barrierFailure = components
                .Where(component => component.MinimumScore.HasValue)
                .Select(component => new
                {
                    Component = component,
                    Score = studentGrades.FirstOrDefault(grade => grade.GradeComponentId == component.Id)?.Score
                })
                .FirstOrDefault(item => item.Score.HasValue && item.Score.Value < item.Component.MinimumScore!.Value);

            // Ağırlıklı ortalamayı hesapla
            decimal? weightedAverage = null;
            if (components.Count > 0 && studentGrades.Any(sg => sg.Score != null))
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
                BarrierFailureReason = barrierFailure == null
                    ? null
                    : $"{barrierFailure.Component.Name} barajinin altinda kaldi ({barrierFailure.Score:0.##}/{barrierFailure.Component.MinimumScore:0.##}).",
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
            AreGradesPublished = section.AreGradesPublished,
            GradesPublishedAt = section.GradesPublishedAt,
            CalculationMethod = criteria?.CalculationMethod ?? "Absolute",
            Components = components.Select(c => new { c.Id, c.Name, c.Weight, c.Order, c.MinimumScore }),
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

        if (!term.IsGradeEntryOpen(_clock.UtcNow))
            return BadRequest(new { error = "Not girişi sadece 'Not Girişi' döneminde yapılabilir." });

        if (section.AreGradesPublished)
            return BadRequest(new { error = "Yayinlanmis notlar degistirilemez. Once notlari ogrencilerden geri cekin." });

        if (section.IsFinalized)
            return BadRequest(new { error = "Sonuclandirilmis ders icin not girilemez. Once sonuclandirmayi iptal edin." });

        // Bileşenin bu derse ait olduğunu doğrula
        var component = await _context.GradeComponents
            .FirstOrDefaultAsync(gc => gc.Id == dto.ComponentId
                && gc.CourseId == section.CourseId
                && gc.AcademicTermId == section.TermId);

        if (component == null)
            return NotFound(new { error = "Bilesen bulunamadi." });

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

            if (entry.Score is < 0 or > 100)
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
public async Task<IActionResult> ExportTemplate(int sectionId)
{
    var section = await GetAuthorizedSectionAsync(sectionId);
    if (section == null)
        return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

    var term = await _context.AcademicTerms.AsNoTracking()
        .Include(t => t.AcademicCalendar)
        .FirstOrDefaultAsync(t => t.Id == section.TermId);
    if (term == null)
        return NotFound(new { error = "Dönem bulunamadi." });

    if (!term.IsGradeEntryOpen(_clock.UtcNow))
        return BadRequest(new { error = "Excel import sadece 'Not Girisi' doneminde yapilabilir." });

    if (section.IsFinalized)
        return BadRequest(new { error = "Sonuclandirilmis ders icin Excel sablonu olusturulamaz." });

    var components = await _context.GradeComponents
        .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
        .OrderBy(gc => gc.Order)
        .ThenBy(gc => gc.Id)
        .ToListAsync();

    if (components.Count == 0)
        return BadRequest(new { error = "Bu ders icin sinav mufredati tanimlanmamis." });

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

    var componentIds = components.Select(c => c.Id).ToList();

    var existingGrades = await _context.StudentGrades
        .Where(sg => componentIds.Contains(sg.GradeComponentId)
            && registrationIds.Contains(sg.CourseRegistrationId))
        .ToListAsync();
    var gradeLookup = existingGrades.ToDictionary(
        sg => (sg.CourseRegistrationId, sg.GradeComponentId),
        sg => sg.Score);

    using var workbook = new XLWorkbook();
    var worksheet = workbook.Worksheets.Add("Grades");

    worksheet.Cell(1, 1).Value = "Course";
    worksheet.Cell(1, 2).Value = $"{section.Course.Code} - {section.Course.Name}";
    worksheet.Cell(2, 1).Value = "Section";
    worksheet.Cell(2, 2).Value = section.SectionNumber;
    worksheet.Cell(3, 1).Value = "Active Students";
    worksheet.Cell(3, 2).Value = registrations.Count;
    worksheet.Cell(4, 1).Value = "SectionId";
    worksheet.Cell(4, 2).Value = section.Id;

    worksheet.Cell(5, 1).Value = "Student Number";
    worksheet.Cell(5, 2).Value = "First Name";
    worksheet.Cell(5, 3).Value = "Last Name";

    var column = 4;
    foreach (var component in components)
    {
        worksheet.Cell(4, column).Value = component.Id;
        worksheet.Cell(4, column + 1).Value = component.Id;
        worksheet.Cell(5, column).Value = $"{component.Name} Score";
        worksheet.Cell(5, column + 1).Value = $"{component.Name} New Score";
        column += 2;
    }

    var row = 6;

    foreach (var reg in registrations
        .OrderBy(r => userMap.GetValueOrDefault(r.Student.UserId)?.LastName)
        .ThenBy(r => userMap.GetValueOrDefault(r.Student.UserId)?.FirstName))
    {
        var user = userMap.GetValueOrDefault(reg.Student.UserId);
        worksheet.Cell(row, 1).Value = reg.Student.StudentNumber;
        worksheet.Cell(row, 2).Value = user?.FirstName ?? "";
        worksheet.Cell(row, 3).Value = user?.LastName ?? "";

        column = 4;
        foreach (var component in components)
        {
            gradeLookup.TryGetValue((reg.Id, component.Id), out var currentScore);
            worksheet.Cell(row, column).Value = currentScore;
            worksheet.Cell(row, column + 1).Value = "";
            column += 2;
        }

        row++;
    }

    worksheet.Row(4).Hide();
    worksheet.SheetView.FreezeRows(5);
    worksheet.Range(1, 1, 5, Math.Max(5, column - 1)).Style.Font.Bold = true;
    worksheet.Columns().AdjustToContents();

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);

    var fileName = $"{section.Course.Code}_section_{section.SectionNumber}_grades.xlsx";

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

    if (!term.IsGradeEntryOpen(_clock.UtcNow))
        return BadRequest(new { error = "Excel import sadece 'Not Girisi' doneminde yapilabilir." });

    if (section.IsFinalized)
        return BadRequest(new { error = "Sonuclandirilmis ders icin import yapilamaz." });

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    using var workbook = new XLWorkbook(stream);
    var workbookValidationError = FileUploadValidation.ValidateWorkbookShape(workbook);
    if (workbookValidationError is not null)
        return BadRequest(new { error = workbookValidationError });

    var worksheet = workbook.Worksheet("Grades");

    var templateSectionIdText = worksheet.Cell(4, 2).GetValue<string>();

    if (!int.TryParse(templateSectionIdText, out var templateSectionId) || templateSectionId != sectionId)
        return BadRequest(new { error = "Yanlis section sablonu yuklendi." });

    var components = await _context.GradeComponents
        .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
        .OrderBy(gc => gc.Order)
        .ThenBy(gc => gc.Id)
        .ToListAsync();
    var componentMap = components.ToDictionary(c => c.Id);

    var componentColumns = new List<(int ComponentId, string ComponentName, int CurrentColumn, int NewColumn)>();
    var column = 4;
    while (true)
    {
        var currentHeader = worksheet.Cell(5, column).GetValue<string>()?.Trim();
        var newHeader = worksheet.Cell(5, column + 1).GetValue<string>()?.Trim();
        var componentIdText = worksheet.Cell(4, column + 1).GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(currentHeader) && string.IsNullOrWhiteSpace(newHeader))
            break;

        if (!int.TryParse(componentIdText, out var parsedComponentId)
            || !componentMap.TryGetValue(parsedComponentId, out var parsedComponent))
        {
            return BadRequest(new { error = "Sablondaki bilesen bilgisi okunamadi. Lutfen yeni sablon indirip tekrar deneyin." });
        }

        componentColumns.Add((parsedComponent.Id, parsedComponent.Name, column, column + 1));
        column += 2;
    }

    if (componentColumns.Count == 0)
        return BadRequest(new { error = "Sablonda not bileseni bulunamadi." });

    var registrations = await _context.CourseRegistrations
        .Include(cr => cr.Student)
        .Where(cr => cr.CourseSectionId == sectionId
            && cr.Status != RegistrationStatus.Dropped
            && cr.Status != RegistrationStatus.Draft)
        .ToListAsync();

    var registrationMap = registrations
        .GroupBy(r => r.Student.StudentNumber)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    var registrationIds = registrations.Select(r => r.Id).ToList();
    var componentIds = componentColumns.Select(c => c.ComponentId).ToList();

    var existingGrades = await _context.StudentGrades
        .Where(sg => componentIds.Contains(sg.GradeComponentId)
            && registrationIds.Contains(sg.CourseRegistrationId))
        .ToListAsync();
    var gradeLookup = existingGrades.ToDictionary(
        sg => (sg.CourseRegistrationId, sg.GradeComponentId),
        sg => sg.Score);

    var previewRows = new List<object>();
    int validCount = 0;
    int warningCount = 0;
    int errorCount = 0;
    int emptyCount = 0;

    var row = 6;
    var lastComponentColumn = componentColumns.Max(c => c.NewColumn);

    while (true)
    {
        var studentNumber = worksheet.Cell(row, 1).GetValue<string>()?.Trim();
        var firstName = worksheet.Cell(row, 2).GetValue<string>()?.Trim();
        var lastName = worksheet.Cell(row, 3).GetValue<string>()?.Trim();
        var anyScoreCell = false;
        for (var scoreColumn = 4; scoreColumn <= lastComponentColumn; scoreColumn++)
        {
            if (!string.IsNullOrWhiteSpace(worksheet.Cell(row, scoreColumn).GetValue<string>()))
            {
                anyScoreCell = true;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(studentNumber)
            && string.IsNullOrWhiteSpace(firstName)
            && string.IsNullOrWhiteSpace(lastName)
            && !anyScoreCell)
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(studentNumber)
            || !registrationMap.TryGetValue(studentNumber, out var registration))
        {
            if (componentColumns.Any(c => !string.IsNullOrWhiteSpace(worksheet.Cell(row, c.NewColumn).GetValue<string>())))
            {
                warningCount++;
                previewRows.Add(new
                {
                    RowNumber = row,
                    StudentId = (int?)null,
                    StudentNumber = studentNumber ?? "",
                    FirstName = firstName ?? "",
                    LastName = lastName ?? "",
                    ComponentId = (int?)null,
                    ComponentName = "",
                    CurrentScore = (decimal?)null,
                    NewScore = (decimal?)null,
                    RawNewScore = "",
                    Status = "Warning",
                    Message = "Kayitsiz ogrenci, bu satirdaki notlar atlanacak."
                });
            }
            row++;
            continue;
        }

        foreach (var componentColumn in componentColumns)
        {
            var newScoreText = worksheet.Cell(row, componentColumn.NewColumn).GetValue<string>()?.Trim();
            gradeLookup.TryGetValue((registration.Id, componentColumn.ComponentId), out var currentScore);

            if (string.IsNullOrWhiteSpace(newScoreText))
            {
                emptyCount++;
                continue;
            }

            string status;
            string message;
            decimal? parsedScore = null;

            if (!decimal.TryParse(newScoreText, out var score))
            {
                status = "Error";
                message = "Not sayisal degil.";
                errorCount++;
            }
            else if (score < 0 || score > 100)
            {
                status = "Error";
                message = "Not 0-100 araliginda olmali.";
                errorCount++;
            }
            else
            {
                parsedScore = score;
                if (currentScore.HasValue && currentScore.Value != parsedScore.Value)
                {
                    status = "Update";
                    message = $"Mevcut not guncellenecek ({currentScore.Value} -> {parsedScore.Value}).";
                    warningCount++;
                }
                else
                {
                    status = "Valid";
                    message = "Gecerli not, kaydedilebilir.";
                    validCount++;
                }
            }

            previewRows.Add(new
            {
                RowNumber = row,
                StudentId = registration.StudentId,
                StudentNumber = studentNumber ?? "",
                FirstName = firstName ?? "",
                LastName = lastName ?? "",
                ComponentId = componentColumn.ComponentId,
                ComponentName = componentColumn.ComponentName,
                CurrentScore = currentScore,
                NewScore = parsedScore,
                RawNewScore = newScoreText,
                Status = status,
                Message = message
            });
        }

        row++;
    }

    return Ok(new
    {
        SectionId = section.Id,
        ComponentId = 0,
        ComponentName = "Tum Bilesenler",
        TotalRows = previewRows.Count + emptyCount,
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

    if (!term.IsGradeEntryOpen(_clock.UtcNow))
        return BadRequest(new { error = "Excel import sadece 'Not Girisi' doneminde yapilabilir." });

    if (section.IsFinalized)
        return BadRequest(new { error = "Sonuclandirilmis ders icin import yapilamaz." });

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    using var workbook = new XLWorkbook(stream);
    var workbookValidationError = FileUploadValidation.ValidateWorkbookShape(workbook);
    if (workbookValidationError is not null)
        return BadRequest(new { error = workbookValidationError });

    var worksheet = workbook.Worksheet("Grades");

    var templateSectionIdText = worksheet.Cell(4, 2).GetValue<string>();

    if (!int.TryParse(templateSectionIdText, out var templateSectionId) || templateSectionId != sectionId)
        return BadRequest(new { error = "Yanlis section sablonu yuklendi." });

    var components = await _context.GradeComponents
        .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
        .OrderBy(gc => gc.Order)
        .ThenBy(gc => gc.Id)
        .ToListAsync();
    var componentMap = components.ToDictionary(c => c.Id);

    var componentColumns = new List<(int ComponentId, string ComponentName, int CurrentColumn, int NewColumn)>();
    var column = 4;
    while (true)
    {
        var currentHeader = worksheet.Cell(5, column).GetValue<string>()?.Trim();
        var newHeader = worksheet.Cell(5, column + 1).GetValue<string>()?.Trim();
        var componentIdText = worksheet.Cell(4, column + 1).GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(currentHeader) && string.IsNullOrWhiteSpace(newHeader))
            break;

        if (!int.TryParse(componentIdText, out var parsedComponentId)
            || !componentMap.TryGetValue(parsedComponentId, out var parsedComponent))
        {
            return BadRequest(new { error = "Sablondaki bilesen bilgisi okunamadi. Lutfen yeni sablon indirip tekrar deneyin." });
        }

        componentColumns.Add((parsedComponent.Id, parsedComponent.Name, column, column + 1));
        column += 2;
    }

    if (componentColumns.Count == 0)
        return BadRequest(new { error = "Sablonda not bileseni bulunamadi." });

    var registrations = await _context.CourseRegistrations
        .Include(cr => cr.Student)
        .Where(cr => cr.CourseSectionId == sectionId
            && cr.Status != RegistrationStatus.Dropped
            && cr.Status != RegistrationStatus.Draft)
        .ToListAsync();

    var registrationMap = registrations
        .GroupBy(r => r.Student.StudentNumber)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    var registrationIds = registrations.Select(r => r.Id).ToList();
    var componentIds = componentColumns.Select(c => c.ComponentId).ToList();

    var existingGrades = await _context.StudentGrades
        .Where(sg => componentIds.Contains(sg.GradeComponentId)
            && registrationIds.Contains(sg.CourseRegistrationId))
        .ToListAsync();
    var gradeLookup = existingGrades.ToDictionary(
        sg => (sg.CourseRegistrationId, sg.GradeComponentId));

    int inserted = 0;
    int updated = 0;
    int skipped = 0;

    var row = 6;
    var lastComponentColumn = componentColumns.Max(c => c.NewColumn);

    while (true)
    {
        var studentNumber = worksheet.Cell(row, 1).GetValue<string>()?.Trim();
        var firstName = worksheet.Cell(row, 2).GetValue<string>()?.Trim();
        var lastName = worksheet.Cell(row, 3).GetValue<string>()?.Trim();
        var anyScoreCell = false;
        for (var scoreColumn = 4; scoreColumn <= lastComponentColumn; scoreColumn++)
        {
            if (!string.IsNullOrWhiteSpace(worksheet.Cell(row, scoreColumn).GetValue<string>()))
            {
                anyScoreCell = true;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(studentNumber)
            && string.IsNullOrWhiteSpace(firstName)
            && string.IsNullOrWhiteSpace(lastName)
            && !anyScoreCell)
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(studentNumber)
            || !registrationMap.TryGetValue(studentNumber, out var registration))
        {
            skipped += componentColumns.Count(c => !string.IsNullOrWhiteSpace(worksheet.Cell(row, c.NewColumn).GetValue<string>()));
            row++;
            continue;
        }

        foreach (var componentColumn in componentColumns)
        {
            var newScoreText = worksheet.Cell(row, componentColumn.NewColumn).GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(newScoreText))
            {
                continue;
            }

            if (!decimal.TryParse(newScoreText, out var newScore) || newScore < 0 || newScore > 100)
            {
                skipped++;
                continue;
            }

            if (gradeLookup.TryGetValue((registration.Id, componentColumn.ComponentId), out var existingGrade))
            {
                existingGrade.Score = newScore;
                updated++;
            }
            else
            {
                var grade = new StudentGrade
                {
                    CourseRegistrationId = registration.Id,
                    GradeComponentId = componentColumn.ComponentId,
                    Score = newScore
                };

                _context.StudentGrades.Add(grade);
                gradeLookup[(registration.Id, componentColumn.ComponentId)] = grade;
                inserted++;
            }
        }

        row++;
    }

    await _context.SaveChangesAsync();

    return Ok(new
    {
        message = "Import tamamlandi.",
        SectionId = sectionId,
        ComponentId = 0,
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

        if (!term.IsGradeEntryOpen(_clock.UtcNow))
            return BadRequest(new { error = "Sonuçlandırma sadece 'Not Girişi' döneminde yapılabilir." });

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
            var barrierFailure = components
                .Where(component => component.MinimumScore.HasValue)
                .Select(component => new
                {
                    Component = component,
                    Score = studentGrades.First(grade => grade.GradeComponentId == component.Id).Score!.Value
                })
                .FirstOrDefault(item => item.Score < item.Component.MinimumScore!.Value);

            var barrierFailureReason = barrierFailure == null
                ? null
                : $"{barrierFailure.Component.Name} barajinin altinda kaldi ({barrierFailure.Score:0.##}/{barrierFailure.Component.MinimumScore:0.##}).";
            var letterGrade = barrierFailure == null
                ? criteria.ResolveGrade(roundedScore)
                : GradeCode.FF;

            reg.Grade = letterGrade;
            reg.UpdatedAt = DateTime.UtcNow;

            results.Add(new
            {
                StudentId = reg.StudentId,
                WeightedAverage = Math.Round(weightedScore, 2),
                LetterGrade = letterGrade.ToString(),
                BarrierFailureReason = barrierFailureReason
            });
        }

        // 5. Section'ı sonuçlandır
        section.IsFinalized = true;
        section.AreGradesPublished = false;
        section.GradesPublishedAt = null;
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

        if (!term.IsGradeEntryOpen(_clock.UtcNow))
            return BadRequest(new { error = "Sonuçlandırma iptali sadece 'Not Girişi' döneminde yapılabilir." });

        if (section.AreGradesPublished)
            return BadRequest(new { error = "Yayinlanmis notlar geri cekilmeden sonuclandirma iptal edilemez." });

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
        section.AreGradesPublished = false;
        section.GradesPublishedAt = null;
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
            criteria.DDMin,
            criteria.CalculationMethod
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
        if (!await CanMutateSharedGradeConfigurationAsync(section!))
            return Forbid();

        if (section.AreGradesPublished)
            return BadRequest(new { error = "Yayinlanmis notlarda harf araligi degistirilemez. Once notlari ogrencilerden geri cekin." });

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
        criteria.CalculationMethod = string.Equals(dto.CalculationMethod, "Relative", StringComparison.OrdinalIgnoreCase)
            ? "Relative"
            : criteria.CalculationMethod;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Not araliklari guncellendi. Sonuclandirilmis sectionlar etkilenmez, yeniden sonuclandirma gerekir." });
    }

    // ══════════════════════════════════════════════════════════
    //  YARDIMCI METOTLAR
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Giriş yapan hocaya ait section'ı doğrular ve döner.
    /// Hocanın o dersin herhangi bir section'ına atanmış olması yeterlidir.
    /// </summary>
    [HttpPost("section/{sectionId}/publish")]
    [RequirePermission("menu.not-girisi.finalize")]
    public async Task<IActionResult> PublishGrades(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!section.IsFinalized)
            return BadRequest(new { error = "Notlar ogrencilere acilmadan once ders sonuclandirilmalidir." });

        if (section.AreGradesPublished)
            return BadRequest(new { error = "Notlar zaten ogrencilere acik." });

        var hasMissingLetterGrade = await _context.CourseRegistrations
            .AsNoTracking()
            .AnyAsync(cr => cr.CourseSectionId == sectionId
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Grade == null);

        if (hasMissingLetterGrade)
            return BadRequest(new { error = "Harf notu eksik ogrenci varken notlar yayinlanamaz." });

        section.AreGradesPublished = true;
        section.GradesPublishedAt = _clock.UtcNow;
        section.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Notlar ogrencilere acildi.",
            section.AreGradesPublished,
            section.GradesPublishedAt
        });
    }

    [HttpPost("section/{sectionId}/unpublish")]
    [RequirePermission("menu.not-girisi.unfinalize")]
    public async Task<IActionResult> UnpublishGrades(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        if (!section.AreGradesPublished)
            return BadRequest(new { error = "Notlar zaten ogrencilere kapali." });

        section.AreGradesPublished = false;
        section.GradesPublishedAt = null;
        section.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Notlar ogrenci gorunumunden geri cekildi." });
    }

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
    }

    public class GradeSubmitDto
    {
        public int ComponentId { get; set; }
        public List<StudentScoreDto> Grades { get; set; } = new();
    }

    public class StudentScoreDto
    {
        public int StudentId { get; set; }
        public decimal? Score { get; set; }
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
        public string? CalculationMethod { get; set; }
    }
    public class ImportFileDto
{
    public IFormFile File { get; set; } = null!;
}
}
