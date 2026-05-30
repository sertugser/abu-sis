using SIS.Application.CourseRegistration.DTOs;

namespace SIS.Application.CourseRegistration.Services;

public interface IRegistrationValidationService
{
    /// <summary>
    /// Ders seçimini iş kurallarına göre doğrular (önkoşul, ECTS limiti, çakışma, alttan ders vb.).
    /// <paramref name="skipGateChecks"/> = true iken öğrenci durumu / finansal blok / kayıt dönemi
    /// "kapı" kontrolleri atlanır; yalnızca akademik seçim kuralları işletilir. Danışman onayı
    /// (Kontrol Et ve Onayla) bu modu kullanır — kapı kontrolleri onay akışında ayrıca yapılır.
    /// </summary>
    Task<ValidationResultDto> ValidateRegistrationAsync(int studentId, List<int> courseSectionIds, bool skipGateChecks = false);
}
