namespace SIS.Application.Attendance.DTOs;

/// <summary>Tek bir (dönem, program?) devamsızlık politikası satırı.</summary>
public sealed class TermAttendancePolicyDto
{
    /// <summary>null = dönem geneli varsayılanı; dolu = o programa özel override.</summary>
    public int? ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public int TermWeekCount { get; set; }
    public decimal TheoryAbsenceLimitPercent { get; set; }
    public decimal LabAbsenceLimitPercent { get; set; }
    public string Method { get; set; } = "Separate";
}

/// <summary>Bir dönemin tüm devamsızlık politikaları + global varsayılan referansı.</summary>
public sealed class TermAttendancePolicyListDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    /// <summary>Hiç dönem politikası yoksa kullanılan global SystemConfig varsayılanı (salt referans).</summary>
    public TermAttendancePolicyDto GlobalDefault { get; set; } = new();
    /// <summary>Dönem geneli (ProgramId null) + program override'ları.</summary>
    public List<TermAttendancePolicyDto> Policies { get; set; } = new();
}

/// <summary>Bir (dönem, program?) politikasını ekler/günceller.</summary>
public sealed class UpsertTermAttendancePolicyRequest
{
    public int? ProgramId { get; set; }
    public int TermWeekCount { get; set; }
    public decimal TheoryAbsenceLimitPercent { get; set; }
    public decimal LabAbsenceLimitPercent { get; set; }
    public string Method { get; set; } = "Separate";
}
