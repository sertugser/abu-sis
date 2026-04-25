namespace SIS.Domain.Enums;

/// <summary>
/// Müfredat dönem kurallarında kullanılan sınıf değeri.
/// Hazırlık sınıfı (İngilizce programlarda) = Preparatory,
/// ardından 1–4. sınıflar (lisans seviyesi).
/// </summary>
public enum CurriculumClassYear
{
    Preparatory = 0,
    First = 1,
    Second = 2,
    Third = 3,
    Fourth = 4,
}
