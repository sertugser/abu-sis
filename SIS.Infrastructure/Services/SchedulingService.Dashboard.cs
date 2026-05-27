using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService — derslik kullanım dashboard'u.
/// GetClassroomDashboardAsync: dönem geneli derslik doluluk + filtre özeti.
/// UI'da "Derslik Yönetimi" sayfasında tablo ve istatistikler için.
/// </summary>
public partial class SchedulingService
{
    public async Task<ClassroomDashboardDto> GetClassroomDashboardAsync(
        int? termId = null,
        int? facultyId = null,
        string? buildingName = null,
        string? classroomType = null,
        int? minCapacity = null,
        int? maxCapacity = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ClassroomDashboardDto();
        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);
        if (resolvedTerm == null)
        {
            return result;
        }

        var snapshot = await BuildRuntimeSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);
        result.ConflictsCount = evaluation.Conflicts?.Count ?? 0;

        // Fakulte filtresi: ClassroomTermAssignment uzerinden
        var classroomIdsForTerm = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(a => a.TermId == resolvedTerm.Id)
            .Where(a => !facultyId.HasValue || a.FacultyId == facultyId.Value)
            .Select(a => new { a.ClassroomId, a.FacultyId, FacultyName = a.Faculty.Name })
            .ToListAsync(cancellationToken);

        var facultyLookup = classroomIdsForTerm
            .GroupBy(x => x.ClassroomId)
            .ToDictionary(g => g.Key, g => g.First());

        var classroomQuery = _context.Classrooms.AsNoTracking().AsQueryable();

        // Term-scope: rapor her zaman döneme atanmış dersliklerle sınırlı (fakülte
        // seçilmese bile). Önceki davranışta fakülte seçilmeyince DB'deki tüm
        // derslikler sayılıyordu → döneme atanmamışlar paydayı şişiriyordu.
        var assignedIds = classroomIdsForTerm.Select(x => x.ClassroomId).ToHashSet();
        classroomQuery = classroomQuery.Where(c => assignedIds.Contains(c.Id));

        if (!string.IsNullOrWhiteSpace(buildingName))
            classroomQuery = classroomQuery.Where(c => c.BuildingName == buildingName);

        if (!string.IsNullOrWhiteSpace(classroomType) && Enum.TryParse<ClassroomType>(classroomType, out var parsedType))
            classroomQuery = classroomQuery.Where(c => c.Type == parsedType);

        if (minCapacity.HasValue)
            classroomQuery = classroomQuery.Where(c => c.Capacity >= minCapacity.Value);

        if (maxCapacity.HasValue)
            classroomQuery = classroomQuery.Where(c => c.Capacity <= maxCapacity.Value);

        var classrooms = await classroomQuery.ToListAsync(cancellationToken);
        result.TotalClassrooms = classrooms.Count;

        // Haftalık doluluk oranını tek formülle (slot grid) hesaplamak için.
        var slotGrid = await LoadTermSlotGridAsync(resolvedTerm.Id, cancellationToken);

        // Her derslik için o donemdeki dersleri haritala
        var classroomSchedules = snapshot.Sections
            .Where(s => s.PrimaryClassroomId.HasValue)
            .GroupBy(s => s.PrimaryClassroomId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(s => s.Blocks.Select(slot => new ClassroomScheduleSlotDto
                {
                    Day = slot.Day,
                    StartHour = slot.StartTime.Hour,
                    EndHour = slot.EndTime.Minute > 0 ? slot.EndTime.Hour : slot.EndTime.Hour - 1,
                    StartTime = slot.StartTime.ToString("HH:mm"),
                    EndTime = slot.EndTime.ToString("HH:mm"),
                    CourseCode = s.CourseCode,
                    CourseName = s.CourseName,
                    InstructorName = s.InstructorName
                })).ToList()
            );

        // Doluluk için ham blok pencereleri (gün + gerçek saatler).
        var blockTuplesByClassroom = snapshot.Sections
            .Where(s => s.PrimaryClassroomId.HasValue)
            .GroupBy(s => s.PrimaryClassroomId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(s => s.Blocks.Select(b => (Day: b.Day, b.StartTime, b.EndTime))).ToList());

        result.Classrooms = classrooms.Select(c =>
        {
            var fac = facultyLookup.TryGetValue(c.Id, out var f) ? f : null;
            var blocks = blockTuplesByClassroom.TryGetValue(c.Id, out var bt)
                ? bt
                : new List<(int Day, TimeOnly StartTime, TimeOnly EndTime)>();
            return new ClassroomInfoDto
            {
                Id = c.Id,
                Name = c.Name,
                Capacity = c.Capacity,
                Type = c.Type.ToString(),
                BuildingName = c.BuildingName,
                Floor = c.Floor,
                FacultyId = fac?.FacultyId ?? 0,
                FacultyName = fac?.FacultyName ?? string.Empty,
                WeeklyOccupancyRate = ComputeWeeklyOccupancyRate(blocks, slotGrid),
                Schedules = classroomSchedules.TryGetValue(c.Id, out var schedules) ? schedules : new()
            };
        }).OrderBy(c => c.Name).ToList();

        var filteredClassroomIds = classrooms.Select(c => c.Id).ToHashSet();

        // TimeSlot'lardan heatmap grid'i oluştur
        var timeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == resolvedTerm.Id)
            .OrderBy(ts => ts.DayOfWeek).ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);

        var distinctSlots = timeSlots
            .GroupBy(ts => ts.SlotOrder)
            .Select(g => g.First())
            .OrderBy(ts => ts.SlotOrder)
            .ToList();

        // Heatmap hücreleri GERÇEK timeslot'lardan kurulur (kartezyen workdays×slots DEĞİL) → günler
        // arası değişken slot sayısında payda doğru olur. Yalnız hafta içi (Pzt-Cum). TimeSlot yoksa
        // fabrikasyon YOK: heatmap boş kalır, occupancy 0 → gerçek "slot tanımlı değil" durumu yansır
        // (eski 8-17 uydurması olmayan veriyi varmış gibi gösteriyordu).
        var heatmapMap = new Dictionary<(int day, int slotOrder), int>();
        foreach (var ts in timeSlots)
        {
            var dayIdx = (int)ts.DayOfWeek - 1;
            if (dayIdx < 0 || dayIdx > 4) continue;
            heatmapMap[(dayIdx, ts.SlotOrder)] = 0;
        }
        var gridCellCount = heatmapMap.Count;

        var timeSlotsPerDay = timeSlots
            .GroupBy(ts => (int)ts.DayOfWeek - 1)
            .ToDictionary(g => g.Key, g => g.OrderBy(ts => ts.SlotOrder).ToList());

        var occupationMap = new Dictionary<(int day, int slotOrder), List<ClassroomOccupationDto>>();
        foreach (var section in snapshot.Sections)
        {
            if (section.PrimaryClassroomId.HasValue && filteredClassroomIds.Contains(section.PrimaryClassroomId.Value))
            {
                foreach (var block in section.Blocks)
                {
                    int dayIndex = block.Day - 1;
                    if (dayIndex >= 0 && dayIndex <= 4)
                    {
                        if (timeSlotsPerDay.TryGetValue(dayIndex, out var daySlots))
                        {
                            foreach (var ts in daySlots)
                            {
                                if (block.StartTime < ts.EndTime && ts.StartTime < block.EndTime)
                                {
                                    var key = (dayIndex, ts.SlotOrder);
                                    if (!heatmapMap.ContainsKey(key)) heatmapMap[key] = 0;
                                    heatmapMap[key]++;

                                    if (!occupationMap.ContainsKey(key)) occupationMap[key] = new List<ClassroomOccupationDto>();
                                    occupationMap[key].Add(new ClassroomOccupationDto
                                    {
                                        ClassroomName = section.PrimaryClassroomName,
                                        CourseCode = section.CourseCode,
                                        CourseName = section.CourseName,
                                        EnrolledCount = section.EnrolledCount,
                                        Capacity = section.PrimaryClassroomCapacity
                                    });
                                }
                            }
                        }
                        else
                        {
                            // Fallback: TimeSlot yoksa saat bazlı eşleştir
                            int startHour = block.StartTime.Hour;
                            int endHour = block.EndTime.Minute > 0 ? block.EndTime.Hour : block.EndTime.Hour - 1;
                            foreach (var s in distinctSlots)
                            {
                                if (startHour < s.EndTime.Hour + 1 && s.StartTime.Hour <= endHour)
                                {
                                    var key = (dayIndex, s.SlotOrder);
                                    if (!heatmapMap.ContainsKey(key)) heatmapMap[key] = 0;
                                    heatmapMap[key]++;

                                    if (!occupationMap.ContainsKey(key)) occupationMap[key] = new List<ClassroomOccupationDto>();
                                    occupationMap[key].Add(new ClassroomOccupationDto
                                    {
                                        ClassroomName = section.PrimaryClassroomName,
                                        CourseCode = section.CourseCode,
                                        CourseName = section.CourseName,
                                        EnrolledCount = section.EnrolledCount,
                                        Capacity = section.PrimaryClassroomCapacity
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        int slotsPerDay = distinctSlots.Count;
        // Payda = derslik × GERÇEK hücre sayısı (günler arası değişken slot sayısını da yansıtır;
        // eski totalDays × slotsPerDay her günün tüm slotlara sahip olduğunu varsayıp şişiriyordu).
        int totalSlots = result.TotalClassrooms * gridCellCount;
        int usedSlots = heatmapMap.Values.Sum();
        result.EmptySlots = Math.Max(0, totalSlots - usedSlots);
        result.OccupancyRate = totalSlots > 0 ? Math.Round((double)usedSlots / totalSlots * 100) : 0;
        result.SlotsPerDay = slotsPerDay;

        var slotLookup = distinctSlots.ToDictionary(s => s.SlotOrder);
        foreach (var kvp in heatmapMap)
        {
            var day = kvp.Key.day;
            var slotOrder = kvp.Key.slotOrder;
            var used = kvp.Value;
            var total = result.TotalClassrooms;
            var slotDef = slotLookup[slotOrder];

            result.Heatmap.Add(new ClassroomHeatmapCellDto
            {
                Day = day,
                Hour = slotDef.StartTime.Hour,
                SlotOrder = slotOrder,
                StartTime = slotDef.StartTime.ToString("HH:mm"),
                EndTime = slotDef.EndTime.ToString("HH:mm"),
                Label = $"{slotDef.StartTime:HH:mm}-{slotDef.EndTime:HH:mm}",
                UsedClassrooms = used,
                Occupancy = total > 0 ? Math.Min(1, (double)used / total) : 0,
                Details = $"{GetDayName(day)} {slotDef.StartTime:HH:mm}-{slotDef.EndTime:HH:mm} — {used}/{total} derslik dolu",
                Occupations = occupationMap.TryGetValue((day, slotOrder), out var occList) ? occList : new()
            });
        }

        return result;
    }

}
