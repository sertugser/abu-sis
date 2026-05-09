using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Manuel atama validate + çakışma raporları + classroom dashboard + diğer UI köprü
/// operasyonları. Partial class — büyük dosyayı sorumluluk-bazlı dosyalara böler:
///
///   - SchedulingService.cs            (constructor, alanlar, validate, suggest, apply, batch)
///   - SchedulingService.Snapshot.cs   (BuildTermSnapshotAsync ve helper'lar)
///
/// Auto-schedule motoruyla karıştırılmamalı — algoritma AutoScheduleEngine + ConstructionSolver
/// + SimulatedAnnealing + LNS dosyalarında.
/// </summary>
public partial class SchedulingService : ISchedulingService
{
    private readonly AppDbContext _context;
    private readonly ConstraintEvaluationService _constraintEvaluation;

    public SchedulingService(AppDbContext context, ConstraintEvaluationService constraintEvaluation)
    {
        _context = context;
        _constraintEvaluation = constraintEvaluation;
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

    // ValidateAsync, ValidateExistingAsync, ValidateExistingByDepartmentAsync
    // → SchedulingService.Validation.cs partial dosyasına taşındı.



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

        var slots = request.ScheduleSlots.Select(s => new TimeBlock
        {
            Day = (DayOfWeek)s.Day,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
        }).ToList();

        section.ClassroomId = request.ClassroomId;

        // Write SectionSchedule records (the authoritative schedule store)
        await WriteSectionScheduleRecordsAsync(section.Id, section.TermId, request.ClassroomId, slots, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        validation.Notes.Add("Program başarıyla uygulanarak kaydedildi.");
        return validation;
    }


    // ── Score Breakdown ────────────────────────────────────────────────

    public async Task<ScoreBreakdownDto> GetScoreBreakdownAsync(int termId, CancellationToken cancellationToken = default)
    {
        var snapshot = await BuildTermSnapshotAsync(termId, null, null, cancellationToken);
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);

        if (softConfig.S4Enabled)
            snapshot.CoEnrollmentPairs = await LoadCoEnrollmentPairsAsync(snapshot.TermId, softConfig.S4MinCoPairThreshold, cancellationToken);

        var config = BuildConstraintConfig(softConfig, hardConfig);
        var state = await _constraintEvaluation.LoadConstraintStateAsync(null, null, null, termId, cancellationToken);
        var weightMap = state.Definitions.ToDictionary(d => d.Key, d => d.Value.Weight);

        return _constraintEvaluation.EvaluateWithBreakdown(snapshot, config, state, weightMap);
    }

    // ── Private helpers ──────────────────────────────────────────────────
    // Snapshot-building helper'ları SchedulingService.Snapshot.cs partial dosyasında.

    // EvaluateExistingConflictsAsync, EvaluateCandidateConflicts
    // → SchedulingService.Validation.cs partial dosyasına taşındı.

