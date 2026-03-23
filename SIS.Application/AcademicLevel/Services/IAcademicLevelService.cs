using SIS.Application.AcademicLevel.DTOs;

namespace SIS.Application.AcademicLevel.Services;

public interface IAcademicLevelService
{
    /// <summary>
    /// Tek bir ogrencinin akademik seviye analizini yapar.
    /// OfficialClassLevel (kayit bazli) ve AcademicLevel (ilerleme bazli) ayri hesaplanir.
    /// </summary>
    Task<AcademicLevelDto?> GetStudentAcademicLevelAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir bolumdeki tum ogrencilerin akademik seviye analizini toplu yapar.
    /// </summary>
    Task<BulkAcademicLevelDto> GetDepartmentAcademicLevelsAsync(
        int departmentId,
        CancellationToken cancellationToken = default);
}
