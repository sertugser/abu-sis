using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record ClassroomDto(int Id, string Name, string BuildingName, int Floor, int Capacity, string Type);
public record CreateClassroomDto(string Name, string BuildingName, int Floor, int Capacity, string Type);
public record UpdateClassroomDto(string Name, string BuildingName, int Floor, int Capacity, string Type, bool ConfirmScheduleImpact = false);

/// <summary>Bir dersliğin tipi/kapasitesi değiştiğinde geçersiz kalabilecek mevcut program satırı.</summary>
public record ClassroomScheduleImpactDto(string CourseCode, string CourseName, int SectionNumber, string Reason);

[ApiController]
[Route("api/admin/classrooms")]
[Authorize]
public class ClassroomController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    [HttpGet]
    [RequirePermission("menu.derslik-yonetimi.view")]
    public async Task<IActionResult> GetAll([FromQuery] string? type, CancellationToken ct)
    {
        var query = _context.Classrooms.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<ClassroomType>(type, true, out var classroomType))
            query = query.Where(c => c.Type == classroomType);

        var classrooms = await query
            .OrderBy(c => c.BuildingName).ThenBy(c => c.Floor).ThenBy(c => c.Name)
            .Select(c => new ClassroomDto(c.Id, c.Name, c.BuildingName, c.Floor, c.Capacity, c.Type.ToString()))
            .ToListAsync(ct);

        return Ok(classrooms);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.derslik-yonetimi.view")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var classroom = await _context.Classrooms.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (classroom == null)
            return NotFound(new { error = "Derslik bulunamadı." });

        return Ok(new ClassroomDto(classroom.Id, classroom.Name, classroom.BuildingName, classroom.Floor, classroom.Capacity, classroom.Type.ToString()));
    }

    [HttpPost]
    [RequirePermission("menu.derslik-yonetimi.create")]
    public async Task<IActionResult> Create([FromBody] CreateClassroomDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Derslik adı boş olamaz." });

        if (dto.Capacity <= 0)
            return BadRequest(new { error = "Kapasite 0'dan büyük olmalıdır." });

        if (!Enum.TryParse<ClassroomType>(dto.Type, true, out var classroomType))
            return BadRequest(new { error = $"Geçersiz derslik tipi: {dto.Type}. Geçerli tipler: Lecture, Lab, Computer, Seminar, Amphitheater" });

        var exists = await _context.Classrooms.AnyAsync(c => c.Name == dto.Name, ct);
        if (exists)
            return BadRequest(new { error = $"'{dto.Name}' adında bir derslik zaten mevcut." });

        var classroom = new Classroom
        {
            Name = dto.Name.Trim(),
            BuildingName = dto.BuildingName?.Trim() ?? string.Empty,
            Floor = dto.Floor,
            Capacity = dto.Capacity,
            Type = classroomType,
            CreatedAt = DateTime.UtcNow
        };

        _context.Classrooms.Add(classroom);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = classroom.Id },
            new ClassroomDto(classroom.Id, classroom.Name, classroom.BuildingName, classroom.Floor, classroom.Capacity, classroom.Type.ToString()));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.derslik-yonetimi.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateClassroomDto dto, CancellationToken ct)
    {
        var classroom = await _context.Classrooms.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (classroom == null)
            return NotFound(new { error = "Derslik bulunamadı." });

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Derslik adı boş olamaz." });

        if (dto.Capacity <= 0)
            return BadRequest(new { error = "Kapasite 0'dan büyük olmalıdır." });

        if (!Enum.TryParse<ClassroomType>(dto.Type, true, out var classroomType))
            return BadRequest(new { error = $"Geçersiz derslik tipi: {dto.Type}" });

        var name = dto.Name.Trim();

        // İsim değiştiyse duplikat kontrol
        if (name != classroom.Name)
        {
            var exists = await _context.Classrooms.AnyAsync(c => c.Name == name && c.Id != id, ct);
            if (exists)
                return BadRequest(new { error = $"'{name}' adında bir derslik zaten mevcut." });
        }

        // Tip değişimi veya kapasite düşüşü, bu dersliğe atanmış mevcut program satırlarını
        // (SectionSchedule) sessizce geçersiz kılabilir: tip kuralı odayı reddedebilir ya da
        // küçülen kapasite section'ın kontenjanına yetmeyebilir. Admin onaylamadan kaydı bloklarız.
        var typeChanged = classroomType != classroom.Type;
        var capacityReduced = dto.Capacity < classroom.Capacity;
        if ((typeChanged || capacityReduced) && !dto.ConfirmScheduleImpact)
        {
            var impact = await BuildScheduleImpactAsync(id, classroomType, dto.Capacity, typeChanged, capacityReduced, ct);
            if (impact.Count > 0)
            {
                return Conflict(new
                {
                    error = $"Bu değişiklik mevcut {impact.Count} ders programı atamasını etkiliyor.",
                    code = "ScheduleImpact",
                    affected = impact,
                });
            }
        }

        classroom.Name = name;
        classroom.BuildingName = dto.BuildingName?.Trim() ?? string.Empty;
        classroom.Floor = dto.Floor;
        classroom.Capacity = dto.Capacity;
        classroom.Type = classroomType;
        classroom.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Ok(new ClassroomDto(classroom.Id, classroom.Name, classroom.BuildingName, classroom.Floor, classroom.Capacity, classroom.Type.ToString()));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.derslik-yonetimi.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var classroom = await _context.Classrooms.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (classroom == null)
            return NotFound(new { error = "Derslik bulunamadı." });

        // Veritabanı düzeyinde silmeyi bloklayan iki Restrict FK'i (SectionSchedule, ClassroomTermAssignment)
        // önceden kontrol edilmezse SaveChanges DbUpdateException fırlatır ve kullanıcı jenerik 500 görür.
        // Her referansı ayrı ayrı kontrol edip anlamlı 400 mesajı döneriz.

        var assignmentCount = await _context.ClassroomTermAssignments
            .CountAsync(cta => cta.ClassroomId == id, ct);
        if (assignmentCount > 0)
            return BadRequest(new
            {
                error = $"Bu derslik {assignmentCount} dönem fakülte atamasında kullanılıyor. Önce 'Derslik-Fakülte Ataması' sayfasından kaldırın.",
                code = "ClassroomInUse",
            });

        var scheduleCount = await _context.SectionSchedules
            .CountAsync(ss => ss.ClassroomId == id, ct);
        if (scheduleCount > 0)
            return BadRequest(new
            {
                error = $"Bu derslik {scheduleCount} ders programı satırında kullanılıyor. Önce ilgili programlardan çıkarın.",
                code = "ClassroomInUse",
            });

        // Aktif section'ın varsayılan dersliği (CourseSection.ClassroomId, SetNull FK) — iş kuralı gereği
        // aktif şubede kullanılan derslik silinmesin (silinirse atama sessizce null'a düşerdi).
        var activeSectionCount = await _context.CourseSections
            .CountAsync(cs => cs.ClassroomId == id && cs.Status == CourseSectionStatus.Active, ct);
        if (activeSectionCount > 0)
            return BadRequest(new
            {
                error = $"Bu derslik {activeSectionCount} aktif ders şubesinde kullanılıyor, silinemez.",
                code = "ClassroomInUse",
            });

        _context.Classrooms.Remove(classroom);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Tip değişimi/kapasite düşüşünün etkilediği mevcut program satırlarını listeler.
    /// Bir section etkilenir: (tip değiştiyse) bu odadaki her atama, VEYA
    /// (kapasite düştüyse) section kontenjanı yeni kapasiteyi aşıyorsa.
    /// </summary>
    private async Task<List<ClassroomScheduleImpactDto>> BuildScheduleImpactAsync(
        int classroomId, ClassroomType newType, int newCapacity,
        bool typeChanged, bool capacityReduced, CancellationToken ct)
    {
        var sections = await _context.SectionSchedules
            .AsNoTracking()
            .Where(ss => ss.ClassroomId == classroomId)
            .Select(ss => new
            {
                ss.CourseSection.SectionNumber,
                ss.CourseSection.Capacity,
                CourseCode = ss.CourseSection.Course.Code,
                CourseName = ss.CourseSection.Course.Name,
            })
            .Distinct()
            .ToListAsync(ct);

        var impact = new List<ClassroomScheduleImpactDto>();
        foreach (var s in sections)
        {
            var overflow = capacityReduced && s.Capacity > newCapacity;
            if (!typeChanged && !overflow)
                continue;

            var reason = overflow
                ? $"Kontenjan {s.Capacity}, yeni kapasite {newCapacity} yetersiz"
                : $"Derslik tipi '{newType}' olarak değişiyor";

            impact.Add(new ClassroomScheduleImpactDto(s.CourseCode, s.CourseName, s.SectionNumber, reason));
        }

        return impact;
    }
}
