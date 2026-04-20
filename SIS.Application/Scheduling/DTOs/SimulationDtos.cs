using System.Text.Json.Serialization;

namespace SIS.Application.Scheduling.DTOs;

public sealed record SimulateMoveRequest(
    int TermId,
    int SectionId,
    int NewDayOfWeek,
    string NewTimeSlotStart,
    int? NewClassroomId);

public sealed record SimulateConstraintChangeRequest(
    int TermId,
    List<ConstraintChangeItem> Changes);

public sealed record ConstraintChangeItem(
    int ConstraintId,
    string Field,
    [property: JsonConverter(typeof(JsonElementConverter))]
    object Value);

public sealed record SimulationResult(
    bool Feasible,
    SimulationBreakdown Before,
    SimulationBreakdown After,
    SimulationDiff Diff);

public sealed record SimulationBreakdown(
    int HardViolations,
    double SoftPenalty);

public sealed record SimulationDiff(
    IReadOnlyList<string> ResolvedConflicts,
    IReadOnlyList<string> NewConflicts,
    double PenaltyDelta,
    bool NetImprovement);

public sealed record SuggestionItem(
    int Rank,
    int DayOfWeek,
    string TimeSlot,
    int? ClassroomId,
    string? ClassroomName,
    int HardViolations,
    double SoftPenalty,
    double Improvement,
    IReadOnlyList<string> Tradeoffs);

/// <summary>
/// Handles deserialization of the Value field in ConstraintChangeItem,
/// which can be bool, int, or string from the JSON payload.
/// </summary>
internal sealed class JsonElementConverter : JsonConverter<object>
{
    public override object? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            System.Text.Json.JsonTokenType.True => true,
            System.Text.Json.JsonTokenType.False => false,
            System.Text.Json.JsonTokenType.Number when reader.TryGetInt32(out var i) => i,
            System.Text.Json.JsonTokenType.Number => reader.GetDouble(),
            System.Text.Json.JsonTokenType.String => reader.GetString()!,
            _ => System.Text.Json.JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, object value, System.Text.Json.JsonSerializerOptions options)
    {
        System.Text.Json.JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
