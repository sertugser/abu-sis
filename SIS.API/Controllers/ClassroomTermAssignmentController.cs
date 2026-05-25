using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Domain.Entities;

namespace SIS.API.Controllers;

public record ClassroomTermAssignmentDto(int Id, int TermId, int ClassroomId, string ClassroomName, int FacultyId, string FacultyName);
public record CreateClassroomTermAssignmentDto(int TermId, int ClassroomId, int FacultyId);
public record BulkAssignmentItemDto(int ClassroomId, int FacultyId);
public record BulkClassroomTermAssignmentDto(int TermId, List<BulkAssignmentItemDto> Assignments);

[ApiController]
[Route("api/admin/classroom-assignments")]
[Authorize]
public class ClassroomTermAssignmentController(AppDbContext context, ClassroomSuggestionService suggestionService) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ClassroomSuggestionService _suggestionService = suggestionService;

    [HttpPost("suggest")]
    [RequirePermission("menu.derslik-fakulte-atamasi.suggest")]
    public async Task<IActionResult> SuggestAssignments([FromQuery] int termId, CancellationToken ct)
    {
        try
        {
            var result = await _suggestionService.SuggestAssignmentsAsync(termId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet]
    [RequirePermission("menu.derslik-fakulte-atamasi.view")]
    public async Task<IActionResult> GetByTerm([FromQuery] int termId, CancellationToken ct)
    {
        var assignments = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Include(a => a.Classroom)
            .Include(a => a.Faculty)
            .Where(a => a.TermId == termId)
            .OrderBy(a => a.Faculty.Name).ThenBy(a => a.Classroom.Name)
            .Select(a => new ClassroomTermAssignmentDto(
                a.Id, a.TermId, a.ClassroomId, a.Classroom.Name,
                a.FacultyId, a.Faculty.Name))
            .ToListAsync(ct);

        return Ok(assignments);
    }

    [HttpPost]
    [RequirePermission("menu.derslik-fakulte-atamasi.create")]
    public async Task<IActionResult> Create([FromBody] CreateClassroomTermAssignmentDto dto, CancellationToken ct)
    {
        // Aynı dönemde aynı derslik başka fakülteye atanmış mı kontrol et
        var exists = await _context.ClassroomTermAssignments
            .AnyAsync(a => a.TermId == dto.TermId && a.ClassroomId == dto.ClassroomId, ct);

        if (exists)
            return BadRequest(new { error = "Bu derslik zaten bu dönemde bir fakülteye atanmış." });

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == dto.TermId, ct);
        if (term == null)
            return NotFound(new { error = "Dönem bulunamadı." });

        var classroom = await _context.Classrooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.ClassroomId, ct);
        if (classroom == null)
            return NotFound(new { error = "Derslik bulunamadı." });

        var faculty = await _context.Faculties.AsNoTracking().FirstOrDefaultAsync(f => f.Id == dto.FacultyId, ct);
        if (faculty == null)
            return NotFound(new { error = "Fakülte bulunamadı." });

        var assignment = new ClassroomTermAssignment
        {
            TermId = dto.TermId,
            ClassroomId = dto.ClassroomId,
            FacultyId = dto.FacultyId
        };

        _context.ClassroomTermAssignments.Add(assignment);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(null, null,
            new ClassroomTermAssignmentDto(assignment.Id, assignment.TermId, assignment.ClassroomId,
                classroom.Name, assignment.FacultyId, faculty.Name));
    }

    [HttpDelete("{id}")]
    [RequirePermission("menu.derslik-fakulte-atamasi.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var assignment = await _context.ClassroomTermAssignments.FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assignment == null)
            return NotFound(new { error = "Atama bulunamadı." });

        _context.ClassroomTermAssignments.Remove(assignment);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("bulk")]
    [RequirePermission("menu.derslik-fakulte-atamasi.create")]
    public async Task<IActionResult> BulkAssign([FromBody] BulkClassroomTermAssignmentDto dto, CancellationToken ct)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == dto.TermId, ct);
        if (term == null)
            return NotFound(new { error = "Dönem bulunamadı." });

        var errors = new List<string>();

        // Doğrulama verilerini tek seferde topla (önceki kod item başına N+1 sorgu yapıyordu).
        var requestedClassroomIds = dto.Assignments.Select(a => a.ClassroomId).Distinct().ToList();
        var requestedFacultyIds = dto.Assignments.Select(a => a.FacultyId).Distinct().ToList();

        var alreadyAssigned = (await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(a => a.TermId == dto.TermId && requestedClassroomIds.Contains(a.ClassroomId))
            .Select(a => a.ClassroomId)
            .ToListAsync(ct)).ToHashSet();

        var classroomNames = await _context.Classrooms
            .AsNoTracking()
            .Where(c => requestedClassroomIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var facultyNames = await _context.Faculties
            .AsNoTracking()
            .Where(f => requestedFacultyIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Name })
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        var toCreate = new List<ClassroomTermAssignment>();
        var seenInRequest = new HashSet<int>(); // aynı istekte tekrarlanan derslik koruması

        foreach (var item in dto.Assignments)
        {
            if (alreadyAssigned.Contains(item.ClassroomId) || !seenInRequest.Add(item.ClassroomId))
            {
                errors.Add($"Derslik {item.ClassroomId} zaten bu dönemde atanmış.");
                continue;
            }
            if (!classroomNames.ContainsKey(item.ClassroomId))
            {
                errors.Add($"Derslik {item.ClassroomId} bulunamadı.");
                continue;
            }
            if (!facultyNames.ContainsKey(item.FacultyId))
            {
                errors.Add($"Fakülte {item.FacultyId} bulunamadı.");
                continue;
            }

            toCreate.Add(new ClassroomTermAssignment
            {
                TermId = dto.TermId,
                ClassroomId = item.ClassroomId,
                FacultyId = item.FacultyId
            });
        }

        if (toCreate.Count > 0)
        {
            _context.ClassroomTermAssignments.AddRange(toCreate);
            await _context.SaveChangesAsync(ct); // tek transaction
        }

        var created = toCreate.Select(a => new ClassroomTermAssignmentDto(
            a.Id, a.TermId, a.ClassroomId, classroomNames[a.ClassroomId],
            a.FacultyId, facultyNames[a.FacultyId])).ToList();

        return Ok(new { created, errors });
    }
}
