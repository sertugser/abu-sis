namespace SIS.Domain.Identity;

/// <summary>
/// Birleşik yetki kaydı. (ScopeKind, TargetId) = kategori veya rapor; (SubjectKind, SubjectId) =
/// rol/kullanıcı/grup; Effect = Allow/Deny. Tek çözümleme yolu (ReportAccessResolver).
/// </summary>
public class ReportAccessGrant
{
    public int Id { get; set; }
    public ReportGrantScopeKind ScopeKind { get; set; }
    public int TargetId { get; set; }            // CategoryId veya ReportId
    public ReportGrantSubjectKind SubjectKind { get; set; }
    public int SubjectId { get; set; }           // RoleId / UserId / GroupId
    public ReportGrantEffect Effect { get; set; }
    public string? Reason { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public int? GrantedByUserId { get; set; }
}
