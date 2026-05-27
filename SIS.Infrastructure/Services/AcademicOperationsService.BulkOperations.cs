using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Toplu danışman ve müfredat atama işlemleri. Her ikisi de transaction içinde çalışır,
/// öğrenci bazında başarı/atlama raporu üretir.
/// </summary>
public partial class AcademicOperationsService
{
    public async Task<BulkAcademicOperationResultDto?> BulkAssignAdvisorsAsync(
        BulkAdvisorAssignmentRequestDto request,
        int userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);
        if (scope.Departments.All(department => department.DepartmentId != request.DepartmentId))
            return null;

        var normalizedIds = request.StudentIds.Distinct().ToList();
        var result = new BulkAcademicOperationResultDto { RequestedCount = normalizedIds.Count };

        if (normalizedIds.Count == 0)
        {
            result.Errors.Add("En az bir ogrenci secilmelidir.");
            return result;
        }

        var accessibleDepartmentIds = scope.Departments
            .Select(department => department.DepartmentId)
            .ToHashSet();

        var advisor = await _context.Instructors
            .Include(instructor => instructor.Department)
            .FirstOrDefaultAsync(instructor =>
                instructor.Id == request.AdvisorId
                && accessibleDepartmentIds.Contains(instructor.DepartmentId),
                cancellationToken);

        if (advisor == null)
        {
            result.Errors.Add("Secilen danisman erisim kapsaminizda degil veya bulunamadi.");
            return result;
        }

        IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await using (transaction)
        {
            var studentPrograms = await _context.StudentPrograms
                .Include(studentProgram => studentProgram.Student)
                .Include(studentProgram => studentProgram.Program)
                .Where(studentProgram => normalizedIds.Contains(studentProgram.StudentId)
                    && studentProgram.IsActive
                    && studentProgram.Type == StudentProgramType.Main)
                .ToListAsync(cancellationToken);

            var users = await LoadUsersForStudentsAsync(studentPrograms.Select(sp => sp.Student), cancellationToken);
            foreach (var studentId in normalizedIds)
            {
                var studentProgram = studentPrograms.FirstOrDefault(sp =>
                    sp.StudentId == studentId && sp.Program.DepartmentId == request.DepartmentId);

                if (studentProgram == null)
                {
                    result.Results.Add(FailedResult(studentId, "Ogrenci bu bolumun aktif ana programinda bulunamadi."));
                    continue;
                }

                var student = studentProgram.Student;
                var studentName = BuildUserFullName(users.GetValueOrDefault(student.UserId), student.StudentNumber);
                if (!request.ReplaceExisting && student.AdvisorId.HasValue && student.AdvisorId.Value != request.AdvisorId)
                {
                    result.Results.Add(new BulkAcademicOperationResultItemDto
                    {
                        StudentId = student.Id,
                        StudentNumber = student.StudentNumber,
                        StudentName = studentName,
                        Success = false,
                        Message = "Mevcut danisman korunacak sekilde ayarlandigi icin atlanmis."
                    });
                    continue;
                }

                student.AdvisorId = request.AdvisorId;
                student.UpdatedAt = DateTime.UtcNow;
                result.Results.Add(new BulkAcademicOperationResultItemDto
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentNumber,
                    StudentName = studentName,
                    Success = true,
                    Message = "Danisman atandi."
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
        }

        FinalizeBulkResult(result);
        return result;
    }

    public async Task<BulkAcademicOperationResultDto?> BulkAssignCurriculumAsync(
        BulkCurriculumAssignmentRequestDto request,
        int userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);
        if (scope.Departments.All(department => department.DepartmentId != request.DepartmentId))
            return null;

        var normalizedIds = request.StudentIds.Distinct().ToList();
        var result = new BulkAcademicOperationResultDto { RequestedCount = normalizedIds.Count };

        if (normalizedIds.Count == 0)
        {
            result.Errors.Add("En az bir ogrenci secilmelidir.");
            return result;
        }

        var targetCurriculum = await _context.Curricula
            .Include(curriculum => curriculum.Program)
            .FirstOrDefaultAsync(curriculum =>
                curriculum.Id == request.CurriculumId
                && curriculum.Program.DepartmentId == request.DepartmentId,
                cancellationToken);

        if (targetCurriculum == null)
        {
            result.Errors.Add("Secilen mufredat bu bolume ait degil veya bulunamadi.");
            return result;
        }

        // Hedef müfredat statü kısıtı yok (Proliz paritesi): Taslak/Yayında/Arşiv hepsi atanabilir.

        IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await using (transaction)
        {
            var studentPrograms = await _context.StudentPrograms
                .Include(studentProgram => studentProgram.Student)
                .Include(studentProgram => studentProgram.Program)
                .Include(studentProgram => studentProgram.Curriculum)
                .Where(studentProgram => normalizedIds.Contains(studentProgram.StudentId)
                    && studentProgram.IsActive
                    && studentProgram.Type == StudentProgramType.Main)
                .ToListAsync(cancellationToken);

            var users = await LoadUsersForStudentsAsync(studentPrograms.Select(sp => sp.Student), cancellationToken);
            foreach (var studentId in normalizedIds)
            {
                var studentProgram = studentPrograms.FirstOrDefault(sp =>
                    sp.StudentId == studentId && sp.Program.DepartmentId == request.DepartmentId);

                if (studentProgram == null)
                {
                    result.Results.Add(FailedResult(studentId, "Ogrenci bu bolumun aktif ana programinda bulunamadi."));
                    continue;
                }

                var student = studentProgram.Student;
                var studentName = BuildUserFullName(users.GetValueOrDefault(student.UserId), student.StudentNumber);

                if (studentProgram.ProgramId != targetCurriculum.ProgramId)
                {
                    result.Results.Add(new BulkAcademicOperationResultItemDto
                    {
                        StudentId = student.Id,
                        StudentNumber = student.StudentNumber,
                        StudentName = studentName,
                        Success = false,
                        Message = "Secilen mufredat ogrencinin programina ait degil."
                    });
                    continue;
                }

                if (student.Status is StudentStatus.Graduated or StudentStatus.Dismissed)
                {
                    result.Results.Add(new BulkAcademicOperationResultItemDto
                    {
                        StudentId = student.Id,
                        StudentNumber = student.StudentNumber,
                        StudentName = studentName,
                        Success = false,
                        Message = "Mezun veya iliskigi kesilmis ogrencinin mufredati degistirilemez."
                    });
                    continue;
                }

                if (studentProgram.CurriculumId == targetCurriculum.Id)
                {
                    result.Results.Add(new BulkAcademicOperationResultItemDto
                    {
                        StudentId = student.Id,
                        StudentNumber = student.StudentNumber,
                        StudentName = studentName,
                        Success = false,
                        Message = "Ogrenci zaten bu mufredata atanmis."
                    });
                    continue;
                }

                studentProgram.CurriculumId = targetCurriculum.Id;
                result.Results.Add(new BulkAcademicOperationResultItemDto
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentNumber,
                    StudentName = studentName,
                    Success = true,
                    Message = "Mufredat atandi."
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
        }

        _forecastCacheInvalidator.InvalidateForecastContext();
        FinalizeBulkResult(result);
        return result;
    }
}
