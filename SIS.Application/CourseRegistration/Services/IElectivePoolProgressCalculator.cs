namespace SIS.Application.CourseRegistration.Services;

/// <summary>Bir öğrencinin seçmeli havuz slotları için ilerlemesini, CurriculumStatusService
/// ile AYNI mantıkla hesaplar: yalnızca geçen not sayılır (Draft / W / FF / NA / U / I hariç).
/// Tek kaynak — pool-status endpoint'i buna delege olur.</summary>
public interface IElectivePoolProgressCalculator
{
    Task<IReadOnlyDictionary<int, PoolProgress>> CalculateAsync(int studentId, IReadOnlyCollection<int> poolIds, CancellationToken ct = default);
}

public sealed record PoolProgress(int CompletedCourseCount, int EarnedEcts);
