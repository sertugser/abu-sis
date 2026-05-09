using SIS.Application.Common.Services;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Auto-schedule engine — Plan 9 sonrası tek-path V2 (Google OR-Tools CP-SAT).
/// Eski v1 (MRV + SimulatedAnnealing + LNS + Construction + FailureAnalyzer) silindi.
/// </summary>
public sealed class AutoScheduleEngine
{
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _configProvider;
    private readonly ISolverEngine _solverEngine;
    private readonly IScheduleProgressReporter _progressReporter;
    private readonly IScheduleCancelRegistry? _cancelRegistry;

    public AutoScheduleEngine(
        AppDbContext context,
        ISystemConfigProvider configProvider,
        ISolverEngine solverEngine,
        IScheduleProgressReporter? progressReporter = null,
        IScheduleCancelRegistry? cancelRegistry = null)
    {
        _context = context;
        _configProvider = configProvider;
        _solverEngine = solverEngine;
        _progressReporter = progressReporter ?? new NoOpScheduleProgressReporter();
        _cancelRegistry = cancelRegistry;
    }

    public async Task<AutoSchedulePreviewDto> RunAsync(
        AutoScheduleRequest request,
        string? userId = null,
        CancellationToken ct = default)
    {
        // Cancel registry — Hub.Cancel() çağrısı linkedCts'i tetikleyip algoritmayı durdurur.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!string.IsNullOrEmpty(userId))
            _cancelRegistry?.Register(userId, linkedCts);
        ct = linkedCts.Token;

        async Task ReportAsync(string stage, int percent, int current = 0, int total = 0, string? detail = null)
        {
            if (!string.IsNullOrEmpty(userId))
                await _progressReporter.ReportAsync(userId,
                    new ScheduleProgressEvent(stage, percent, current, total, detail), ct);
        }

        try
        {
            await ReportAsync("started", 0, detail: "Auto-schedule başlatılıyor...");

            var seed = request.Seed ?? (int)(DateTime.UtcNow.Ticks % int.MaxValue);

            await ReportAsync("v2-building-request", 10,
                detail: "v2-ortools: SolverRequest DB'den inşa ediliyor...");

            var requestBuilder = new OrTools.SolverRequestBuilder(_context, _configProvider);
            var solverRequest = await requestBuilder.BuildAsync(
                request.TermId,
                request.FacultyId,
                request.DepartmentId,
                request.Seed,
                ct);

            await ReportAsync("v2-solving", 50,
                detail: $"v2-ortools: CP-SAT çözücü çalışıyor ({solverRequest.Sections.Count} section)...");

            var solverResult = await _solverEngine.SolveAsync(solverRequest, ct);

            await ReportAsync("complete", 100,
                detail: $"v2-ortools tamamlandı ({solverResult.Status}, {solverResult.Diagnostics.ElapsedSeconds:F2}s).");

            return MapSolverResultToPreviewDto(solverResult, solverRequest, seed);
        }
        catch (OperationCanceledException)
        {
            if (!string.IsNullOrEmpty(userId))
                await _progressReporter.ReportAsync(userId,
                    new ScheduleProgressEvent("failed", 0, 0, 0, "İşlem iptal edildi."), CancellationToken.None);
            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(userId))
                _cancelRegistry?.Unregister(userId);
        }
    }

    /// <summary>
    /// SolverResult → AutoSchedulePreviewDto mapping. CP-SAT'ın ürettiği Assignment ve Failure
    /// listelerini frontend'in beklediği DTO formatına çevirir.
    /// </summary>
    private static AutoSchedulePreviewDto MapSolverResultToPreviewDto(
        SolverResult result,
        SolverRequest solverRequest,
        int seed)
    {
        var sectionLookup = solverRequest.Sections.ToDictionary(s => s.Id, s => s);
        var classroomLookup = solverRequest.Classrooms.ToDictionary(c => c.Id, c => c);
        var timeSlotLookup = solverRequest.TimeSlots.ToDictionary(ts => ts.Id, ts => ts);

        var assignmentDtos = new List<AutoSchedulePreviewItemDto>();
        foreach (var asn in result.Assignments)
        {
            if (!sectionLookup.TryGetValue(asn.SectionId, out var section))
                continue;

            var primaryClassroomId = asn.Slots.Count > 0 ? asn.Slots[0].ClassroomId : (int?)null;
            ClassroomInput? primaryClassroom = null;
            if (primaryClassroomId.HasValue
                && classroomLookup.TryGetValue(primaryClassroomId.Value, out var cr))
            {
                primaryClassroom = cr;
            }

            var scheduleSlots = asn.Slots
                .Select(s =>
                {
                    var ts = timeSlotLookup.TryGetValue(s.TimeSlotId, out var slot) ? slot : null;
                    var crName = classroomLookup.TryGetValue(s.ClassroomId, out var c) ? c.Name : "?";
                    return new ScheduleSlotDto
                    {
                        Day = ts?.DayOfWeek ?? DayOfWeek.Monday,
                        StartTime = ts?.StartTime ?? TimeOnly.MinValue,
                        EndTime = ts?.EndTime ?? TimeOnly.MinValue,
                        ClassroomId = s.ClassroomId,
                        ClassroomName = crName
                    };
                })
                .ToList();

            var summary = string.Join(", ", scheduleSlots.Select(ss =>
                $"{ss.Day} {ss.StartTime:HH\\:mm}-{ss.EndTime:HH\\:mm} ({ss.ClassroomName})"));

            assignmentDtos.Add(new AutoSchedulePreviewItemDto
            {
                SectionId = section.Id,
                SectionNumber = section.SectionNumber,
                CourseCode = section.CourseCode,
                CourseName = section.CourseName,
                DepartmentCode = string.Empty,
                ClassroomId = primaryClassroomId,
                ClassroomName = primaryClassroom?.Name ?? string.Empty,
                ScheduleSlots = scheduleSlots,
                ScheduleSummary = summary,
                InstructorId = section.InstructorId,
                InstructorName = null,
                FitScore = 0
            });
        }

        var failureDtos = new List<AutoScheduleFailureDto>();
        foreach (var f in result.Failures)
        {
            if (!sectionLookup.TryGetValue(f.SectionId, out var section))
                continue;
            failureDtos.Add(new AutoScheduleFailureDto
            {
                SectionId = section.Id,
                SectionNumber = section.SectionNumber,
                Capacity = section.Capacity,
                CourseCode = section.CourseCode,
                CourseName = section.CourseName,
                Reason = f.Reason,
                Suggestions = new List<FailureSuggestionDto>()
            });
        }

        return new AutoSchedulePreviewDto
        {
            Seed = seed,
            TotalProcessed = assignmentDtos.Count + failureDtos.Count,
            SuccessCount = assignmentDtos.Count,
            FailedCount = failureDtos.Count,
            TotalPenalty = result.Diagnostics.ObjectiveValue,
            Assignments = assignmentDtos,
            Failures = failureDtos,
            Stats = new AutoScheduleStats
            {
                ElapsedSeconds = result.Diagnostics.ElapsedSeconds
            }
        };
    }
}
