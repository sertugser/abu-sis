namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 11 — IRulePredicate'tan dönen tek violation birimi. RuleEngine bunu
/// <c>SchedulingConflictDto</c>'ya map'ler (UI ve mevcut endpoint imzaları geriye uyumlu kalır).
/// </summary>
/// <param name="Code">Constraint code (H1, H4, H-DL vb.). Plan 10 ResolveCategory + IsDisabled için bu key kullanılır.</param>
/// <param name="ConflictType">Mevcut <c>SchedulingConflictDto.ConflictType</c> ile aynı string (örn. "ClassroomConflict", "DailyLoadConflict") — UI gruplama için.</param>
/// <param name="Message">Kullanıcıya gösterilecek Türkçe açıklama.</param>
/// <param name="SectionId">İhlal eden section.</param>
/// <param name="RelatedSectionId">İhlalin diğer tarafındaki section (varsa). Bidirectional conflict dedup için kullanılır.</param>
/// <param name="Day">İhlal slot'unun günü (0-6).</param>
/// <param name="StartTime">İhlal slot başlangıcı.</param>
/// <param name="EndTime">İhlal slot bitişi.</param>
public sealed record RuleViolation(
    string Code,
    string ConflictType,
    string Message,
    int SectionId,
    int? RelatedSectionId = null,
    int? Day = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null);
