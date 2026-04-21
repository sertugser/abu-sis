namespace SIS.Domain.Enums;

/// <summary>
/// Müfredat yaşam döngüsü.
/// Draft: Düzenlenebilir, öğrencilere bağlanamaz.
/// Published: Yayında, öğrencilere bağlanır; ders ekleme/çıkarma engeli.
/// Archived: Geçmiş, yeni öğrenci bağlanamaz ama geçmiş öğrenci kayıtları bu müfredatta kalır.
/// </summary>
public enum CurriculumStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}
