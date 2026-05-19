using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.FacultyPlanning.DTOs;

public class AssignSectionRequest
{
    public int SectionId { get; set; }
    public int? ClassroomId { get; set; }
    public List<ScheduleSlotDto>? ScheduleSlots { get; set; }
    public int? InstructorId { get; set; }

    /// <summary>
    /// Plan 10 — V2 (CP-SAT) auto-schedule sonucu apply edilirken V1 evaluator-tabanlı
    /// hard kısıt kontrolünü atla. V2 zaten H1/H2/H4/H6/H8/H11 vb. encoder'larla enforce
    /// ediyor; ikinci kontrol paralel-lab section'larında false-positive üretiyor
    /// (aynı section'ın iki farklı odasını "çakışma" sanıyor). Default false: manuel
    /// admin atamalarında validator çalışmaya devam eder.
    /// </summary>
    public bool SkipHardValidation { get; set; } = false;
}

public class AssignmentResultDto
{
    public bool Success { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<SchedulingConflictDto> Conflicts { get; set; } = new();
}
