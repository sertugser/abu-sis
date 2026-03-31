using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class SchedulingService : ISchedulingService
{
    private static readonly JsonSerializerOptions ScheduleJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _context;

    public SchedulingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SchedulingConflictDto>> GetConflictsAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateExistingAsync(termId, cancellationToken);
        return validation.Conflicts;
    }

    public async Task<List<SchedulingConflictDto>> GetConflictsByDepartmentAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateExistingByDepartmentAsync(departmentId, termId, cancellationToken);
        return validation.Conflicts;
    }

    public async Task<ScheduleValidationResultDto> ValidateAsync(ScheduleValidationRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };

        if (request.SectionId <= 0)
        {
            result.IsValid = false;
            result.Notes.Add("Geçerli bir section ID gönderilmelidir.");
            return result;
        }

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            result.IsValid = false;
            result.Notes.Add("Section bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(section.TermId, section.Id, null, cancellationToken);
        var candidate = await BuildCandidateSnapshotAsync(section, request.InstructorId, request.ClassroomId, request.ScheduleSlots, cancellationToken);

        result.Notes.Add($"Degerlendirilen donem: {snapshot.TermName}");
        result.Notes.AddRange(candidate.Notes);

        var conflicts = EvaluateCandidateConflicts(candidate, snapshot);
        result.Conflicts = conflicts;
        result.IsValid = conflicts.Count == 0;
        return result;
    }

    public async Task<ScheduleValidationResultDto> ValidateExistingAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };
        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);

        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
        var evaluation = EvaluateExistingConflicts(snapshot);

        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        return evaluation;
    }

    public async Task<ScheduleValidationResultDto> ValidateExistingByDepartmentAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };
        if (departmentId <= 0)
        {
            result.IsValid = false;
            result.Notes.Add("Geçerli bir bölüm ID gönderilmelidir.");
            return result;
        }

        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);
        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, departmentId, cancellationToken);
        var evaluation = EvaluateExistingConflicts(snapshot);
        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        evaluation.Notes.Insert(1, $"Kapsam: {snapshot.ScopeName}");
        return evaluation;
    }

    public async Task<ScheduleSuggestionDto> SuggestAsync(ScheduleSuggestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = new ScheduleSuggestionDto
        {
            IsFeasible = false,
            SectionId = request.SectionId
        };

        if (request.SectionId <= 0)
        {
            response.Notes.Add("Geçerli bir section ID gönderilmelidir.");
            return response;
        }

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            response.Notes.Add("Section bulunamadı.");
            return response;
        }

        var instructorId = request.InstructorId ?? section.InstructorId;
        if (!instructorId.HasValue)
        {
            response.Notes.Add("Öneri üretilebilmesi için section üzerinde bir öğretim üyesi tanımlı olmalı veya istek ile instructorId gönderilmelidir.");
            return response;
        }

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instructorId.Value, cancellationToken);

        if (instructor == null)
        {
            response.Notes.Add("Öğretim üyesi bulunamadı.");
            return response;
        }

        var instructorName = await GetInstructorNameAsync(instructor, cancellationToken);

        // Load TimeSlots for this term
        var timeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == section.TermId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);

        response.InstructorId = instructor.Id;
        response.InstructorName = instructorName;

        if (timeSlots.Count == 0)
        {
            response.Notes.Add("Bu dönem için zaman slotu tanımı bulunmadığından öneri üretilemedi.");
            return response;
        }

        var existingSnapshot = await BuildTermSnapshotAsync(section.TermId, section.Id, null, cancellationToken);
        var currentParse = ParseScheduleJson(section.ScheduleJson);
        var requiredHours = ResolveTotalRequiredHours(section);
        var slotCombinations = BuildTimeSlotCombinations(requiredHours, currentParse.Slots, timeSlots);

        if (slotCombinations.Count == 0)
        {
            response.Notes.Add("Zaman grid'inde uygun slot kombinasyonu üretilemedi.");
            return response;
        }

        var requiredCapacity = Math.Max(section.Capacity, 0);
        var classrooms = await _context.Classrooms
            .AsNoTracking()
            .Where(c => c.Capacity > 0 && c.Capacity >= requiredCapacity)
            .OrderBy(c => c.Capacity - requiredCapacity)
            .ThenBy(c => c.Id == section.ClassroomId ? 0 : 1)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        if (classrooms.Count == 0)
        {
            response.Notes.Add("Section kontenjanını karşılayan uygun bir derslik bulunamadı.");
            return response;
        }

        var currentWeeklyHours = ResolveWeeklyHours(currentParse.Slots, section.Course);

        foreach (var slotCombination in slotCombinations)
        {
            foreach (var classroom in classrooms)
            {
                var candidate = BuildCandidateSnapshot(
                    section,
                    instructor,
                    instructorName,
                    classroom,
                    slotCombination.Slots,
                    new List<string>());

                var conflicts = EvaluateCandidateConflicts(candidate, existingSnapshot);
                if (conflicts.Count > 0)
                {
                    continue;
                }

                var candidateWeeklyHours = ResolveWeeklyHours(candidate.ScheduleSlots, section.Course);
                response.IsFeasible = true;
                response.SuggestedClassroomId = classroom.Id;
                response.SuggestedClassroomName = classroom.Name;
                response.SuggestedSlots = candidate.ScheduleSlots.Select(MapScheduleSlot).ToList();
                response.EstimatedWeeklyHoursImpact = candidateWeeklyHours - currentWeeklyHours;
                response.Notes.AddRange(candidate.Notes);
                response.Notes.Add("Slotlar zaman grid'inden uygun kombinasyon olarak seçildi.");
                response.Notes.Add($"Derslik secimi kapasite uyumu esas alinarak yapildi ({classroom.Capacity}/{requiredCapacity}).");
                return response;
            }
        }

        response.Notes.Add("Çakışma olmayan derslik + slot kombinasyonu bulunamadı.");
        return response;
    }

    public async Task<ScheduleValidationResultDto> ApplyAsync(ApplyScheduleRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationRequest = new ScheduleValidationRequestDto
        {
            SectionId = request.SectionId,
            ClassroomId = request.ClassroomId,
            ScheduleSlots = request.ScheduleSlots,
        };

        var validation = await ValidateAsync(validationRequest, cancellationToken);

        if (!validation.IsValid)
        {
            validation.Notes.Add("Çakışma tespit edildiğinden program uygulanmadı.");
            return validation;
        }

        var section = await _context.CourseSections
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            return new ScheduleValidationResultDto
            {
                IsValid = false,
                Notes = { "Section bulunamadı." }
            };
        }

        var slots = request.ScheduleSlots.Select(s => new ScheduleSlot
        {
            Day = s.Day,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
        }).ToList();

        // Keep existing ScheduleJson behavior
        section.ScheduleJson = JsonSerializer.Serialize(slots, ScheduleJsonOptions);
        section.ClassroomId = request.ClassroomId;

        // Also write SectionSchedule records for the new TimeSlot-based system
        await WriteSectionScheduleRecordsAsync(section.Id, section.TermId, request.ClassroomId, slots, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        validation.Notes.Add("Program başarıyla uygulanarak kaydedildi.");
        return validation;
    }

    public async Task<BatchAutoScheduleResultDto> BatchAutoScheduleAsync(BatchAutoScheduleRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = new BatchAutoScheduleResultDto();

        var resolvedTerm = await ResolveTermAsync(request.TermId, cancellationToken);
        if (resolvedTerm == null)
        {
            result.Notes.Add(request.TermId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        result.Notes.Add($"Donem: {resolvedTerm.Name}");

        var sectionsQuery = _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == resolvedTerm.Id && cs.IsActive && cs.InstructorId != null);

        if (request.DepartmentId.HasValue)
        {
            sectionsQuery = sectionsQuery.Where(cs => cs.Course.DepartmentId == request.DepartmentId.Value);
        }

        var sections = await sectionsQuery.ToListAsync(cancellationToken);

        var unscheduled = sections
            .Where(cs => string.IsNullOrWhiteSpace(cs.ScheduleJson) || cs.ScheduleJson == "[]")
            .OrderByDescending(cs => cs.EnrolledCount)
            .ToList();

        result.TotalSections = unscheduled.Count;

        if (unscheduled.Count == 0)
        {
            result.Notes.Add("Programsız section bulunamadı, tüm section'lar zaten programlı.");
            return result;
        }

        result.Notes.Add($"{unscheduled.Count} programsiz section bulundu.");

        foreach (var section in unscheduled)
        {
            var item = new BatchAutoScheduleItemDto
            {
                SectionId = section.Id,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name,
            };

            var suggestRequest = new ScheduleSuggestionRequestDto
            {
                SectionId = section.Id,
                InstructorId = section.InstructorId,
            };

            var suggestion = await SuggestAsync(suggestRequest, cancellationToken);

            if (!suggestion.IsFeasible || suggestion.SuggestedSlots.Count == 0)
            {
                item.Success = false;
                item.FailureReason = suggestion.Notes.Count > 0
                    ? string.Join(" ", suggestion.Notes)
                    : "Uygun slot kombinasyonu bulunamadı.";
                result.FailedCount++;
                result.Results.Add(item);
                continue;
            }

            var applyRequest = new ApplyScheduleRequestDto
            {
                SectionId = section.Id,
                ClassroomId = suggestion.SuggestedClassroomId,
                ScheduleSlots = suggestion.SuggestedSlots,
            };

            var applyResult = await ApplyAsync(applyRequest, cancellationToken);

            if (applyResult.IsValid)
            {
                item.Success = true;
                item.ClassroomName = suggestion.SuggestedClassroomName;
                item.Slots = suggestion.SuggestedSlots;
                result.ScheduledCount++;
            }
            else
            {
                item.Success = false;
                item.FailureReason = applyResult.Conflicts.Count > 0
                    ? applyResult.Conflicts[0].Message
                    : "Uygulama sırasında çakışma tespit edildi.";
                result.FailedCount++;
            }

            result.Results.Add(item);
        }

        return result;
    }

    public async Task<ClassroomDashboardDto> GetClassroomDashboardAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ClassroomDashboardDto();
        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);
        if (resolvedTerm == null)
        {
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
        var evaluation = EvaluateExistingConflicts(snapshot);
        result.ConflictsCount = evaluation.Conflicts?.Count ?? 0;

        var classrooms = await _context.Classrooms.AsNoTracking().ToListAsync(cancellationToken);
        result.TotalClassrooms = classrooms.Count;

        // Her derslik için o donemdeki dersleri haritala
        var classroomSchedules = snapshot.Sections
            .Where(s => s.ClassroomId.HasValue)
            .GroupBy(s => s.ClassroomId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(s => s.ScheduleSlots.Select(slot => new ClassroomScheduleSlotDto
                {
                    Day = (int)slot.Day,
                    StartHour = slot.StartTime.Hour,
                    EndHour = slot.EndTime.Minute > 0 ? slot.EndTime.Hour : slot.EndTime.Hour - 1,
                    CourseCode = s.CourseCode,
                    CourseName = s.CourseName,
                    InstructorName = s.InstructorName
                })).ToList()
            );

        result.Classrooms = classrooms.Select(c => new ClassroomInfoDto
        {
            Id = c.Id,
            Name = c.Name,
            Capacity = c.Capacity,
            Type = c.Type.ToString(),
            Schedules = classroomSchedules.TryGetValue(c.Id, out var schedules) ? schedules : new()
        }).OrderBy(c => c.Name).ToList();

        var heatmapMap = new Dictionary<(int day, int hour), int>();
        for (int day = 0; day < 5; day++)
        {
            for (int hour = 8; hour <= 17; hour++)
            {
                heatmapMap[(day, hour)] = 0;
            }
        }

        var occupationMap = new Dictionary<(int day, int hour), List<ClassroomOccupationDto>>();
        foreach (var section in snapshot.Sections)
        {
            if (section.ClassroomId.HasValue)
            {
                foreach (var slot in section.ScheduleSlots)
                {
                    int dayIndex = (int)slot.Day - 1;
                    if (dayIndex >= 0 && dayIndex <= 4)
                    {
                        int startHour = slot.StartTime.Hour;
                        int endHour = slot.EndTime.Minute > 0 ? slot.EndTime.Hour : slot.EndTime.Hour - 1;

                        for (int h = startHour; h <= endHour && h <= 17; h++)
                        {
                            if (h >= 8)
                            {
                                var key = (dayIndex, h);
                                if (!heatmapMap.ContainsKey(key)) heatmapMap[key] = 0;
                                heatmapMap[key]++;

                                if (!occupationMap.ContainsKey(key)) occupationMap[key] = new List<ClassroomOccupationDto>();
                                occupationMap[key].Add(new ClassroomOccupationDto
                                {
                                    ClassroomName = section.ClassroomName,
                                    CourseCode = section.CourseCode,
                                    CourseName = section.CourseName,
                                    EnrolledCount = section.EnrolledCount,
                                    Capacity = section.ClassroomCapacity
                                });
                            }
                        }
                    }
                }
            }
        }

        int totalSlots = result.TotalClassrooms * 5 * 10;
        int usedSlots = heatmapMap.Values.Sum();
        result.EmptySlots = Math.Max(0, totalSlots - usedSlots);
        result.OccupancyRate = totalSlots > 0 ? Math.Round((double)usedSlots / totalSlots * 100) : 0;

        foreach (var kvp in heatmapMap)
        {
            var day = kvp.Key.day;
            var hour = kvp.Key.hour;
            var used = kvp.Value;
            var total = result.TotalClassrooms;

            result.Heatmap.Add(new ClassroomHeatmapCellDto
            {
                Day = day,
                Hour = hour,
                UsedClassrooms = used,
                Occupancy = total > 0 ? Math.Min(1, (double)used / total) : 0,
                Details = $"{GetDayName(day)} {hour:D2}:00 — {used}/{total} derslik dolu",
                Occupations = occupationMap.TryGetValue((day, hour), out var occList) ? occList : new()
            });
        }

        return result;
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private async Task<TermSnapshot> BuildTermSnapshotAsync(int termId, int? excludedSectionId, int? departmentId, CancellationToken cancellationToken)
    {
        var sectionsQuery = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == termId && cs.IsActive)
            .Where(cs => !excludedSectionId.HasValue || cs.Id != excludedSectionId.Value);

        if (departmentId.HasValue)
        {
            sectionsQuery = sectionsQuery.Where(cs =>
                cs.Course.DepartmentId == departmentId.Value
                || (cs.Instructor != null && cs.Instructor.DepartmentId == departmentId.Value));
        }

        var sections = await sectionsQuery.ToListAsync(cancellationToken);

        var termName = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.Id == termId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var scopeName = departmentId.HasValue
            ? await _context.Departments
                .AsNoTracking()
                .Where(department => department.Id == departmentId.Value)
                .Select(department => department.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "Bolum"
            : "Tum sistem";

        var instructorIds = sections
            .Where(section => section.InstructorId.HasValue)
            .Select(section => section.InstructorId!.Value)
            .Distinct()
            .ToList();

        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, cancellationToken);

        var snapshots = sections
            .Select(section => BuildExistingSectionSnapshot(section, instructorNameMap))
            .ToList();

        return new TermSnapshot
        {
            TermId = termId,
            TermName = termName,
            ScopeName = scopeName,
            Sections = snapshots
        };
    }

    private static SectionSnapshot BuildExistingSectionSnapshot(
        CourseSection section,
        IReadOnlyDictionary<int, string> instructorNameMap)
    {
        var parseResult = ParseScheduleJson(section.ScheduleJson);
        var instructorId = section.InstructorId;

        return new SectionSnapshot
        {
            SectionId = section.Id,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TermId = section.TermId,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            Capacity = section.Capacity,
            ClassroomId = section.ClassroomId,
            ClassroomName = section.Classroom?.Name ?? string.Empty,
            ClassroomCapacity = section.Classroom?.Capacity ?? 0,
            InstructorId = instructorId,
            InstructorName = instructorId.HasValue && instructorNameMap.TryGetValue(instructorId.Value, out var instructorName)
                ? instructorName
                : string.Empty,
            InstructorMaxWeeklyHours = section.Instructor?.MaxWeeklyHours ?? 0,
            ScheduleSlots = parseResult.Slots,
            ParseError = parseResult.ErrorMessage,
            Notes = new List<string>()
        };
    }

    private async Task<SectionSnapshot> BuildCandidateSnapshotAsync(
        CourseSection section,
        int? instructorIdOverride,
        int? classroomIdOverride,
        List<ScheduleSlotDto>? scheduleSlotOverrides,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var instructorId = instructorIdOverride ?? section.InstructorId;
        var classroomId = classroomIdOverride ?? section.ClassroomId;

        Instructor? instructor = null;
        string instructorName = string.Empty;

        if (instructorId.HasValue)
        {
            instructor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == instructorId.Value, cancellationToken);

            if (instructor == null)
            {
                notes.Add("Seçilen öğretim üyesi bulunamadı.");
            }
            else
            {
                instructorName = await GetInstructorNameAsync(instructor, cancellationToken);
            }
        }
        else
        {
            notes.Add("Section için öğretim üyesi atanmamış.");
        }

        Classroom? classroom = null;
        if (classroomId.HasValue)
        {
            classroom = await _context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classroomId.Value, cancellationToken);

            if (classroom == null)
            {
                notes.Add("Seçilen derslik bulunamadı.");
            }
        }
        else
        {
            notes.Add("Section için derslik atanmamış.");
        }

        var scheduleSlots = new List<ScheduleSlot>();
        string? parseError = null;

        if (scheduleSlotOverrides != null)
        {
            var overrideValidation = ValidateScheduleSlots(scheduleSlotOverrides
                .Select(dto => new ScheduleSlot
                {
                    Day = dto.Day,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime
                })
                .ToList());

            scheduleSlots = overrideValidation.Slots;
            parseError = overrideValidation.ErrorMessage;
        }
        else
        {
            var parseResult = ParseScheduleJson(section.ScheduleJson);
            scheduleSlots = parseResult.Slots;
            parseError = parseResult.ErrorMessage;
        }

        if (scheduleSlots.Count == 0 && parseError == null)
        {
            notes.Add("Section için program slot tanımı bulunmuyor.");
        }

        return BuildCandidateSnapshot(
            section,
            instructor,
            instructorName,
            classroom,
            scheduleSlots,
            notes,
            parseError);
    }

    private static SectionSnapshot BuildCandidateSnapshot(
        CourseSection section,
        Instructor? instructor,
        string instructorName,
        Classroom? classroom,
        IReadOnlyList<ScheduleSlot> scheduleSlots,
        List<string> notes,
        string? parseError = null)
    {
        return new SectionSnapshot
        {
            SectionId = section.Id,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TermId = section.TermId,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            ClassroomId = classroom?.Id,
            ClassroomName = classroom?.Name ?? string.Empty,
            ClassroomCapacity = classroom?.Capacity ?? 0,
            InstructorId = instructor?.Id,
            InstructorName = instructorName,
            InstructorMaxWeeklyHours = instructor?.MaxWeeklyHours ?? 0,
            ScheduleSlots = scheduleSlots.ToList(),
            ParseError = parseError,
            Notes = notes
        };
    }

    private ScheduleValidationResultDto EvaluateExistingConflicts(TermSnapshot snapshot)
    {
        var conflicts = new List<SchedulingConflictDto>();
        var notes = new List<string>();

        foreach (var section in snapshot.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.ParseError))
            {
                conflicts.Add(BuildConflict(
                    section,
                    ConflictTypes.InvalidScheduleJson,
                    section.ParseError,
                    null,
                    null));
            }

            if (section.ClassroomId.HasValue && section.ClassroomCapacity < section.Capacity)
            {
                conflicts.Add(BuildConflict(
                    section,
                    ConflictTypes.CapacityConflict,
                    $"Derslik kapasitesi section kontenjanini karsilamiyor ({section.ClassroomCapacity}/{section.Capacity}).",
                    null,
                    null));
            }
        }

        for (var i = 0; i < snapshot.Sections.Count; i++)
        {
            for (var j = i + 1; j < snapshot.Sections.Count; j++)
            {
                var first = snapshot.Sections[i];
                var second = snapshot.Sections[j];

                foreach (var overlap in GetOverlapSlots(first.ScheduleSlots, second.ScheduleSlots))
                {
                    if (first.ClassroomId.HasValue && first.ClassroomId == second.ClassroomId)
                    {
                        conflicts.Add(BuildConflict(
                            first,
                            ConflictTypes.ClassroomConflict,
                            $"Derslik '{first.ClassroomName}' aynı zaman aralığında '{second.CourseCode}' section'u ile çakışıyor.",
                            overlap,
                            second.SectionId));

                        conflicts.Add(BuildConflict(
                            second,
                            ConflictTypes.ClassroomConflict,
                            $"Derslik '{second.ClassroomName}' aynı zaman aralığında '{first.CourseCode}' section'u ile çakışıyor.",
                            overlap,
                            first.SectionId));
                    }

                    if (first.InstructorId.HasValue && first.InstructorId == second.InstructorId)
                    {
                        conflicts.Add(BuildConflict(
                            first,
                            ConflictTypes.InstructorConflict,
                            $"Öğretim üyesi '{first.InstructorName}' aynı zaman aralığında '{second.CourseCode}' section'u ile çakışıyor.",
                            overlap,
                            second.SectionId));

                        conflicts.Add(BuildConflict(
                            second,
                            ConflictTypes.InstructorConflict,
                            $"Öğretim üyesi '{second.InstructorName}' aynı zaman aralığında '{first.CourseCode}' section'u ile çakışıyor.",
                            overlap,
                            first.SectionId));
                    }
                }
            }
        }

        foreach (var group in snapshot.Sections
                     .Where(section => section.InstructorId.HasValue)
                     .GroupBy(section => section.InstructorId!.Value))
        {
            var firstSection = group.First();
            if (firstSection.InstructorMaxWeeklyHours <= 0)
            {
                notes.Add($"Instructor haftalik yuk limiti tanimsiz veya sifir: {firstSection.InstructorName}.");
                continue;
            }

            var totalWeeklyHours = group.Sum(section => ResolveWeeklyHours(section.ScheduleSlots, section));
            if (totalWeeklyHours <= firstSection.InstructorMaxWeeklyHours)
            {
                continue;
            }

            foreach (var section in group)
            {
                conflicts.Add(BuildConflict(
                    section,
                    ConflictTypes.WeeklyLoadConflict,
                    $"Instructor haftalik ders yuku limiti asiliyor ({totalWeeklyHours:0.##}/{firstSection.InstructorMaxWeeklyHours} saat).",
                    null,
                    null));
            }
        }

        return new ScheduleValidationResultDto
        {
            IsValid = conflicts.Count == 0,
            Conflicts = conflicts
                .OrderBy(conflict => conflict.SectionId)
                .ThenBy(conflict => conflict.ConflictType)
                .ThenBy(conflict => conflict.Day)
                .ThenBy(conflict => conflict.StartTime)
                .ToList(),
            Notes = notes.Distinct().OrderBy(note => note).ToList()
        };
    }

    private List<SchedulingConflictDto> EvaluateCandidateConflicts(SectionSnapshot candidate, TermSnapshot snapshot)
    {
        var conflicts = new List<SchedulingConflictDto>();

        if (!string.IsNullOrWhiteSpace(candidate.ParseError))
        {
            conflicts.Add(BuildConflict(
                candidate,
                ConflictTypes.InvalidScheduleJson,
                candidate.ParseError,
                null,
                null));

            return conflicts;
        }

        if (candidate.ClassroomId.HasValue && candidate.ClassroomCapacity < candidate.Capacity)
        {
            conflicts.Add(BuildConflict(
                candidate,
                ConflictTypes.CapacityConflict,
                $"Derslik kapasitesi section kontenjanini karsilamiyor ({candidate.ClassroomCapacity}/{candidate.Capacity}).",
                null,
                null));
        }

        foreach (var existing in snapshot.Sections)
        {
            foreach (var overlap in GetOverlapSlots(candidate.ScheduleSlots, existing.ScheduleSlots))
            {
                if (candidate.ClassroomId.HasValue && candidate.ClassroomId == existing.ClassroomId)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.ClassroomConflict,
                        $"Derslik '{candidate.ClassroomName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
                        overlap,
                        existing.SectionId));
                }

                if (candidate.InstructorId.HasValue && candidate.InstructorId == existing.InstructorId)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.InstructorConflict,
                        $"Öğretim üyesi '{candidate.InstructorName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
                        overlap,
                        existing.SectionId));
                }
            }
        }

        if (candidate.InstructorId.HasValue && candidate.InstructorMaxWeeklyHours > 0)
        {
            var totalWeeklyHours = snapshot.Sections
                .Where(section => section.InstructorId == candidate.InstructorId)
                .Sum(section => ResolveWeeklyHours(section.ScheduleSlots, section))
                + ResolveWeeklyHours(candidate.ScheduleSlots, candidate);

            if (totalWeeklyHours > candidate.InstructorMaxWeeklyHours)
            {
                conflicts.Add(BuildConflict(
                    candidate,
                    ConflictTypes.WeeklyLoadConflict,
                    $"Instructor haftalik ders yuku limiti asiliyor ({totalWeeklyHours:0.##}/{candidate.InstructorMaxWeeklyHours} saat).",
                    null,
                    null));
            }
        }

        return conflicts
            .OrderBy(conflict => conflict.ConflictType)
            .ThenBy(conflict => conflict.Day)
            .ThenBy(conflict => conflict.StartTime)
            .ThenBy(conflict => conflict.RelatedSectionId)
            .ToList();
    }

    // ── TimeSlot-based suggestion helpers ────────────────────────────────

    /// <summary>
    /// Determines total required hours for a course section (TheoryHours + LabHours).
    /// </summary>
    private static int ResolveTotalRequiredHours(CourseSection section)
    {
        var total = section.Course.TheoryHours + section.Course.LabHours;
        return total > 0 ? total : 1;
    }

    /// <summary>
    /// Resolves how the required hours should be split across day blocks.
    /// Returns a list of consecutive-slot counts per day block:
    ///   1 hour -> [1]
    ///   2 hours -> [2]
    ///   3 hours -> [3]
    ///   4 hours -> [2, 2]
    ///   5 hours -> [3, 2]
    ///   6+ hours -> [3, 3] (then more blocks if needed)
    /// </summary>
    private static IReadOnlyList<int> ResolveDayBlocks(int totalHours)
    {
        if (totalHours <= 3)
        {
            return new[] { totalHours };
        }

        var blocks = new List<int>();
        var remaining = totalHours;

        while (remaining > 0)
        {
            if (remaining >= 6)
            {
                blocks.Add(3);
                remaining -= 3;
            }
            else if (remaining >= 4)
            {
                // 4 -> [2,2], 5 -> [3,2]
                var first = remaining == 4 ? 2 : 3;
                blocks.Add(first);
                remaining -= first;
            }
            else
            {
                // remaining is 1, 2, or 3
                blocks.Add(remaining);
                remaining = 0;
            }
        }

        return blocks;
    }

    /// <summary>
    /// Groups TimeSlots by day and finds all possible consecutive-slot windows of a given size.
    /// A consecutive window means slots whose SlotOrder values are sequential (e.g., 1,2,3).
    /// </summary>
    private static List<List<TimeSlot>> FindConsecutiveWindows(IReadOnlyList<TimeSlot> daySlots, int requiredConsecutive)
    {
        var windows = new List<List<TimeSlot>>();
        if (daySlots.Count < requiredConsecutive)
        {
            return windows;
        }

        var sorted = daySlots.OrderBy(ts => ts.SlotOrder).ToList();

        for (int i = 0; i <= sorted.Count - requiredConsecutive; i++)
        {
            var window = new List<TimeSlot>();
            bool valid = true;

            for (int j = 0; j < requiredConsecutive; j++)
            {
                if (j > 0 && sorted[i + j].SlotOrder != sorted[i + j - 1].SlotOrder + 1)
                {
                    valid = false;
                    break;
                }
                window.Add(sorted[i + j]);
            }

            if (valid)
            {
                windows.Add(window);
            }
        }

        return windows;
    }

    /// <summary>
    /// Converts a list of consecutive TimeSlots into a single ScheduleSlot covering them.
    /// </summary>
    private static ScheduleSlot TimeSlotWindowToScheduleSlot(IReadOnlyList<TimeSlot> window)
    {
        return new ScheduleSlot
        {
            Day = window[0].DayOfWeek,
            StartTime = window[0].StartTime,
            EndTime = window[^1].EndTime
        };
    }

    /// <summary>
    /// Builds slot combinations from the TimeSlot grid for the given required hours.
    /// If currentSlots exist and are valid, they are included as the first (preferred) combination.
    /// </summary>
    private static List<RankedSlotCombination> BuildTimeSlotCombinations(
        int totalRequiredHours,
        IReadOnlyList<ScheduleSlot> currentSlots,
        IReadOnlyList<TimeSlot> timeSlots)
    {
        var combinations = new List<RankedSlotCombination>();
        var signatures = new HashSet<string>(StringComparer.Ordinal);

        // Include the current schedule as first option if valid
        if (currentSlots.Count > 0)
        {
            var currentValidation = ValidateScheduleSlots(currentSlots.ToList());
            if (currentValidation.ErrorMessage == null)
            {
                AddCombination(combinations, signatures, currentValidation.Slots, 0);
            }
        }

        var dayBlocks = ResolveDayBlocks(totalRequiredHours);

        // Group time slots by day
        var slotsByDay = timeSlots
            .GroupBy(ts => ts.DayOfWeek)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TimeSlot>)g.OrderBy(ts => ts.SlotOrder).ToList());

        // For each day block size, find all possible consecutive windows across all days
        var perBlockCandidates = dayBlocks
            .Select(blockSize =>
            {
                var candidates = new List<RankedSlot>();
                foreach (var kvp in slotsByDay)
                {
                    var windows = FindConsecutiveWindows(kvp.Value, blockSize);
                    foreach (var window in windows)
                    {
                        candidates.Add(new RankedSlot
                        {
                            Slot = TimeSlotWindowToScheduleSlot(window),
                            PreferencePenalty = 0
                        });
                    }
                }
                return candidates
                    .OrderBy(c => c.Slot.Day)
                    .ThenBy(c => c.Slot.StartTime)
                    .ToList();
            })
            .ToList();

        if (perBlockCandidates.Any(candidateList => candidateList.Count == 0))
        {
            return combinations;
        }

        // Build combinations recursively, ensuring no overlap and different days for multi-block
        var current = new List<RankedSlot>();
        BuildCombinationsRecursive(perBlockCandidates, 0, current, combinations, signatures);

        return combinations
            .OrderBy(combination => combination.PreferenceScore)
            .ThenBy(combination => BuildSlotSignature(combination.Slots))
            .ToList();
    }

    // ── SectionSchedule write helper ─────────────────────────────────────

    /// <summary>
    /// Writes SectionSchedule records alongside ScheduleJson.
    /// Deletes existing records for this section first, then creates new ones
    /// by matching each ScheduleSlot to TimeSlots in the term.
    /// </summary>
    private async Task WriteSectionScheduleRecordsAsync(
        int sectionId,
        int termId,
        int? classroomId,
        IReadOnlyList<ScheduleSlot> slots,
        CancellationToken cancellationToken)
    {
        // Remove old SectionSchedule records for this section
        var existing = await _context.SectionSchedules
            .Where(ss => ss.CourseSectionId == sectionId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.SectionSchedules.RemoveRange(existing);
        }

        if (!classroomId.HasValue || slots.Count == 0)
        {
            return;
        }

        // Load term time slots for matching
        var termTimeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .ToListAsync(cancellationToken);

        foreach (var slot in slots)
        {
            // Find all TimeSlots that overlap with this ScheduleSlot on the same day
            var matchingTimeSlots = termTimeSlots
                .Where(ts => ts.DayOfWeek == slot.Day
                    && ts.StartTime < slot.EndTime
                    && slot.StartTime < ts.EndTime)
                .OrderBy(ts => ts.SlotOrder)
                .ToList();

            foreach (var ts in matchingTimeSlots)
            {
                _context.SectionSchedules.Add(new SectionSchedule
                {
                    CourseSectionId = sectionId,
                    TimeSlotId = ts.Id,
                    ClassroomId = classroomId.Value
                });
            }
        }
    }

    // ── Shared slot helpers ──────────────────────────────────────────────

    private static void BuildCombinationsRecursive(
        IReadOnlyList<List<RankedSlot>> perBlockCandidates,
        int index,
        List<RankedSlot> current,
        List<RankedSlotCombination> results,
        HashSet<string> signatures)
    {
        if (results.Count >= 200)
        {
            return;
        }

        if (index == perBlockCandidates.Count)
        {
            AddCombination(results, signatures, current.Select(slot => slot.Slot).ToList(), current.Sum(slot => slot.PreferencePenalty));
            return;
        }

        foreach (var candidate in perBlockCandidates[index])
        {
            if (current.Any(existing => SlotsOverlap(existing.Slot, candidate.Slot)))
            {
                continue;
            }

            current.Add(candidate);
            BuildCombinationsRecursive(perBlockCandidates, index + 1, current, results, signatures);
            current.RemoveAt(current.Count - 1);
        }
    }

    private static void AddCombination(
        List<RankedSlotCombination> combinations,
        HashSet<string> signatures,
        List<ScheduleSlot> slots,
        int preferenceScore)
    {
        var normalizedSlots = slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .Select(slot => new ScheduleSlot
            {
                Day = slot.Day,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            })
            .ToList();

        var signature = BuildSlotSignature(normalizedSlots);
        if (!signatures.Add(signature))
        {
            return;
        }

        combinations.Add(new RankedSlotCombination
        {
            Slots = normalizedSlots,
            PreferenceScore = preferenceScore
        });
    }

    private static ScheduleParseResult ParseScheduleJson(string? scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson) || scheduleJson.Trim() == "[]")
        {
            return new ScheduleParseResult();
        }

        try
        {
            var rawSlots = JsonSerializer.Deserialize<List<ScheduleSlotJsonModel>>(scheduleJson, ScheduleJsonOptions);
            if (rawSlots == null || rawSlots.Count == 0)
            {
                return new ScheduleParseResult();
            }

            var slots = new List<ScheduleSlot>();
            foreach (var rawSlot in rawSlots)
            {
                var day = rawSlot.Day ?? rawSlot.DayOfWeek;
                if (!day.HasValue)
                {
                    return new ScheduleParseResult
                    {
                        ErrorMessage = "Program JSON içindeki bir slotta gün bilgisi eksik."
                    };
                }

                slots.Add(new ScheduleSlot
                {
                    Day = day.Value,
                    StartTime = rawSlot.StartTime,
                    EndTime = rawSlot.EndTime
                });
            }

            return ValidateScheduleSlots(slots);
        }
        catch (JsonException)
        {
            return new ScheduleParseResult
            {
                ErrorMessage = "Program JSON güvenli şekilde ayrıştırılamadı."
            };
        }
    }

    private static ScheduleParseResult ValidateScheduleSlots(List<ScheduleSlot> slots)
    {
        var normalizedSlots = slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .ToList();

        foreach (var slot in normalizedSlots)
        {
            if (slot.EndTime <= slot.StartTime)
            {
                return new ScheduleParseResult
                {
                    ErrorMessage = "Slot bitiş saati başlangıç saatinden sonra olmalıdır."
                };
            }
        }

        for (var i = 0; i < normalizedSlots.Count; i++)
        {
            for (var j = i + 1; j < normalizedSlots.Count; j++)
            {
                if (SlotsOverlap(normalizedSlots[i], normalizedSlots[j]))
                {
                    return new ScheduleParseResult
                    {
                        ErrorMessage = "Aynı section içindeki slotlar birbiriyle çakışamaz."
                    };
                }
            }
        }

        return new ScheduleParseResult
        {
            Slots = normalizedSlots
        };
    }

    private static List<ScheduleSlot> GetOverlapSlots(IReadOnlyList<ScheduleSlot> firstSchedule, IReadOnlyList<ScheduleSlot> secondSchedule)
    {
        var overlaps = new List<ScheduleSlot>();

        foreach (var first in firstSchedule)
        {
            foreach (var second in secondSchedule)
            {
                if (!TryGetOverlap(first, second, out var overlap))
                {
                    continue;
                }

                overlaps.Add(overlap);
            }
        }

        return overlaps
            .GroupBy(slot => BuildSlotSignature(new[] { slot }))
            .Select(group => group.First())
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .ToList();
    }

    private static bool TryGetOverlap(ScheduleSlot first, ScheduleSlot second, out ScheduleSlot overlap)
    {
        overlap = default!;

        if (first.Day != second.Day)
        {
            return false;
        }

        var start = first.StartTime > second.StartTime ? first.StartTime : second.StartTime;
        var end = first.EndTime < second.EndTime ? first.EndTime : second.EndTime;

        if (start >= end)
        {
            return false;
        }

        overlap = new ScheduleSlot
        {
            Day = first.Day,
            StartTime = start,
            EndTime = end
        };

        return true;
    }

    private static bool SlotsOverlap(ScheduleSlot first, ScheduleSlot second) =>
        first.Day == second.Day
        && first.StartTime < second.EndTime
        && second.StartTime < first.EndTime;

    private static decimal ResolveWeeklyHours(IReadOnlyList<ScheduleSlot> slots, Course course)
    {
        if (slots.Count > 0)
        {
            var minutes = slots.Sum(slot => (decimal)(slot.EndTime - slot.StartTime).TotalMinutes);
            return minutes / 60m;
        }

        var catalogHours = course.TheoryHours + course.LabHours;
        return catalogHours > 0 ? catalogHours : 0m;
    }

    private static decimal ResolveWeeklyHours(IReadOnlyList<ScheduleSlot> slots, SectionSnapshot section)
    {
        if (slots.Count > 0)
        {
            var minutes = slots.Sum(slot => (decimal)(slot.EndTime - slot.StartTime).TotalMinutes);
            return minutes / 60m;
        }

        var catalogHours = section.TheoryHours + section.LabHours;
        return catalogHours > 0 ? catalogHours : 0m;
    }

    private static ScheduleSlotDto MapScheduleSlot(ScheduleSlot slot) =>
        new()
        {
            Day = slot.Day,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime
        };

    private static SchedulingConflictDto BuildConflict(
        SectionSnapshot section,
        string conflictType,
        string message,
        ScheduleSlot? slot,
        int? relatedSectionId)
    {
        return new SchedulingConflictDto
        {
            ConflictType = conflictType,
            SectionId = section.SectionId,
            RelatedSectionId = relatedSectionId,
            CourseCode = section.CourseCode,
            CourseName = section.CourseName,
            InstructorId = section.InstructorId,
            InstructorName = section.InstructorName,
            ClassroomId = section.ClassroomId,
            ClassroomName = section.ClassroomName,
            Day = slot?.Day,
            StartTime = slot?.StartTime,
            EndTime = slot?.EndTime,
            Message = message
        };
    }

    private static string BuildSlotSignature(IEnumerable<ScheduleSlot> slots) =>
        string.Join("|", slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .Select(slot => $"{(int)slot.Day}:{slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}"));

    private async Task<AcademicTerm?> ResolveTermAsync(int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue)
        {
            return await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(term => term.Id == termId.Value, cancellationToken);
        }

        return await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(term => term.IsActive, cancellationToken);
    }

    private async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(List<int> instructorIds, CancellationToken cancellationToken)
    {
        if (instructorIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _context.Instructors
            .AsNoTracking()
            .Where(instructor => instructorIds.Contains(instructor.Id))
            .Join(
                _context.Users.AsNoTracking(),
                instructor => instructor.UserId,
                user => user.Id,
                (instructor, user) => new
                {
                    instructor.Id,
                    FullName = $"{instructor.Title} {user.FirstName} {user.LastName}".Trim()
                })
            .ToDictionaryAsync(item => item.Id, item => item.FullName, cancellationToken);
    }

    private async Task<string> GetInstructorNameAsync(Instructor instructor, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == instructor.UserId, cancellationToken);

        return user == null
            ? instructor.Title
            : $"{instructor.Title} {user.FirstName} {user.LastName}".Trim();
    }

    private static string GetDayName(int day)
    {
        return day switch
        {
            0 => "Pazartesi",
            1 => "Salı",
            2 => "Çarşamba",
            3 => "Perşembe",
            4 => "Cuma",
            _ => "Bilinmeyen"
        };
    }

    // ── Constants & inner types ──────────────────────────────────────────

    private static class ConflictTypes
    {
        public const string ClassroomConflict = "ClassroomConflict";
        public const string InstructorConflict = "InstructorConflict";
        public const string CapacityConflict = "CapacityConflict";
        public const string WeeklyLoadConflict = "WeeklyLoadConflict";
        public const string InvalidScheduleJson = "InvalidScheduleJson";
    }

    private sealed class TermSnapshot
    {
        public int TermId { get; init; }
        public string TermName { get; init; } = string.Empty;
        public string ScopeName { get; init; } = string.Empty;
        public List<SectionSnapshot> Sections { get; init; } = new();
    }

    private sealed class SectionSnapshot
    {
        public int SectionId { get; init; }
        public int CourseId { get; init; }
        public string CourseCode { get; init; } = string.Empty;
        public string CourseName { get; init; } = string.Empty;
        public int TermId { get; init; }
        public int TheoryHours { get; init; }
        public int LabHours { get; init; }
        public int Capacity { get; init; }
        public int EnrolledCount { get; init; }
        public int? ClassroomId { get; init; }
        public string ClassroomName { get; init; } = string.Empty;
        public int ClassroomCapacity { get; init; }
        public int? InstructorId { get; init; }
        public string InstructorName { get; init; } = string.Empty;
        public int InstructorMaxWeeklyHours { get; init; }
        public List<ScheduleSlot> ScheduleSlots { get; init; } = new();
        public string? ParseError { get; init; }
        public List<string> Notes { get; init; } = new();
    }

    private sealed class ScheduleParseResult
    {
        public List<ScheduleSlot> Slots { get; init; } = new();
        public string? ErrorMessage { get; init; }
    }

    private sealed class RankedSlot
    {
        public ScheduleSlot Slot { get; init; } = null!;
        public int PreferencePenalty { get; init; }
    }

    private sealed class RankedSlotCombination
    {
        public List<ScheduleSlot> Slots { get; init; } = new();
        public int PreferenceScore { get; init; }
    }

    private sealed class ScheduleSlotJsonModel
    {
        public DayOfWeek? Day { get; init; }
        public DayOfWeek? DayOfWeek { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
    }
}
