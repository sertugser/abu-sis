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

    private static readonly TimeSpan SuggestionStep = TimeSpan.FromMinutes(30);

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
            result.Notes.Add("Gecerli bir sectionId gonderilmelidir.");
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
            result.Notes.Add("Section bulunamadi.");
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
                ? "Belirtilen akademik donem bulunamadi."
                : "Aktif akademik donem bulunamadi.");
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
            result.Notes.Add("Gecerli bir departmentId gonderilmelidir.");
            return result;
        }

        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);
        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik donem bulunamadi."
                : "Aktif akademik donem bulunamadi.");
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
            response.Notes.Add("Gecerli bir sectionId gonderilmelidir.");
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
            response.Notes.Add("Section bulunamadi.");
            return response;
        }

        var instructorId = request.InstructorId ?? section.InstructorId;
        if (!instructorId.HasValue)
        {
            response.Notes.Add("Oneri uretilebilmesi icin section uzerinde bir ogretim uyesi tanimli olmali veya request ile instructorId gonderilmelidir.");
            return response;
        }

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instructorId.Value, cancellationToken);

        if (instructor == null)
        {
            response.Notes.Add("Ogretim uyesi bulunamadi.");
            return response;
        }

        var instructorName = await GetInstructorNameAsync(instructor, cancellationToken);
        var availabilities = await _context.InstructorAvailabilities
            .AsNoTracking()
            .Where(ia => ia.InstructorId == instructor.Id)
            .OrderByDescending(ia => ia.IsPreferred)
            .ThenBy(ia => ia.DayOfWeek)
            .ThenBy(ia => ia.StartTime)
            .ToListAsync(cancellationToken);

        response.InstructorId = instructor.Id;
        response.InstructorName = instructorName;
        response.InstructorAvailabilities = availabilities
            .Select(MapAvailability)
            .ToList();

        if (availabilities.Count == 0)
        {
            response.Notes.Add("Instructor availability kaydi olmadigi icin availability disinda oneride bulunulamadi.");
            return response;
        }

        var existingSnapshot = await BuildTermSnapshotAsync(section.TermId, section.Id, null, cancellationToken);
        var currentParse = ParseScheduleJson(section.ScheduleJson);
        var durationMinutes = ResolveDesiredSlotDurations(section, currentParse.Slots);
        var slotCombinations = BuildSuggestedSlotCombinations(durationMinutes, currentParse.Slots, availabilities);

        if (slotCombinations.Count == 0)
        {
            response.Notes.Add("Instructor availability icinde uygun slot kombinasyonu uretilemedi.");
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
            response.Notes.Add("Section kontenjanini karsilayan uygun bir derslik bulunamadi.");
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
                    availabilities,
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
                response.Notes.Add(slotCombination.PreferenceScore == 0
                    ? "Slotlar preferred availability pencereleriyle uyumludur."
                    : "Slotlar available availability pencereleri icinden secildi.");
                response.Notes.Add($"Derslik secimi kapasite uyumu esas alinarak yapildi ({classroom.Capacity}/{requiredCapacity}).");
                return response;
            }
        }

        response.Notes.Add("Conflict olmayan classroom + slot kombinasyonu bulunamadi.");
        return response;
    }

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
        var availabilityMap = await LoadAvailabilityMapAsync(instructorIds, cancellationToken);

        var snapshots = sections
            .Select(section => BuildExistingSectionSnapshot(section, instructorNameMap, availabilityMap))
            .ToList();

        return new TermSnapshot
        {
            TermId = termId,
            TermName = termName,
            ScopeName = scopeName,
            Sections = snapshots
        };
    }

    private SectionSnapshot BuildExistingSectionSnapshot(
        CourseSection section,
        IReadOnlyDictionary<int, string> instructorNameMap,
        IReadOnlyDictionary<int, List<InstructorAvailability>> availabilityMap)
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
            AvailabilityRules = instructorId.HasValue && availabilityMap.TryGetValue(instructorId.Value, out var rules)
                ? rules
                : new List<InstructorAvailability>(),
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
        List<InstructorAvailability> availabilities = new();

        if (instructorId.HasValue)
        {
            instructor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == instructorId.Value, cancellationToken);

            if (instructor == null)
            {
                notes.Add("Secilen ogretim uyesi bulunamadi.");
            }
            else
            {
                instructorName = await GetInstructorNameAsync(instructor, cancellationToken);
                availabilities = await _context.InstructorAvailabilities
                    .AsNoTracking()
                    .Where(ia => ia.InstructorId == instructor.Id)
                    .ToListAsync(cancellationToken);

                if (availabilities.Count == 0)
                {
                    notes.Add("Bu ogretim uyesi icin availability tanimi bulunmuyor; availability conflict kontrolu bilgilendirme notu ile surduruluyor.");
                }
            }
        }
        else
        {
            notes.Add("Section icin ogretim uyesi atanmamis.");
        }

        Classroom? classroom = null;
        if (classroomId.HasValue)
        {
            classroom = await _context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classroomId.Value, cancellationToken);

            if (classroom == null)
            {
                notes.Add("Secilen derslik bulunamadi.");
            }
        }
        else
        {
            notes.Add("Section icin derslik atanmamis.");
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
            notes.Add("Section icin schedule slot tanimi bulunmuyor.");
        }

        return BuildCandidateSnapshot(
            section,
            instructor,
            instructorName,
            classroom,
            availabilities,
            scheduleSlots,
            notes,
            parseError);
    }

    private static SectionSnapshot BuildCandidateSnapshot(
        CourseSection section,
        Instructor? instructor,
        string instructorName,
        Classroom? classroom,
        List<InstructorAvailability> availabilities,
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
            AvailabilityRules = availabilities,
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

            if (section.InstructorId.HasValue)
            {
                if (section.AvailabilityRules.Count == 0)
                {
                    notes.Add($"Instructor availability tanimi yok: {section.InstructorName} (Section {section.SectionId}).");
                }
                else
                {
                    foreach (var slot in section.ScheduleSlots)
                    {
                        var availability = EvaluateAvailability(section.AvailabilityRules, slot);
                        if (availability.HasData && !availability.IsWithinAvailability)
                        {
                            conflicts.Add(BuildConflict(
                                section,
                                ConflictTypes.InstructorAvailabilityConflict,
                                availability.Message,
                                slot,
                                null));
                        }
                    }
                }
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
                            $"Derslik '{first.ClassroomName}' ayni zaman araliginda '{second.CourseCode}' sectionu ile cakisiyor.",
                            overlap,
                            second.SectionId));

                        conflicts.Add(BuildConflict(
                            second,
                            ConflictTypes.ClassroomConflict,
                            $"Derslik '{second.ClassroomName}' ayni zaman araliginda '{first.CourseCode}' sectionu ile cakisiyor.",
                            overlap,
                            first.SectionId));
                    }

                    if (first.InstructorId.HasValue && first.InstructorId == second.InstructorId)
                    {
                        conflicts.Add(BuildConflict(
                            first,
                            ConflictTypes.InstructorConflict,
                            $"Ogretim uyesi '{first.InstructorName}' ayni zaman araliginda '{second.CourseCode}' sectionu ile cakisiyor.",
                            overlap,
                            second.SectionId));

                        conflicts.Add(BuildConflict(
                            second,
                            ConflictTypes.InstructorConflict,
                            $"Ogretim uyesi '{second.InstructorName}' ayni zaman araliginda '{first.CourseCode}' sectionu ile cakisiyor.",
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

        if (candidate.InstructorId.HasValue && candidate.AvailabilityRules.Count > 0)
        {
            foreach (var slot in candidate.ScheduleSlots)
            {
                var availability = EvaluateAvailability(candidate.AvailabilityRules, slot);
                if (availability.HasData && !availability.IsWithinAvailability)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.InstructorAvailabilityConflict,
                        availability.Message,
                        slot,
                        null));
                }
            }
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
                        $"Derslik '{candidate.ClassroomName}' ayni zaman araliginda '{existing.CourseCode}' sectionu ile cakisiyor.",
                        overlap,
                        existing.SectionId));
                }

                if (candidate.InstructorId.HasValue && candidate.InstructorId == existing.InstructorId)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.InstructorConflict,
                        $"Ogretim uyesi '{candidate.InstructorName}' ayni zaman araliginda '{existing.CourseCode}' sectionu ile cakisiyor.",
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

    private static List<RankedSlotCombination> BuildSuggestedSlotCombinations(
        IReadOnlyList<int> durationMinutes,
        IReadOnlyList<ScheduleSlot> currentSlots,
        IReadOnlyList<InstructorAvailability> availabilities)
    {
        var combinations = new List<RankedSlotCombination>();
        var signatures = new HashSet<string>(StringComparer.Ordinal);

        if (currentSlots.Count > 0)
        {
            var currentValidation = ValidateScheduleSlots(currentSlots.ToList());
            if (currentValidation.ErrorMessage == null)
            {
                var currentPreference = currentValidation.Slots
                    .Select(slot => EvaluateAvailability(availabilities, slot))
                    .All(check => check.HasData && check.IsWithinAvailability)
                    ? currentValidation.Slots.Count(slot => !EvaluateAvailability(availabilities, slot).IsPreferred)
                    : int.MaxValue;

                AddCombination(combinations, signatures, currentValidation.Slots, currentPreference);
            }
        }

        var perDurationCandidates = durationMinutes
            .Select(duration => BuildRankedSlotsForDuration(duration, availabilities))
            .ToList();

        if (perDurationCandidates.Any(candidateList => candidateList.Count == 0))
        {
            return combinations;
        }

        var current = new List<RankedSlot>();
        BuildCombinationsRecursive(perDurationCandidates, 0, current, combinations, signatures);

        return combinations
            .Where(combination => combination.PreferenceScore < int.MaxValue)
            .OrderBy(combination => combination.PreferenceScore)
            .ThenBy(combination => BuildSlotSignature(combination.Slots))
            .ToList();
    }

    private static void BuildCombinationsRecursive(
        IReadOnlyList<List<RankedSlot>> perDurationCandidates,
        int index,
        List<RankedSlot> current,
        List<RankedSlotCombination> results,
        HashSet<string> signatures)
    {
        if (results.Count >= 200)
        {
            return;
        }

        if (index == perDurationCandidates.Count)
        {
            AddCombination(results, signatures, current.Select(slot => slot.Slot).ToList(), current.Sum(slot => slot.PreferencePenalty));
            return;
        }

        foreach (var candidate in perDurationCandidates[index])
        {
            if (current.Any(existing => SlotsOverlap(existing.Slot, candidate.Slot)))
            {
                continue;
            }

            current.Add(candidate);
            BuildCombinationsRecursive(perDurationCandidates, index + 1, current, results, signatures);
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

    private static List<RankedSlot> BuildRankedSlotsForDuration(int durationMinutes, IReadOnlyList<InstructorAvailability> availabilities)
    {
        var rankedSlots = new List<RankedSlot>();

        foreach (var availability in availabilities
                     .Where(availability => availability.IsAvailable)
                     .OrderByDescending(availability => availability.IsPreferred)
                     .ThenBy(availability => availability.DayOfWeek)
                     .ThenBy(availability => availability.StartTime))
        {
            var latestStart = availability.EndTime.Add(-TimeSpan.FromMinutes(durationMinutes));
            if (latestStart < availability.StartTime)
            {
                continue;
            }

            for (var start = availability.StartTime; start <= latestStart; start = start.Add(SuggestionStep))
            {
                var candidateSlot = new ScheduleSlot
                {
                    Day = availability.DayOfWeek,
                    StartTime = start,
                    EndTime = start.Add(TimeSpan.FromMinutes(durationMinutes))
                };

                var availabilityCheck = EvaluateAvailability(availabilities, candidateSlot);
                if (!availabilityCheck.HasData || !availabilityCheck.IsWithinAvailability)
                {
                    continue;
                }

                rankedSlots.Add(new RankedSlot
                {
                    Slot = candidateSlot,
                    PreferencePenalty = availabilityCheck.IsPreferred ? 0 : 1
                });
            }
        }

        return rankedSlots
            .GroupBy(candidate => BuildSlotSignature(new[] { candidate.Slot }))
            .Select(group => group.OrderBy(candidate => candidate.PreferencePenalty).First())
            .OrderBy(candidate => candidate.PreferencePenalty)
            .ThenBy(candidate => candidate.Slot.Day)
            .ThenBy(candidate => candidate.Slot.StartTime)
            .ToList();
    }

    private static IReadOnlyList<int> ResolveDesiredSlotDurations(CourseSection section, IReadOnlyList<ScheduleSlot> currentSlots)
    {
        if (currentSlots.Count > 0)
        {
            return currentSlots
                .OrderBy(slot => slot.Day)
                .ThenBy(slot => slot.StartTime)
                .Select(slot => (int)(slot.EndTime - slot.StartTime).TotalMinutes)
                .Where(duration => duration > 0)
                .ToList();
        }

        var durations = new List<int>();

        if (section.Course.TheoryHours > 0)
        {
            durations.Add(section.Course.TheoryHours * 60);
        }

        if (section.Course.LabHours > 0)
        {
            durations.Add(section.Course.LabHours * 60);
        }

        if (durations.Count == 0)
        {
            durations.Add(60);
        }

        return durations;
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
                        ErrorMessage = "ScheduleJson icindeki bir slotta gun bilgisi eksik."
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
                ErrorMessage = "ScheduleJson guvenli sekilde parse edilemedi."
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
                    ErrorMessage = "Schedule slot bitis saati baslangic saatinden sonra olmalidir."
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
                        ErrorMessage = "Ayni section icindeki schedule slotlari birbiriyle cakisamaz."
                    };
                }
            }
        }

        return new ScheduleParseResult
        {
            Slots = normalizedSlots
        };
    }

    private static AvailabilityCheckResult EvaluateAvailability(IReadOnlyList<InstructorAvailability> availabilities, ScheduleSlot slot)
    {
        if (availabilities.Count == 0)
        {
            return new AvailabilityCheckResult
            {
                HasData = false,
                IsWithinAvailability = true,
                IsPreferred = false,
                Message = "Instructor availability tanimi bulunmuyor."
            };
        }

        var sameDay = availabilities
            .Where(availability => availability.DayOfWeek == slot.Day)
            .ToList();

        if (sameDay.Count == 0)
        {
            return new AvailabilityCheckResult
            {
                HasData = true,
                IsWithinAvailability = false,
                IsPreferred = false,
                Message = "Ogretim uyesi bu gun icin available degil."
            };
        }

        if (sameDay.Any(availability => !availability.IsAvailable && SlotOverlapsAvailability(slot, availability)))
        {
            return new AvailabilityCheckResult
            {
                HasData = true,
                IsWithinAvailability = false,
                IsPreferred = false,
                Message = "Ogretim uyesi bu saat araliginda uygun degil."
            };
        }

        var coveringWindows = sameDay
            .Where(availability => availability.IsAvailable && SlotContainedByAvailability(slot, availability))
            .ToList();

        if (coveringWindows.Count == 0)
        {
            return new AvailabilityCheckResult
            {
                HasData = true,
                IsWithinAvailability = false,
                IsPreferred = false,
                Message = "Schedule slot instructor availability penceresi disinda kaliyor."
            };
        }

        return new AvailabilityCheckResult
        {
            HasData = true,
            IsWithinAvailability = true,
            IsPreferred = coveringWindows.Any(window => window.IsPreferred),
            Message = string.Empty
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

    private static bool SlotOverlapsAvailability(ScheduleSlot slot, InstructorAvailability availability) =>
        slot.Day == availability.DayOfWeek
        && slot.StartTime < availability.EndTime
        && availability.StartTime < slot.EndTime;

    private static bool SlotContainedByAvailability(ScheduleSlot slot, InstructorAvailability availability) =>
        slot.Day == availability.DayOfWeek
        && slot.StartTime >= availability.StartTime
        && slot.EndTime <= availability.EndTime;

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

    private static InstructorAvailabilityDto MapAvailability(InstructorAvailability availability) =>
        new()
        {
            Id = availability.Id,
            InstructorId = availability.InstructorId,
            DayOfWeek = availability.DayOfWeek,
            StartTime = availability.StartTime,
            EndTime = availability.EndTime,
            IsAvailable = availability.IsAvailable,
            IsPreferred = availability.IsPreferred
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

    private async Task<Dictionary<int, List<InstructorAvailability>>> LoadAvailabilityMapAsync(List<int> instructorIds, CancellationToken cancellationToken)
    {
        if (instructorIds.Count == 0)
        {
            return new Dictionary<int, List<InstructorAvailability>>();
        }

        var availabilities = await _context.InstructorAvailabilities
            .AsNoTracking()
            .Where(availability => instructorIds.Contains(availability.InstructorId))
            .ToListAsync(cancellationToken);

        return availabilities
            .GroupBy(availability => availability.InstructorId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(availability => availability.IsPreferred)
                    .ThenBy(availability => availability.DayOfWeek)
                    .ThenBy(availability => availability.StartTime)
                    .ToList());
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

    private static class ConflictTypes
    {
        public const string ClassroomConflict = "ClassroomConflict";
        public const string InstructorConflict = "InstructorConflict";
        public const string CapacityConflict = "CapacityConflict";
        public const string InstructorAvailabilityConflict = "InstructorAvailabilityConflict";
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
        public List<InstructorAvailability> AvailabilityRules { get; init; } = new();
        public List<string> Notes { get; init; } = new();
    }

    private sealed class ScheduleParseResult
    {
        public List<ScheduleSlot> Slots { get; init; } = new();
        public string? ErrorMessage { get; init; }
    }

    private sealed class AvailabilityCheckResult
    {
        public bool HasData { get; init; }
        public bool IsWithinAvailability { get; init; }
        public bool IsPreferred { get; init; }
        public string Message { get; init; } = string.Empty;
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
            validation.Notes.Add("Conflict tespit edildiginden program uygulanmadi.");
            return validation;
        }

        var section = await _context.CourseSections
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            return new ScheduleValidationResultDto
            {
                IsValid = false,
                Notes = { "Section bulunamadi." }
            };
        }

        var slots = request.ScheduleSlots.Select(s => new ScheduleSlot
        {
            Day = s.Day,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
        }).ToList();

        section.ScheduleJson = JsonSerializer.Serialize(slots, ScheduleJsonOptions);

        section.ClassroomId = request.ClassroomId;

        await _context.SaveChangesAsync(cancellationToken);

        validation.Notes.Add("Program basariyla uygulanarak kaydedildi.");
        return validation;
    }

    public async Task<BatchAutoScheduleResultDto> BatchAutoScheduleAsync(BatchAutoScheduleRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = new BatchAutoScheduleResultDto();

        var resolvedTerm = await ResolveTermAsync(request.TermId, cancellationToken);
        if (resolvedTerm == null)
        {
            result.Notes.Add(request.TermId.HasValue
                ? "Belirtilen akademik donem bulunamadi."
                : "Aktif akademik donem bulunamadi.");
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
            result.Notes.Add("Programsiz section bulunamadi, tum section'lar zaten programli.");
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
                    : "Uygun slot kombinasyonu bulunamadi.";
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
                    : "Uygulama sirasinda conflict tespit edildi.";
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
                    CourseName = s.CourseName
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

    private string GetDayName(int day)
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
}
