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
public record UpdateClassroomDto(string Name, string BuildingName, int Floor, int Capacity, string Type);

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
            Name = dto.Name,
            BuildingName = dto.BuildingName ?? string.Empty,
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

        // İsim değiştiyse duplikat kontrol
        if (dto.Name != classroom.Name)
        {
            var exists = await _context.Classrooms.AnyAsync(c => c.Name == dto.Name && c.Id != id, ct);
            if (exists)
                return BadRequest(new { error = $"'{dto.Name}' adında bir derslik zaten mevcut." });
        }

        classroom.Name = dto.Name;
        classroom.BuildingName = dto.BuildingName ?? string.Empty;
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

        // Aktif section'da kullanılıyorsa silme
        var inUse = await _context.CourseSections
            .AnyAsync(cs => cs.ClassroomId == id && cs.IsActive, ct);
        if (inUse)
            return BadRequest(new { error = "Bu derslik aktif ders şubelerinde kullanılıyor, silinemez." });

        _context.Classrooms.Remove(classroom);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}