    internal SchedulingConstraintConfig BuildConstraintConfig(SoftConstraintConfig soft, HardConstraintConfig? hard)
    {
        return new SchedulingConstraintConfig
        {
            LunchBreakIsHard = hard?.LunchBreakIsHard ?? false,
            LunchBreakStart = hard?.LunchBreakStart ?? new TimeOnly(11, 40),
            LunchBreakEnd = hard?.LunchBreakEnd ?? new TimeOnly(12, 20),
            MaxConsecutiveHours = hard?.MaxConsecutiveHours ?? 4,
            BreakDurationMinutes = hard?.BreakDurationMinutes ?? 5,
            AmphiThreshold = hard?.AmphiThreshold ?? 100,
            TheoryLabSplitThreshold = hard?.TheoryLabSplitThreshold ?? 4,
            AllowSameCourseSectionOverlap = hard?.AllowSameCourseSectionOverlap ?? false,
            CurriculumConflictMode = hard?.CurriculumConflictMode ?? "curriculum",
            S1Enabled = soft.S1Enabled,
            S1MinOccupancy = soft.S1MinOccupancy,
            S1MaxOccupancy = soft.S1MaxOccupancy,
            S2Enabled = soft.S2Enabled,
            S2MaxDailyHours = soft.S2MaxDailyHours,
            S3Enabled = soft.S3Enabled,
            S3MaxSectionsPerSlot = soft.S3MaxSectionsPerSlot,
            S4Enabled = soft.S4Enabled,
            S4MinCoPairThreshold = soft.S4MinCoPairThreshold,
            S5Enabled = soft.S5Enabled,
            S7Enabled = soft.S7Enabled,
            S7MaxGapSlots = soft.S7MaxGapSlots,
            S8Enabled = soft.S8Enabled,
            S12Enabled = soft.S12Enabled,
            S12MaxImbalancePercent = soft.S12MaxImbalancePercent,
        };
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
    /// Converts a list of consecutive TimeSlots into a single TimeBlock covering them.
    /// </summary>
    private static TimeBlock TimeSlotWindowToTimeBlock(IReadOnlyList<TimeSlot> window)
    {
        return new TimeBlock
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
        IReadOnlyList<TimeBlock> currentSlots,
        IReadOnlyList<TimeSlot> timeSlots)
    {
        var combinations = new List<RankedSlotCombination>();
        var signatures = new HashSet<string>(StringComparer.Ordinal);

        // Include the current schedule as first option if valid
        if (currentSlots.Count > 0)
        {
            var currentValidation = ValidateTimeBlocks(currentSlots.ToList());
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
                            Slot = TimeSlotWindowToTimeBlock(window),
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
    /// Loads existing SectionSchedule records for a section and converts them to TimeBlocks.
    /// </summary>
    private async Task<List<TimeBlock>> LoadSectionTimeBlocksAsync(int sectionId, CancellationToken cancellationToken)
    {
        var schedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == sectionId)
            .ToListAsync(cancellationToken);

        return ScheduleHelper.ToTimeBlocks(schedules);
    }

    private async Task<int> CountMatchedTimeSlotsAsync(
        int termId,
        IReadOnlyList<TimeBlock> slots,
        CancellationToken cancellationToken)
    {
        if (slots.Count == 0)
        {
            return 0;
        }

        var termTimeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .ToListAsync(cancellationToken);

        if (termTimeSlots.Count == 0)
        {
            return ResolveAssignedTimeSlotCount(slots);
        }

        return termTimeSlots
            .Where(ts => slots.Any(slot =>
                ts.DayOfWeek == slot.Day
                && ts.StartTime < slot.EndTime
                && slot.StartTime < ts.EndTime))
            .Select(ts => ts.Id)
            .Distinct()
            .Count();
    }

    /// <summary>
    /// Writes SectionSchedule records.
    /// Deletes existing records for this section first, then creates new ones
    /// by matching each TimeBlock to TimeSlots in the term.
    /// BlockType heuristic: course.TheoryHours kadar ilk slot Theory, kalanı Lab.
    /// </summary>
    private async Task WriteSectionScheduleRecordsAsync(
        int sectionId,
        int termId,
        int? classroomId,
        IReadOnlyList<TimeBlock> slots,
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

        // Dersin teorik/uygulama saat dağılımını yükle — BlockType heuristic'i için
        var courseHours = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.Id == sectionId)
            .Select(cs => new { cs.Course.TheoryHours, cs.Course.LabHours })
            .FirstOrDefaultAsync(cancellationToken);

        var theoryHours = courseHours?.TheoryHours ?? 0;
        var slotIndex = 0;

        foreach (var slot in slots)
        {
            // Find all TimeSlots that overlap with this TimeBlock on the same day
            var matchingTimeSlots = termTimeSlots
                .Where(ts => ts.DayOfWeek == slot.Day
                    && ts.StartTime < slot.EndTime
                    && slot.StartTime < ts.EndTime)
                .OrderBy(ts => ts.SlotOrder)
                .ToList();

            foreach (var ts in matchingTimeSlots)
            {
                // İlk theoryHours kadar slot Theory, sonrası Lab.
                // Scheduler CandidateGenerator'ı Theory bloklarını Lab bloklarından önce üretir,
                // dolayısıyla slot sırası teorik→uygulama düzenindedir.
                var blockType = slotIndex < theoryHours
                    ? ScheduleBlockType.Theory
                    : ScheduleBlockType.Lab;

                _context.SectionSchedules.Add(new SectionSchedule
                {
                    CourseSectionId = sectionId,
                    TimeSlotId = ts.Id,
                    ClassroomId = classroomId.Value,
                    BlockType = blockType,
                });
                slotIndex++;
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
        List<TimeBlock> slots,
        int preferenceScore)
    {
        var normalizedSlots = slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .Select(slot => new TimeBlock
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

    private static ScheduleParseResult ValidateTimeBlocks(List<TimeBlock> slots)
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

    private static List<TimeBlock> GetOverlapSlots(IReadOnlyList<TimeBlock> firstSchedule, IReadOnlyList<TimeBlock> secondSchedule) => SchedulingHelpers.GetOverlapSlots(firstSchedule, secondSchedule);

    private static bool TryGetOverlap(TimeBlock first, TimeBlock second, out TimeBlock overlap) => SchedulingHelpers.TryGetOverlap(first, second, out overlap);

    private static bool SlotsOverlap(TimeBlock first, TimeBlock second) => SchedulingHelpers.SlotsOverlap(first, second);

    private static int ResolveAssignedTimeSlotCount(IReadOnlyList<TimeBlock> slots) => SchedulingHelpers.ResolveAssignedTimeSlotCount(slots);

    private static decimal ResolveWeeklyHours(IReadOnlyList<TimeBlock> slots, Course course) => SchedulingHelpers.ResolveWeeklyHours(slots, course);

    private static decimal ResolveWeeklyHours(IReadOnlyList<TimeBlock> slots, SectionSnapshot section) => SchedulingHelpers.ResolveWeeklyHours(slots, section);

    private static ScheduleSlotDto MapTimeBlock(TimeBlock slot) =>
        new()
        {
            Day = (int)slot.Day,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime
        };

    private static SchedulingConflictDto BuildConflict(
        SectionSnapshot section,
        string conflictType,
        string message,
        TimeBlock? slot,
        int? relatedSectionId) => SchedulingHelpers.BuildConflict(section, conflictType, message, slot, relatedSectionId);

    private static bool TryGetSharedCurriculumDepartment(
        SectionSnapshot first,
        SectionSnapshot second,
        out int sharedDepartmentId) => SchedulingHelpers.TryGetSharedCurriculumDepartment(first, second, out sharedDepartmentId);

    private static Dictionary<int, int> GetSuggestedSemestersByDepartment(
        int courseId,
        IReadOnlyDictionary<int, Dictionary<int, int>> suggestedSemestersByDepartment)
    {
        return suggestedSemestersByDepartment.TryGetValue(courseId, out var values)
            ? values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            : new Dictionary<int, int>();
    }

    private static HashSet<int> GetAccessibleDepartments(
        int courseId,
        int? owningDepartmentId,
        IReadOnlyDictionary<int, HashSet<int>> accessibleDepartments)
    {
        var result = accessibleDepartments.TryGetValue(courseId, out var values)
            ? values.ToHashSet()
            : new HashSet<int>();

        if (owningDepartmentId.HasValue)
            result.Add(owningDepartmentId.Value);

        return result;
    }

    private static string BuildSlotSignature(IEnumerable<TimeBlock> slots) =>
        string.Join("|", slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .Select(slot => $"{(int)slot.Day}:{slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}"));

    private async Task<CourseSchedulingMetadata> LoadCourseSchedulingMetadataAsync(
        int termId,
        IEnumerable<int> courseIds,
        CancellationToken cancellationToken)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new CourseSchedulingMetadata();
        }

        var curriculumRows = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => ids.Contains(cc.CourseId) && cc.Curriculum.IsActive && cc.IsCompulsory)
            .Select(cc => new
            {
                cc.CourseId,
                DepartmentId = cc.Curriculum.Program.DepartmentId,
                cc.SuggestedSemester
            })
            .ToListAsync(cancellationToken);

        var suggestedSemestersByDepartment = curriculumRows
            .GroupBy(row => row.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(row => row.DepartmentId)
                    .ToDictionary(deptGroup => deptGroup.Key, deptGroup => deptGroup.Min(row => row.SuggestedSemester)));

        var minSuggestedSemesters = suggestedSemestersByDepartment
            .ToDictionary(item => item.Key, item => item.Value.Values.Min());

        var courseDepartmentIds = await _context.Courses
            .AsNoTracking()
            .Where(course => ids.Contains(course.Id))
            .Select(course => new { course.Id, course.DepartmentId })
            .ToDictionaryAsync(course => course.Id, course => course.DepartmentId, cancellationToken);

        var policyRows = await _context.CourseAccessPolicies
            .AsNoTracking()
            .Where(policy => policy.TermId == termId && ids.Contains(policy.CourseId))
            .Select(policy => new { policy.CourseId, policy.DepartmentId, policy.FacultyId })
            .ToListAsync(cancellationToken);

        var facultyIds = policyRows
            .Where(policy => !policy.DepartmentId.HasValue && policy.FacultyId.HasValue)
            .Select(policy => policy.FacultyId!.Value)
            .Distinct()
            .ToList();

        var facultyDepartmentMap = facultyIds.Count == 0
            ? new Dictionary<int, List<int>>()
            : await _context.Departments
                .AsNoTracking()
                .Where(department => facultyIds.Contains(department.FacultyId))
                .GroupBy(department => department.FacultyId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group.Select(department => department.Id).ToList(),
                    cancellationToken);

        var policiesByCourse = policyRows
            .GroupBy(policy => policy.CourseId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var accessibleDepartments = new Dictionary<int, HashSet<int>>();
        foreach (var courseId in ids)
        {
            var departmentIds = new HashSet<int>();

            if (suggestedSemestersByDepartment.TryGetValue(courseId, out var suggestedByDept))
            {
                departmentIds.UnionWith(suggestedByDept.Keys);
            }

            if (courseDepartmentIds.TryGetValue(courseId, out var owningDepartmentId) && owningDepartmentId.HasValue)
            {
                departmentIds.Add(owningDepartmentId.Value);
            }

            if (policiesByCourse.TryGetValue(courseId, out var policies))
            {
                foreach (var policy in policies)
                {
                    if (policy.DepartmentId.HasValue)
                    {
                        departmentIds.Add(policy.DepartmentId.Value);
                    }
                    else if (policy.FacultyId.HasValue
                        && facultyDepartmentMap.TryGetValue(policy.FacultyId.Value, out var facultyDepartments))
                    {
                        departmentIds.UnionWith(facultyDepartments);
                    }
                }
            }

            accessibleDepartments[courseId] = departmentIds;
        }

        return new CourseSchedulingMetadata
        {
            MinSuggestedSemesters = minSuggestedSemesters,
            SuggestedSemestersByDepartment = suggestedSemestersByDepartment,
            AccessibleDepartments = accessibleDepartments
        };
    }

    private async Task<AcademicTerm?> ResolveTermAsync(int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue)
        {
            return await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(term => term.Id == termId.Value, cancellationToken);
        }

        var now = DateTime.UtcNow;
        return await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(term => !term.IsCancelled && term.TermStart <= now && term.TermEnd >= now, cancellationToken);
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

    private static string GetDayName(int day) => SchedulingHelpers.GetDayName(day);

    // ── Soft constraint config helpers ────────────────────────────────────


}
