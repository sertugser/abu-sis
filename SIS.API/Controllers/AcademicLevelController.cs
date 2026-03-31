using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicLevel.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdministrativeStaff,SystemAdministrator,DepartmentManager,Dean,AcademicAdvisor")]
public class AcademicLevelController : ControllerBase
{
    private readonly IAcademicLevelService _academicLevelService;
    private readonly AppDbContext _context;

    public AcademicLevelController(IAcademicLevelService academicLevelService, AppDbContext context)
    {
        _academicLevelService = academicLevelService;
        _context = context;
    }

    /// <summary>
    /// Tek bir ogrencinin resmi sinif etiketi ve akademik seviye analizini dondurur.
    /// Uyumsuzluk varsa detayli aciklama icinde gelir.
    /// </summary>
    [HttpGet("student/{studentId}")]
    [Authorize(Roles = "AdministrativeStaff,SystemAdministrator,DepartmentManager,Dean,AcademicAdvisor,Student")]
    public async Task<IActionResult> GetStudentAcademicLevel(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
            return BadRequest(new { error = "Gecerli bir studentId gonderilmelidir." });

        // IDOR koruması: Student rolü sadece kendi verisini görebilir
        if (User.IsInRole("Student"))
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var ownStudentId = await _context.Students
                .Where(s => s.UserId == userId)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (ownStudentId == 0 || ownStudentId != studentId)
                return Forbid();
        }

        var result = await _academicLevelService.GetStudentAcademicLevelAsync(
            studentId, cancellationToken);

        if (result == null)
            return NotFound(new { error = "Ogrenci bulunamadi veya aktif programi yok." });

        return Ok(result);
    }

    /// <summary>
    /// Bir bolumdeki tum aktif ogrencilerin akademik seviye analizini toplu dondurur.
    /// Resmi sinif dagilimi ve akademik seviye dagilimi karsilastirilabilir.
    /// </summary>
    [HttpGet("department/{departmentId}")]
    public async Task<IActionResult> GetDepartmentAcademicLevels(
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        var result = await _academicLevelService.GetDepartmentAcademicLevelsAsync(
            departmentId, cancellationToken);

        return Ok(result);
    }
}
