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

    /// <summary>
    /// Kullanıcı override — true ise hard rule ihlali varsa bile atama yazılır ve conflict'ler
    /// warning olarak işaretlenir. False (varsayılan) ile çağrılırsa conflict varlığında atama
    /// yazılmaz; response'ta <see cref="AssignmentResultDto.RequiresConfirmation"/>=true ve
    /// conflict listesi döndürülür → frontend dialog gösterir, kullanıcı "Yine de devam" derse
    /// aynı request'i Force=true ile tekrar yollar.
    ///
    /// Override edilemeyen kontroller (varlık, yetki, finalize, term iptal) Force'tan bağımsız
    /// olarak hep çalışır; bunlar exception fırlatır (KeyNotFound/InvalidOperation/Unauthorized).
    /// </summary>
    public bool Force { get; set; } = false;
}

public class AssignmentResultDto
{
    public bool Success { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<SchedulingConflictDto> Conflicts { get; set; } = new();

    /// <summary>
    /// Override akışı — true ise atama YAZILMADI; backend conflict listesini döndürdü ve
    /// kullanıcının onayını bekliyor. Frontend dialog ile conflict mesajlarını gösterir;
    /// kullanıcı "Devam Et" derse aynı request Force=true ile tekrar yollanır.
    /// </summary>
    public bool RequiresConfirmation { get; set; } = false;
}
