namespace SIS.Application.Scheduling.DTOs;

/// <summary>
/// Sayfa açılışında öğrencinin default filtre değerlerini ve dropdown'larını
/// tek istekte döndüren context. Frontend bu yanıttan default term/program/classYear
/// ve seçilebilir tüm seçenekleri kurar.
/// </summary>
public sealed record DepartmentScheduleContextDto(
    int ProgramId,
    string ProgramName,
    string DepartmentName,
    int DefaultClassYear,
    List<int> AvailableClassYears,
    List<DepartmentScheduleTermDto> Terms);
