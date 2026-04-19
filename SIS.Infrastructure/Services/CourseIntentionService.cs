using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.CourseIntention.DTOs;
using SIS.Application.StudentEligibility.DTOs;
using SIS.Application.CourseIntention.Services;
using SIS.Application.StudentEligibility.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class CourseIntentionService : ICourseIntentionService
{
    private readonly AppDbContext _context;
    private readonly IStudentEligibilityService _eligibilityService;
    private readonly IAdvisorConstraintService _advisorConstraintService;

    public CourseIntentionService(
        AppDbContext context,
        IStudentEligibilityService eligibilityService,
        IAdvisorConstraintService advisorConstraintService)
    {
        _context = context;
        _eligibilityService = eligibilityService;
        _advisorConstraintService = advisorConstraintService;
    }

    public async Task<CourseIntentionSurveyDetailDto?> GetActiveSurveyForStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var survey = await _context.CourseIntentionSurveys
            .AsNoTracking()
            .Include(s => s.Term)
            .Where(s => s.Status == CourseIntentionSurveyStatus.Open)
            .OrderByDescending(s => s.OpenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return survey == null
            ? null
            : await BuildSurveyDetailAsync(studentId, survey, cancellationToken);
    }

    public async Task<CourseIntentionSurveyDetailDto?> GetSurveyForStudentAsync(
        int studentId,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var survey = await _context.CourseIntentionSurveys
            .AsNoTracking()
            .Include(s => s.Term)
            .FirstOrDefaultAsync(s => s.Id == surveyId, cancellationToken);

        return survey == null
            ? null
            : await BuildSurveyDetailAsync(studentId, survey, cancellationToken);
    }

    public async Task<CourseIntentionSurveyDetailDto> SaveResponsesAsync(
        int studentId,
        SaveCourseIntentionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var survey = await _context.CourseIntentionSurveys
            .Include(s => s.Term)
            .FirstOrDefaultAsync(s => s.Id == request.SurveyId, cancellationToken)
            ?? throw new InvalidOperationException("Anket bulunamadi.");

        if (survey.Status != CourseIntentionSurveyStatus.Open)
            throw new InvalidOperationException("Anket su anda acik degil.");

        var availableCourses = await GetAvailableCourseOptionsAsync(studentId, survey.TermId, cancellationToken);
        var availableCourseIds = availableCourses.Select(c => c.CourseId).ToHashSet();
        var lockedIntentions = availableCourses
            .Where(c => c.IsLocked && c.LockedIntention.HasValue)
            .ToDictionary(c => c.CourseId, c => c.LockedIntention!.Value);

        var requestCourseIds = request.Intentions.Select(i => i.CourseId).ToHashSet();
        if (requestCourseIds.Except(availableCourseIds).Any())
            throw new InvalidOperationException("Danisman kisiti veya uygunluk nedeniyle secilemeyen dersler ankete kaydedilemez.");

        var effectiveIntentions = request.Intentions
            .GroupBy(i => i.CourseId)
            .Select(g => g.Last())
            .Where(i => availableCourseIds.Contains(i.CourseId) && !lockedIntentions.ContainsKey(i.CourseId))
            .ToDictionary(i => i.CourseId, i => i.Intention);

        foreach (var lockedItem in lockedIntentions)
        {
            effectiveIntentions[lockedItem.Key] = lockedItem.Value;
        }

        var existingIntentions = await _context.CourseIntentions
            .Where(ci => ci.SurveyId == survey.Id && ci.StudentId == studentId)
            .ToListAsync(cancellationToken);

        foreach (var obsolete in existingIntentions.Where(ci => !effectiveIntentions.ContainsKey(ci.CourseId)).ToList())
        {
            _context.CourseIntentions.Remove(obsolete);
        }

        var existingByCourseId = existingIntentions.ToDictionary(ci => ci.CourseId, ci => ci);
        foreach (var item in effectiveIntentions)
        {
            if (existingByCourseId.TryGetValue(item.Key, out var existing))
            {
                existing.Intention = item.Value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.CourseIntentions.Add(new SIS.Domain.Entities.CourseIntention
                {
                    SurveyId = survey.Id,
                    StudentId = studentId,
                    CourseId = item.Key,
                    Intention = item.Value,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        var preference = await _context.StudentSurveyPreferences
            .FirstOrDefaultAsync(p => p.SurveyId == survey.Id && p.StudentId == studentId, cancellationToken);

        if (preference == null)
        {
            preference = new SIS.Domain.Entities.StudentSurveyPreference
            {
                SurveyId = survey.Id,
                StudentId = studentId,
                PreferredDays = NormalizePreferenceValue(request.Preference.PreferredDays),
                PreferredTimeRanges = NormalizePreferenceValue(request.Preference.PreferredTimeRanges),
                CreatedAt = DateTime.UtcNow
            };
            _context.StudentSurveyPreferences.Add(preference);
        }
        else
        {
            preference.PreferredDays = NormalizePreferenceValue(request.Preference.PreferredDays);
            preference.PreferredTimeRanges = NormalizePreferenceValue(request.Preference.PreferredTimeRanges);
            preference.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var detail = await GetSurveyForStudentAsync(studentId, survey.Id, cancellationToken);
        return detail ?? throw new InvalidOperationException("Anket detaylari yuklenemedi.");
    }

    public async Task<List<CourseIntentionSurveyDto>> GetSurveysAsync(
        int? termId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CourseIntentionSurveys
            .AsNoTracking()
            .Include(s => s.Term)
            .AsQueryable();

        if (termId.HasValue)
            query = query.Where(s => s.TermId == termId.Value);

        return await query
            .OrderByDescending(s => s.Term.Year)
            .ThenByDescending(s => s.Term.Semester)
            .Select(s => ToSurveyDto(s))
            .ToListAsync(cancellationToken);
    }

    public async Task<CourseIntentionSurveyDto> UpsertSurveyAsync(
        UpsertCourseIntentionSurveyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CloseAtUtc <= request.OpenAtUtc)
            throw new InvalidOperationException("Kapanis tarihi acilis tarihinden sonra olmalidir.");

        var term = await _context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Id == request.TermId, cancellationToken)
            ?? throw new InvalidOperationException("Donem bulunamadi.");

        var survey = await _context.CourseIntentionSurveys
            .FirstOrDefaultAsync(s => s.TermId == request.TermId, cancellationToken);

        if (survey == null)
        {
            survey = new SIS.Domain.Entities.CourseIntentionSurvey
            {
                TermId = request.TermId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? $"{term.Name} Niyet Anketi" : request.Title.Trim(),
                OpenAtUtc = request.OpenAtUtc,
                CloseAtUtc = request.CloseAtUtc,
                Status = ResolveSurveyStatus(request.OpenAtUtc, request.CloseAtUtc, DateTime.UtcNow),
                LastStatusChangedAtUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _context.CourseIntentionSurveys.Add(survey);
        }
        else
        {
            survey.Title = string.IsNullOrWhiteSpace(request.Title) ? survey.Title : request.Title.Trim();
            survey.OpenAtUtc = request.OpenAtUtc;
            survey.CloseAtUtc = request.CloseAtUtc;
            if (!survey.IsManualOverride)
            {
                survey.Status = ResolveSurveyStatus(request.OpenAtUtc, request.CloseAtUtc, DateTime.UtcNow);
                survey.LastStatusChangedAtUtc = DateTime.UtcNow;
            }
            survey.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        survey.Term = term;
        return ToSurveyDto(survey);
    }

    public async Task<CourseIntentionSurveyDto?> SetSurveyStateAsync(
        int surveyId,
        SetCourseIntentionSurveyStateRequest request,
        CancellationToken cancellationToken = default)
    {
        var survey = await _context.CourseIntentionSurveys
            .Include(s => s.Term)
            .FirstOrDefaultAsync(s => s.Id == surveyId, cancellationToken);

        if (survey == null)
            return null;

        survey.IsManualOverride = request.IsManualOverride;
        survey.Status = request.IsManualOverride
            ? request.Status
            : ResolveSurveyStatus(survey.OpenAtUtc, survey.CloseAtUtc, DateTime.UtcNow);
        survey.LastStatusChangedAtUtc = DateTime.UtcNow;
        survey.ManualOverrideAtUtc = request.IsManualOverride ? DateTime.UtcNow : null;
        survey.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return ToSurveyDto(survey);
    }

    public async Task<CourseIntentionSummaryDto?> GetSurveySummaryAsync(
        int surveyId,
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var survey = await _context.CourseIntentionSurveys
            .AsNoTracking()
            .Include(s => s.Term)
            .FirstOrDefaultAsync(s => s.Id == surveyId, cancellationToken);

        if (survey == null)
            return null;

        var intentions = await _context.CourseIntentions
            .AsNoTracking()
            .Include(i => i.Course)
            .Where(i => i.SurveyId == surveyId && (!departmentId.HasValue || i.Course.DepartmentId == departmentId.Value))
            .ToListAsync(cancellationToken);

        var respondingStudentIds = intentions.Select(i => i.StudentId).Distinct().ToHashSet();
        var preferences = await _context.StudentSurveyPreferences
            .AsNoTracking()
            .Where(p => p.SurveyId == surveyId && respondingStudentIds.Contains(p.StudentId))
            .ToListAsync(cancellationToken);

        var preferencesByStudentId = preferences.ToDictionary(p => p.StudentId, p => p);

        var courses = intentions
            .GroupBy(i => new { i.CourseId, i.Course.Code, i.Course.Name })
            .OrderBy(g => g.Key.Code)
            .Select(g =>
            {
                var relatedPreferences = g
                    .Select(i => preferencesByStudentId.GetValueOrDefault(i.StudentId))
                    .Where(p => p != null)
                    .Cast<SIS.Domain.Entities.StudentSurveyPreference>()
                    .ToList();

                return new CourseIntentionSummaryItemDto
                {
                    CourseId = g.Key.CourseId,
                    CourseCode = g.Key.Code,
                    CourseName = g.Key.Name,
                    CertainCount = g.Count(i => i.Intention == CourseIntentionLevel.Certain),
                    MaybeCount = g.Count(i => i.Intention == CourseIntentionLevel.Maybe),
                    PreferredDays = FormatPreferenceCounts(relatedPreferences.SelectMany(p => SplitPreferenceTokens(p.PreferredDays))),
                    PreferredTimeRanges = FormatPreferenceCounts(relatedPreferences.SelectMany(p => SplitPreferenceTokens(p.PreferredTimeRanges)))
                };
            })
            .ToList();

        return new CourseIntentionSummaryDto
        {
            SurveyId = survey.Id,
            TermId = survey.TermId,
            TermName = survey.Term.Name,
            TotalRespondingStudents = respondingStudentIds.Count,
            Courses = courses
        };
    }

    private async Task<CourseIntentionSurveyDetailDto> BuildSurveyDetailAsync(
        int studentId,
        SIS.Domain.Entities.CourseIntentionSurvey survey,
        CancellationToken cancellationToken)
    {
        var availableCourses = await GetAvailableCourseOptionsAsync(studentId, survey.TermId, cancellationToken);
        var availableCoursesById = availableCourses.ToDictionary(c => c.CourseId);
        var responses = await _context.CourseIntentions
            .AsNoTracking()
            .Include(i => i.Course)
            .Where(i => i.SurveyId == survey.Id && i.StudentId == studentId)
            .OrderBy(i => i.Course.Code)
            .ToListAsync(cancellationToken);

        var preference = await _context.StudentSurveyPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SurveyId == survey.Id && p.StudentId == studentId, cancellationToken);

        return new CourseIntentionSurveyDetailDto
        {
            Survey = ToSurveyDto(survey),
            AvailableCourses = availableCourses,
            Responses = MergeResponses(responses, availableCoursesById),
            Preference = new StudentSurveyPreferenceDto
            {
                PreferredDays = preference?.PreferredDays ?? string.Empty,
                PreferredTimeRanges = preference?.PreferredTimeRanges ?? string.Empty
            }
        };
    }

    private async Task<List<CourseIntentionOptionDto>> GetAvailableCourseOptionsAsync(
        int studentId,
        int termId,
        CancellationToken cancellationToken)
    {
        var eligibility = await _eligibilityService.GetStudentEligibleCoursesAsync(studentId, cancellationToken);
        var blockedCourseIds = await _advisorConstraintService.GetBlockedCourseIdsAsync(studentId, termId, cancellationToken);

        return eligibility
            .Where(c => c.IsEligible && (!blockedCourseIds.Contains(c.CourseId) || IsLockedRetake(c)))
            .Select(c => new CourseIntentionOptionDto
            {
                CourseId = c.CourseId,
                CourseCode = c.CourseCode,
                CourseName = c.CourseName,
                Ects = c.Ects,
                SuggestedSemester = c.SuggestedSemester,
                IsCompulsory = c.IsCompulsory,
                TakeStatus = MapTakeStatus(c.TakeType),
                PreviousGrade = c.PreviousGrade?.ToString(),
                IsBlockedByAdvisor = blockedCourseIds.Contains(c.CourseId),
                IsLocked = IsLockedRetake(c),
                LockedIntention = IsLockedRetake(c) ? CourseIntentionLevel.Certain : null,
                LockReason = BuildLockReason(c.PreviousGrade)
            })
            .OrderBy(c => TakeStatusOrder(c.TakeStatus))
            .ThenBy(c => c.IsLocked ? 0 : 1)
            .ThenBy(c => c.SuggestedSemester)
            .ThenBy(c => c.CourseCode)
            .ToList();
    }

    private static CourseIntentionSurveyDto ToSurveyDto(SIS.Domain.Entities.CourseIntentionSurvey survey)
    {
        return new CourseIntentionSurveyDto
        {
            Id = survey.Id,
            TermId = survey.TermId,
            TermName = survey.Term.Name,
            Title = survey.Title,
            Status = survey.Status,
            OpenAtUtc = survey.OpenAtUtc,
            CloseAtUtc = survey.CloseAtUtc,
            IsManualOverride = survey.IsManualOverride,
            LastStatusChangedAtUtc = survey.LastStatusChangedAtUtc
        };
    }

    internal static CourseIntentionSurveyStatus ResolveSurveyStatus(DateTime openAtUtc, DateTime closeAtUtc, DateTime nowUtc)
    {
        if (nowUtc < openAtUtc)
            return CourseIntentionSurveyStatus.Scheduled;

        if (nowUtc > closeAtUtc)
            return CourseIntentionSurveyStatus.Closed;

        return CourseIntentionSurveyStatus.Open;
    }

    private static string NormalizePreferenceValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static List<string> FormatPreferenceCounts(IEnumerable<string> values)
    {
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();
    }

    private static IEnumerable<string> SplitPreferenceTokens(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token));
    }

    private static List<CourseIntentionResponseDto> MergeResponses(
        IEnumerable<SIS.Domain.Entities.CourseIntention> responses,
        IReadOnlyDictionary<int, CourseIntentionOptionDto> availableCoursesById)
    {
        var merged = responses.ToDictionary(
            response => response.CourseId,
            response =>
            {
                var normalizedIntention = availableCoursesById.TryGetValue(response.CourseId, out var option)
                    && option.IsLocked
                    && option.LockedIntention.HasValue
                    ? option.LockedIntention.Value
                    : response.Intention;

                return new CourseIntentionResponseDto
                {
                    CourseId = response.CourseId,
                    CourseCode = response.Course.Code,
                    CourseName = response.Course.Name,
                    Intention = normalizedIntention
                };
            });

        foreach (var option in availableCoursesById.Values.Where(option => option.IsLocked && option.LockedIntention.HasValue))
        {
            merged[option.CourseId] = new CourseIntentionResponseDto
            {
                CourseId = option.CourseId,
                CourseCode = option.CourseCode,
                CourseName = option.CourseName,
                Intention = option.LockedIntention!.Value
            };
        }

        return merged.Values
            .OrderBy(response => response.CourseCode)
            .ToList();
    }

    private static bool IsLockedRetake(StudentCourseEligibilityDto item)
    {
        return item.TakeType == "MustRetake"
            && item.PreviousGrade is GradeCode.FF or GradeCode.NA or GradeCode.U;
    }

    private static string? BuildLockReason(GradeCode? previousGrade)
    {
        return previousGrade is GradeCode.FF or GradeCode.NA or GradeCode.U
            ? $"Bu ders onceki notun {previousGrade} oldugu icin sistem tarafindan zorunlu olarak eklendi."
            : null;
    }

    private static string MapTakeStatus(string takeType) => takeType switch
    {
        "MustRetake" => "Alttan",
        "BehindSchedule" => "Gecikmis",
        "UpperCourse" => "UstDonem",
        _ => "Normal"
    };

    private static int TakeStatusOrder(string takeStatus) => takeStatus switch
    {
        "Alttan" => 0,
        "Gecikmis" => 1,
        "Normal" => 2,
        _ => 3
    };
}
