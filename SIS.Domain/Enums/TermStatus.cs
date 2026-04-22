namespace SIS.Domain.Enums;

/// <summary>
/// Dönem üst-seviye durumu (gösterim için).
/// Faz 1 revize: stored değil — AcademicTerm.IsCancelled (bool) + tarih kıyaslamasıyla hesaplanır.
/// </summary>
public enum TermStatus
{
    /// <summary>Tarih aralığı dışında veya henüz başlamamış.</summary>
    Inactive = 0,

    /// <summary>TermStart ≤ şimdi ≤ TermEnd ve iptal edilmemiş.</summary>
    Active = 1,

    /// <summary>Admin iptal etti — veriler kalır, yazma yok.</summary>
    Cancelled = 2
}
