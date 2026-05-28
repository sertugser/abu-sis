namespace SIS.Application.CourseRegistration.Services;

/// <summary>Öğrencinin o dönem seçtiği dersleri ("Seçilen Dersler") resmî PDF olarak üretir.</summary>
public interface IRegistrationPlanPdfService
{
    Task<byte[]> GenerateAsync(int studentId, CancellationToken cancellationToken = default);
}
