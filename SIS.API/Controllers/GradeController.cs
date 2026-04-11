using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/instructor")]
[Authorize(Roles = "Instructor")]
public class GradeController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaService;

    public GradeController(AppDbContext context, IGpaCalculationService gpaService)
    {
        _context = context;
        _gpaService = gpaService;
    }

    // ══════════════════════════════════════════════════════════
    //  BILEŞEN YÖNETIMI (ders+dönem bazında, tüm section'lar ortak)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// GET api/instructor/section/{sectionId}/components
    /// Dersin not bileşenlerini listeler (Vize, Final, Ödev vb.)
    /// </summary>
    [HttpGet("section/{sectionId}/components")]
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
    public async Task<IActionResult> UpsertComponent(int sectionId, [FromBody] ComponentDto dto)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        // Guard: bileşen ekleme/düzenleme GradeEntry veya öncesi (Planning/Scheduling) olmalı
        var termStatus = await GetTermStatusAsync(section.TermId);
        if (termStatus >= TermStatus.Closed)
            return BadRequest(new { error = "Kapalı veya arşivlenmiş dönemde bileşen değiştirilemez." });

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
    public async Task<IActionResult> DeleteComponent(int sectionId, int componentId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var termStatus2 = await GetTermStatusAsync(section.TermId);
        if (termStatus2 >= TermStatus.Closed)
            return BadRequest(new { error = "Kapalı veya arşivlenmiş dönemde bileşen silinemez." });

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
    public async Task<IActionResult> GetGrades(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

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
    public async Task<IActionResult> SubmitGrades(int sectionId, [FromBody] GradeSubmitDto dto)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var gradeTermStatus = await GetTermStatusAsync(section.TermId);
        if (gradeTermStatus != TermStatus.GradeEntry)
            return BadRequest(new { error = "Not girişi sadece 'Not Girişi' döneminde yapılabilir." });

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
    public async Task<IActionResult> Finalize(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var finTermStatus = await GetTermStatusAsync(section.TermId);
        if (finTermStatus != TermStatus.GradeEntry)
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

        // 2. GradingCriteria al (yoksa varsayılan kullan)
        var criteria = await _context.GradingCriteria
            .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId);

        criteria ??= new GradingCriteria();

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

        // 4. Her öğrenci için harf notu hesapla
        var results = new List<object>();

        foreach (var reg in registrations)
        {
            var studentGrades = allGrades.Where(sg => sg.CourseRegistrationId == reg.Id).ToList();

            // Ağırlıklı ortalama hesapla
            decimal weightedScore = 0;
            bool allComponentsHaveGrades = true;

            foreach (var comp in components)
            {
                var sg = studentGrades.FirstOrDefault(g => g.GradeComponentId == comp.Id);
                if (sg?.Score == null)
                {
                    allComponentsHaveGrades = false;
                    break;
                }
                weightedScore += sg.Score.Value * comp.Weight / 100m;
            }

            GradeCode letterGrade;
            if (!allComponentsHaveGrades)
            {
                // Eksik notu olan öğrenci → NA (devamsızlık/eksik)
                letterGrade = GradeCode.NA;
            }
            else
            {
                var roundedScore = (int)Math.Round(weightedScore, 0, MidpointRounding.AwayFromZero);
                letterGrade = criteria.ResolveGrade(roundedScore);
            }

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
    public async Task<IActionResult> Unfinalize(int sectionId)
    {
        var section = await GetAuthorizedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu section sizin dersleriniz arasinda bulunamadi." });

        var unfTermStatus = await GetTermStatusAsync(section.TermId);
        if (unfTermStatus != TermStatus.GradeEntry)
            return BadRequest(new { error = "Sonuçlandırma iptali sadece 'Not Girişi' döneminde yapılabilir." });

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

    private async Task<TermStatus?> GetTermStatusAsync(int termId)
    {
        var term = await _context.AcademicTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == termId);
        return term?.Status;
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
}
