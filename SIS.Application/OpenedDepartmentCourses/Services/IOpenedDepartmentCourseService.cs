using SIS.Application.OpenedDepartmentCourses.DTOs;

namespace SIS.Application.OpenedDepartmentCourses.Services;

public interface IOpenedDepartmentCourseService
{
    /// <summary>
    /// Verilen kullanıcının (öğrenci) bölümünde aktif dönemde açılmış tüm CourseSection'ları döner.
    /// Sınıftan bağımsız — 1, 2, 3, 4. sınıf tüm dersler dahil.
    /// Öğrenci kaydı veya aktif dönem yoksa boş items döner.
    /// </summary>
    Task<OpenedDepartmentCoursesDto> GetForStudentAsync(int userId, CancellationToken ct = default);
}
