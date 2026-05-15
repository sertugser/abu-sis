using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService — derslik filter ve arama.
/// GetClassroomFilterOptionsAsync: UI dropdown için faculty/building/type listesi.
/// SearchAvailableClassroomsAsync: belirli zaman/kapasite kriterlerine uyan boş derslikler.
/// </summary>
public partial class SchedulingService
{
    public async Task<ClassroomFilterOptionsDto> GetClassroomFilterOptionsAsync(int termId, CancellationToken cancellationToken = default)
    {
        var assignments = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Include(a => a.Faculty)
            .Include(a => a.Classroom)
            .Where(a => a.TermId == termId)
            .ToListAsync(cancellationToken);

        var faculties = assignments
            .Select(a => new ClassroomFacultyOption { Id = a.FacultyId, Name = a.Faculty.Name })
            .DistinctBy(f => f.Id)
            .OrderBy(f => f.Name)
            .ToList();

        var buildings = assignments
            .Select(a => a.Classroom.BuildingName)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct()
            .OrderBy(b => b)
            .ToList();

        // Client-side evaluation: EF Core cannot translate enum .ToString() to SQL
        var classroomTypes = (await _context.Classrooms
            .AsNoTracking()
            .Select(c => c.Type)
            .Distinct()
            .ToListAsync(cancellationToken))
            .Select(t => t.ToString())
            .OrderBy(t => t)
            .ToList();

        return new ClassroomFilterOptionsDto
        {
            Faculties = faculties,
            Buildings = buildings,
            ClassroomTypes = classroomTypes
        };
    }

    public async Task<List<AvailableClassroomResultDto>> SearchAvailableClassroomsAsync(
        int termId,
        List<int> days,
        int startHour,
        int endHour,
        int? minCapacity = null,
        string? classroomType = null,
        int? facultyId = null,
        string? buildingName = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await BuildRuntimeSnapshotAsync(termId, null, null, cancellationToken);

        var classroomQuery = _context.Classrooms.AsNoTracking().AsQueryable();

        if (facultyId.HasValue)
        {
            var assignedIds = await _context.ClassroomTermAssignments
                .AsNoTracking()
                .Where(a => a.TermId == termId && a.FacultyId == facultyId.Value)
                .Select(a => a.ClassroomId)
                .ToListAsync(cancellationToken);
            classroomQuery = classroomQuery.Where(c => assignedIds.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(buildingName))
            classroomQuery = classroomQuery.Where(c => c.BuildingName == buildingName);

        if (!string.IsNullOrWhiteSpace(classroomType) && Enum.TryParse<ClassroomType>(classroomType, out var parsedType))
            classroomQuery = classroomQuery.Where(c => c.Type == parsedType);

        if (minCapacity.HasValue)
            classroomQuery = classroomQuery.Where(c => c.Capacity >= minCapacity.Value);

        var classrooms = await classroomQuery.ToListAsync(cancellationToken);

        var occupiedSlots = snapshot.Sections
            .Where(s => s.PrimaryClassroomId.HasValue)
            .SelectMany(s => s.Blocks.Select(tb => new
            {
                ClassroomId = s.PrimaryClassroomId!.Value,
                Day = tb.Day,
                StartHour = tb.StartTime.Hour,
                EndHour = tb.EndTime.Minute > 0 ? tb.EndTime.Hour : tb.EndTime.Hour - 1
            }))
            .GroupBy(x => x.ClassroomId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var weeklyUsage = occupiedSlots.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Sum(s => s.EndHour - s.StartHour + 1));

        var results = new List<AvailableClassroomResultDto>();

        foreach (var classroom in classrooms)
        {
            var slots = occupiedSlots.TryGetValue(classroom.Id, out var s) ? s : new();

            bool isAvailable = true;
            foreach (var day in days)
            {
                for (int h = startHour; h <= endHour && isAvailable; h++)
                {
                    if (slots.Any(sl => sl.Day == day && h >= sl.StartHour && h <= sl.EndHour))
                    {
                        isAvailable = false;
                    }
                }
            }

            if (!isAvailable) continue;

            int totalWeeklySlots = 5 * 10;
            int used = weeklyUsage.TryGetValue(classroom.Id, out var u) ? u : 0;

            var classroomSchedules = snapshot.Sections
                .Where(sec => sec.PrimaryClassroomId == classroom.Id)
                .SelectMany(sec => sec.Blocks.Select(tb => new ClassroomScheduleSlotDto
                {
                    Day = tb.Day,
                    StartHour = tb.StartTime.Hour,
                    EndHour = tb.EndTime.Minute > 0 ? tb.EndTime.Hour : tb.EndTime.Hour - 1,
                    CourseCode = sec.CourseCode,
                    CourseName = sec.CourseName,
                    InstructorName = sec.InstructorName
                }))
                .OrderBy(sl => sl.Day).ThenBy(sl => sl.StartHour)
                .ToList();

            results.Add(new AvailableClassroomResultDto
            {
                Id = classroom.Id,
                Name = classroom.Name,
                Capacity = classroom.Capacity,
                Type = classroom.Type.ToString(),
                BuildingName = classroom.BuildingName,
                Floor = classroom.Floor,
                WeeklyOccupancyRate = totalWeeklySlots > 0 ? Math.Round((double)used / totalWeeklySlots * 100) : 0,
                Schedules = classroomSchedules
            });
        }

        return results.OrderBy(r => r.Name).ToList();
    }

}
