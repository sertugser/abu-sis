namespace SIS.Domain.Identity;

/// <summary>
/// Vekâlet kaydı: bir kullanıcı (Delegator), başka bir kullanıcıya (Delegatee)
/// belirli bir tarih aralığında kendi rollerinin yetkilerini devreder.
/// Vekil, vekâlet verenin scope'unu da devralır (faculty-bound olanlar dâhil).
/// </summary>
public class PermissionDelegation
{
    public int Id { get; set; }

    /// <summary>Yetkilerini devreden kullanıcı.</summary>
    public int DelegatorUserId { get; set; }

    /// <summary>Yetkileri devralan kullanıcı.</summary>
    public int DelegateeUserId { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }

    /// <summary>Sebep / not (audit için zorunlu).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Erken iptal anı (null = aktif veya doğal sona ermiş).</summary>
    public DateTime? RevokedAtUtc { get; set; }
    public int? RevokedByUserId { get; set; }
    public string? RevokeReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }

    /// <summary>
    /// Verili anda aktif sayılabilir mi? (RevokedAt yok + zaman penceresinde)
    /// </summary>
    public bool IsActiveAt(DateTime utcNow)
        => RevokedAtUtc is null && StartsAtUtc <= utcNow && utcNow <= EndsAtUtc;
}
