namespace SIS.Application.Common.Services;

/// <summary>Bir dönemde harf notlarının öğrenciye görünür (yayınlanmış) olup olmadığını
/// tek kaynaktan belirler: letter-grade-publication açıksa görünür; değilse
/// makeup-letter-grade-publication açıksa görünür. CurriculumStatusService ve pool
/// ilerleme hesaplayıcısı bunu paylaşır (ikibaşlılık önlenir).</summary>
public interface IGradeVisibilityResolver
{
    Task<bool> IsVisibleAsync(int termId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, bool>> BuildMapAsync(IReadOnlyCollection<int> termIds, CancellationToken ct = default);
}
