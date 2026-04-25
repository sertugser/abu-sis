using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Scheduling.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/classroom-export")]
[Authorize]
public class ClassroomExportController(AppDbContext context, ISchedulingService schedulingService) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ISchedulingService _schedulingService = schedulingService;

    [HttpGet("csv")]
    [RequirePermission("menu.derslik-doluluk.export")]
    public async Task<IActionResult> ExportCsv([FromQuery] int? termId, CancellationToken ct)
    {
        var dashboard = await _schedulingService.GetClassroomDashboardAsync(termId, cancellationToken: ct);
        var classroomMetadata = await _context.Classrooms
            .AsNoTracking()
            .ToDictionaryAsync(
                classroom => classroom.Id,
                classroom => new { classroom.BuildingName, classroom.Floor },
                ct);

        var csv = new StringBuilder();
        csv.AppendLine("ClassroomId,ClassroomName,BuildingName,Floor,Type,Capacity,Day,StartHour,EndHour,CourseCode,CourseName,InstructorName");

        foreach (var classroom in dashboard.Classrooms.OrderBy(c => c.Name))
        {
            var metadata = classroomMetadata.GetValueOrDefault(classroom.Id);
            if (classroom.Schedules.Count == 0)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(classroom.Id.ToString()),
                    EscapeCsv(classroom.Name),
                    EscapeCsv(metadata?.BuildingName),
                    EscapeCsv(metadata?.Floor.ToString()),
                    EscapeCsv(classroom.Type),
                    EscapeCsv(classroom.Capacity.ToString()),
                    EscapeCsv(null),
                    EscapeCsv(null),
                    EscapeCsv(null),
                    EscapeCsv(null),
                    EscapeCsv(null),
                    EscapeCsv(null)));
                continue;
            }

            foreach (var schedule in classroom.Schedules
                .OrderBy(s => s.Day)
                .ThenBy(s => s.StartHour)
                .ThenBy(s => s.CourseCode))
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(classroom.Id.ToString()),
                    EscapeCsv(classroom.Name),
                    EscapeCsv(metadata?.BuildingName),
                    EscapeCsv(metadata?.Floor.ToString()),
                    EscapeCsv(classroom.Type),
                    EscapeCsv(classroom.Capacity.ToString()),
                    EscapeCsv(schedule.Day.ToString()),
                    EscapeCsv(schedule.StartHour.ToString()),
                    EscapeCsv(schedule.EndHour.ToString()),
                    EscapeCsv(schedule.CourseCode),
                    EscapeCsv(schedule.CourseName),
                    EscapeCsv(schedule.InstructorName)));
            }
        }

        var fileName = $"classroom-occupancy-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
