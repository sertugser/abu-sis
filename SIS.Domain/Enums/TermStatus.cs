namespace SIS.Domain.Enums;

/// <summary>
/// Dönem yaşam döngüsü. Her geçiş SystemAdmin tarafından manuel tetiklenir.
/// Draft → Planning → Scheduling → Published → RegistrationOpen →
/// AddDrop → WithdrawalPeriod → GradeEntry → Closed → Archived
/// </summary>
public enum TermStatus
{
    /// <summary>Dönem oluşturuldu, tarihler henüz kesinleşmedi. Düzenleme serbes.</summary>
    Draft = 0,

    /// <summary>Tarihler onaylandı. Bölümler ders açabilir, section oluşturabilir.</summary>
    Planning = 1,

    /// <summary>Ders açma tamamlandı. Hoca/derslik/zaman ataması yapılıyor.</summary>
    Scheduling = 2,

    /// <summary>Program yayınlandı. Öğrenciler kataloğu görebilir (read-only). Kayıt henüz kapalı.</summary>
    Published = 3,

    /// <summary>Ders kayıt dönemi açık. Öğrenciler plana ekleme ve submit yapabilir.</summary>
    RegistrationOpen = 4,

    /// <summary>Ders ekleme/bırakma dönemi. Mevcut kayıtlar değiştirilebilir.</summary>
    AddDrop = 5,

    /// <summary>Çekilme dönemi. Yalnızca W notu ile dersten çekilme yapılabilir.</summary>
    WithdrawalPeriod = 6,

    /// <summary>Not giriş dönemi. Öğretim üyeleri not girebilir.</summary>
    GradeEntry = 7,

    /// <summary>Dönem kapandı. Tüm notlar kilitlendi.</summary>
    Closed = 8,

    /// <summary>Arşivlendi. Salt okunur.</summary>
    Archived = 9
}
