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

        var slotGrid = await LoadTermSlotGridAsync(termId, cancellationToken);

        return new ClassroomFilterOptionsDto
        {
            Faculties = faculties,
            Buildings = buildings,
            ClassroomTypes = classroomTypes,
            Slots = ProjectSlotSummaries(slotGrid)
        };
    }

    public async Task<List<AvailableClassroomResultDto>> SearchAvailableClassroomsAsync(
        int termId,
        List<int> days,
        TimeOnly startTime,
        TimeOnly endTime,
        int? minCapacity = null,
        string? classroomType = null,
        int? facultyId = null,
        string? buildingName = null,
        CancellationToken cancellationToken = default)
    {
        // Savunma: geçersiz aralık/gün → boş sonuç (controller da doğrular).
        if (days.Count == 0 || endTime <= startTime)
            return new List<AvailableClassroomResultDto>();

        var snapshot = await BuildRuntimeSnapshotAsync(termId, null, null, cancellationToken);
        var slotGrid = await LoadTermSlotGridAsync(termId, cancellationToken);

        var classroomQuery = _context.Classrooms.AsNoTracking().AsQueryable();

        // Term-scope: derslikler her zaman döneme atanmış olanlarla sınırlı; fakülte
        // seçilirse o fakülteye daralır. (Önceki davranışta fakülte seçilmeyince DB'deki
        // tüm derslikler dönüyordu — döneme atanmamışlar dahil.)
        var assignmentQuery = _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(a => a.TermId == termId);
        if (facultyId.HasValue)
            assignmentQuery = assignmentQuery.Where(a => a.FacultyId == facultyId.Value);

        var assignedIds = await assignmentQuery
            .Select(a => a.ClassroomId)
            .Distinct()
            .ToListAsync(cancellationToken);
        classroomQuery = classroomQuery.Where(c => assignedIds.Contains(c.Id));

        if (!string.IsNullOrWhiteSpace(buildingName))
            classroomQuery = classroomQuery.Where(c => c.BuildingName == buildingName);

        if (!string.IsNullOrWhiteSpace(classroomType) && Enum.TryParse<ClassroomType>(classroomType, out var parsedType))
            classroomQuery = classroomQuery.Where(c => c.Type == parsedType);

        if (minCapacity.HasValue)
            classroomQuery = classroomQuery.Where(c => c.Capacity >= minCapacity.Value);

        var classrooms = await classroomQuery.ToListAsync(cancellationToken);

        // Derslik başına bloklar (gün + gerçek başlangıç/bitiş saatleri).
        var blocksByClassroom = snapshot.Sections
            .Where(s => s.PrimaryClassroomId.HasValue)
            .SelectMany(s => s.Blocks.Select(tb => new
            {
                ClassroomId = s.PrimaryClassroomId!.Value,
                Section = s,
                Block = tb
            }))
            .GroupBy(x => x.ClassroomId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<AvailableClassroomResultDto>();

        foreach (var classroom in classrooms)
        {
            var entries = blocksByClassroom.TryGetValue(classroom.Id, out var e) ? e : new();

            // Interval-overlap müsaitlik: seçilen günlerde [startTime, endTime) ile
            // çakışan blok yoksa derslik müsait. (Eski tam-saat döngüsü 50 dk slotlarda
            // hatalı sonuç veriyordu; "Bitiş Saati dahil" off-by-one'ı da burada kalkar.)
            var isAvailable = !entries.Any(x =>
                days.Contains((int)x.Block.Day)
                && x.Block.StartTime < endTime
                && startTime < x.Block.EndTime);

            if (!isAvailable) continue;

            var occupancy = ComputeWeeklyOccupancyRate(
                entries.Select(x => ((int)x.Block.Day, x.Block.StartTime, x.Block.EndTime)),
                slotGrid);

            var classroomSchedules = entries
                .Select(x => new ClassroomScheduleSlotDto
                {
                    Day = (int)x.Block.Day,
                    StartHour = x.Block.StartTime.Hour,
                    EndHour = x.Block.EndTime.Minute > 0 ? x.Block.EndTime.Hour : x.Block.EndTime.Hour - 1,
                    StartTime = x.Block.StartTime.ToString("HH:mm"),
                    EndTime = x.Block.EndTime.ToString("HH:mm"),
                    CourseCode = x.Section.CourseCode,
                    CourseName = x.Section.CourseName,
                    InstructorName = x.Section.InstructorName
                })
                .OrderBy(sl => sl.Day).ThenBy(sl => sl.StartTime)
                .ToList();

            results.Add(new AvailableClassroomResultDto
            {
                Id = classroom.Id,
                Name = classroom.Name,
                Capacity = classroom.Capacity,
                Type = classroom.Type.ToString(),
                BuildingName = classroom.BuildingName,
                Floor = classroom.Floor,
                WeeklyOccupancyRate = occupancy,
                Schedules = classroomSchedules
            });
        }

        return results.OrderBy(r => r.Name).ToList();
    }

}
