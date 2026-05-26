namespace SIS.Application.Reports;

public enum ReportParameterType
{
    Text,
    Int,
    Decimal,
    Bool,
    Date,
    Enum,
    Term,        // dönem seçici (lookup)
    Faculty,     // fakülte seçici (lookup)
    Department,  // bölüm seçici (lookup)
    Program,     // program seçici (lookup)
}

public sealed record ReportParameterOption(string Value, string Label);

public sealed record ReportParameter(
    string Key,
    string Label,
    ReportParameterType Type,
    bool Required = false,
    IReadOnlyList<ReportParameterOption>? Options = null);

public sealed record ReportParameterSchema(IReadOnlyList<ReportParameter> Parameters)
{
    public static readonly ReportParameterSchema Empty = new(Array.Empty<ReportParameter>());
}
