using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.CourseIntention.DTOs;
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

        var requestCourseIds = request.Intentions.Select(i => i.CourseId).ToHashSet();
        if (requestCourseIds.Except(availableCourseIds).Any())
            throw new InvalidOperationException("Danisman kisiti veya uygunluk nedeniyle secilemeyen dersler ankete kaydedilemez.");

        var existingIntentions = await _context.CourseIntentions
            .Where(ci => ci.SurveyId == survey.Id && ci.StudentId == studentId)
            .ToListAsync(cancellationToken);

        foreach (var obsolete in existingIntentions.Where(ci => !requestCourseIds.Contains(ci.CourseId)).ToList())
        {
            _context.CourseIntentions.Remove(obsolete);
        }

        var existingByCourseId = existingIntentions.ToDictionary(ci => ci.CourseId, ci => ci);
        foreach (var item in request.Intentions.GroupBy(i => i.CourseId).Select(g => g.Last()))
        {
            if (existingByCourseId.TryGetValue(item.CourseId, out var existing))
            {
                existing.Intention = item.Intention;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.CourseIntentions.Add(new SIS.Domain.Entities.CourseIntention
                {
                    SurveyId = survey.Id,
                    StudentId = studentId,
                    CourseId = item.CourseId,
                    Intention = item.Intention,
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
            AvailableCourses = await GetAvailableCourseOptionsAsync(studentId, survey.TermId, cancellationToken),
            Responses = responses.Select(i => new CourseIntentionResponseDto
            {
                CourseId = i.CourseId,
                CourseCode = i.Course.Code,
                CourseName = i.Course.Name,
                Intention = i.Intention
            }).ToList(),
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
            .Where(c => c.IsEligible && !blockedCourseIds.Contains(c.CourseId))
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
                IsBlockedByAdvisor = false
            })
            .OrderBy(c => TakeStatusOrder(c.TakeStatus))
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
