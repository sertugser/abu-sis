namespace SIS.Domain.Enums;

/// <summary>
/// Dönemin onay akışı durumu. 5 state, sadece insan işi (onay/hazırlık).
/// Öğrenci/hoca fazları (kayıt, ekle/bırak, çekilme, not girişi) TermStatus değildir —
/// AcademicTerm üzerindeki computed methods (IsRegistrationOpen vb.) ile hesaplanır.
/// </summary>
public enum TermStatus
{
    /// <summary>Dönem oluşturuldu, tarihler henüz kesinleşmedi.</summary>
    Draft = 0,

    /// <summary>Tarihler netleşti. Ders açma, çizelgeleme, dekan onayı bu aşamada.</summary>
    Preparing = 1,

    /// <summary>Dönem canlı. Öğrenci/hoca erişimi tarih pencerelerine göre açık.</summary>
    Ready = 2,

    /// <summary>Dönem kapandı. Notlar finalize, değişiklik yok.</summary>
    Closed = 3,

    /// <summary>Arşiv. Salt okunur.</summary>
    Archived = 4
}
