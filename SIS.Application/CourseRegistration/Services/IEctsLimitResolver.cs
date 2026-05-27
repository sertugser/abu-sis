using CurriculumEntity = SIS.Domain.Entities.Curriculum;

namespace SIS.Application.CourseRegistration.Services;

/// <summary>
/// Dönemlik AKTS limitini tek kaynaktan çözer: müfredat override (Curriculum.EctsLimit*)
/// varsa onu, yoksa SystemConfig (Registration.EctsLimit.*) değerini kullanır.
/// CGPA eşikleri: &lt;2.00, 2.00–2.49, ≥2.50. Sabit/hardcoded tavan YOKTUR.
/// </summary>
public interface IEctsLimitResolver
{
    /// <param name="cgpa">Öğrencinin genel not ortalaması.</param>
    /// <param name="curriculum">Öğrencinin aktif ana müfredatı; null ise yalnızca SystemConfig kullanılır.</param>
    Task<int> ResolveAsync(decimal cgpa, CurriculumEntity? curriculum, CancellationToken ct = default);
}
