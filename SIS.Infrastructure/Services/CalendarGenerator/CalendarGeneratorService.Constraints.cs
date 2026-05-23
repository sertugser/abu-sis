using System.Text.Json;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public partial class CalendarGeneratorService
{
    private static bool Matches(CalendarConstraintInputDto c, CalendarEventType eventType, string chainCode, string? templateItemKey = null, SemesterType? semester = null) =>
        (!c.TargetEventType.HasValue || c.TargetEventType.Value == eventType)
        && (string.IsNullOrWhiteSpace(c.TargetChainCode) || string.Equals(c.TargetChainCode, chainCode, StringComparison.OrdinalIgnoreCase))
        && MatchesTemplateItem(c, templateItemKey)
        && MatchesSemester(c, semester);

    private static IReadOnlySet<string> ResolvePinnedDraftEventIds(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<CalendarConstraintInputDto> constraints)
    {
        var pinConstraints = constraints
            .Where(c => c.ConstraintType == CalendarConstraintType.Pin)
            .ToList();
        if (pinConstraints.Count == 0)
            return new HashSet<string>(StringComparer.Ordinal);

        return events
            .Where(ev => !ev.IsHoliday && pinConstraints.Any(c =>
                Matches(c, ev.EventType, ev.ChainCode, ev.TemplateItemKey, ev.Semester)))
            .Select(ev => ev.DraftEventId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool MatchesTemplateItem(CalendarConstraintInputDto constraint, string? templateItemKey)
    {
        if (string.IsNullOrWhiteSpace(templateItemKey))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(constraint.ParameterJson);
            if (!doc.RootElement.TryGetProperty("templateItemKey", out var keyProperty) || keyProperty.ValueKind != JsonValueKind.String)
                return true;

            var constraintKey = keyProperty.GetString();
            return string.IsNullOrWhiteSpace(constraintKey) || string.Equals(constraintKey, templateItemKey, StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool MatchesSemester(CalendarConstraintInputDto constraint, SemesterType? semester)
    {
        if (!semester.HasValue)
            return true;

        try
        {
            using var doc = JsonDocument.Parse(constraint.ParameterJson);
            if (!doc.RootElement.TryGetProperty("semester", out var semesterProperty) || semesterProperty.ValueKind != JsonValueKind.String)
                return true;

            var constraintSemester = semesterProperty.GetString();
            return string.IsNullOrWhiteSpace(constraintSemester)
                || string.Equals(constraintSemester, semester.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return true;
        }
    }
}
