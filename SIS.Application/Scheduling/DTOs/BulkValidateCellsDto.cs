namespace SIS.Application.Scheduling.DTOs;

/// <summary>
/// Düzenleyici drag-start akışında çağrılır: bir section için aday hücre listesinin
/// her birini RuleEngine ile değerlendirip per-cell {isValid, reason} döndürür.
/// Tek HTTP çağrısında N hücre validate edilir; backend snapshot/config/state'i bir kez
/// kurar, her aday için sadece RuntimeCandidate + RuleEngine yeniden çalıştırılır.
/// </summary>
public class BulkValidateCellsRequestDto
{
    public int SectionId { get; set; }
    public int? InstructorId { get; set; }
    public int? ClassroomId { get; set; }
    public List<BulkValidateCellCandidateDto> Candidates { get; set; } = new();
}

public class BulkValidateCellCandidateDto
{
    /// <summary>
    /// Frontend tarafında hücre kimliği (`cell-{day}-{slotOrder}`). Backend bu değeri opak
    /// kabul eder, sadece response'ta geri yansıtır — frontend invalidDropIds set'ini
    /// güncellerken match için kullanır.
    /// </summary>
    public string CellKey { get; set; } = string.Empty;

    /// <summary>
    /// Bu aday hücre için section'ın TAM yeni schedule slot listesi (taşıma akışında
    /// section'ın diğer blokları + yeni aday block; pool akışında sadece aday block).
    /// </summary>
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
}

public class BulkValidateCellsResultDto
{
    public List<BulkValidateCellResultDto> Results { get; set; } = new();
}

public class BulkValidateCellResultDto
{
    public string CellKey { get; set; } = string.Empty;
    public bool IsValid { get; set; }

    /// <summary>
    /// Geçersiz ise ilk conflict mesajı; geçerli ise null. UI tooltip'inde gösterilir.
    /// </summary>
    public string? Reason { get; set; }
}
