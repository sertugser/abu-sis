using SIS.Application.CourseRegistration.DTOs;
using StudentEntity = SIS.Domain.Entities.Student;
using StudentProgramEntity = SIS.Domain.Entities.StudentProgram;

namespace SIS.Application.CourseRegistration.Services;

/// <summary>Verilen öğrenci + aktif programı için müfredat-ağacı kataloğunu üretir
/// (öğrenci kendi akışı ve danışman öğrenci-adına akışı bu servisi paylaşır).</summary>
public interface IStudentRegistrationCatalogService
{
    Task<StudentRegistrationCatalogDto> BuildAsync(
        StudentEntity student,
        StudentProgramEntity activeProgram,
        CancellationToken ct = default);
}
