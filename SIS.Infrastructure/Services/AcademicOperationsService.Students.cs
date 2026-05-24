using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Bölüm öğrenci listesi ve toplu işlem hedefi olarak kullanılan müfredat seçenekleri.
/// </summary>
public partial class AcademicOperationsService
{
    private const int DefaultStudentPageSize = 50;
    private const int MaxStudentPageSize = 200;

    public async Task<DepartmentStudentListDto?> GetDepartmentStudentsAsync(
        int departmentId,
        int userId,
        IReadOnlyCollection<string> roles,
        string? search = null,
        string? status = null,
        int? programId = null,
        int? curriculumId = null,
        int? advisorId = null,
        bool? hasAdvisor = null,
        bool? hasFailingCourse = null,
        int? classYear = null,
        string? studentNoFrom = null,
        string? studentNoTo = null,
        DateTime? enrollmentFrom = null,
        DateTime? enrollmentTo = null,
        int page = 1,
        int pageSize = DefaultStudentPageSize,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);
        if (scope.Departments.All(department => department.DepartmentId != departmentId))
            return null;

        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(currentDepartment => currentDepartment.Id == departmentId, cancellationToken);

        if (department == null)
            return null;

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultStudentPageSize;
        if (pageSize > MaxStudentPageSize) pageSize = MaxStudentPageSize;

        var query = _context.StudentPrograms
            .AsNoTracking()
            .Where(studentProgram => studentProgram.IsActive
                && studentProgram.Type == StudentProgramType.Main
                && studentProgram.Program.DepartmentId == departmentId);

        if (programId.HasValue)
            query = query.Where(studentProgram => studentProgram.ProgramId == programId.Value);

        if (curriculumId.HasValue)
            query = query.Where(studentProgram => studentProgram.CurriculumId == curriculumId.Value);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<StudentStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(studentProgram => studentProgram.Student.Status == parsedStatus);
        }

        // Sınıf: getStudentClassYear ile aynı kural — floor(CompletedSemesters/2)+1, 4'te cap.
        if (classYear.HasValue)
        {
            var cy = classYear.Value;
            if (cy >= 4)
                query = query.Where(studentProgram => studentProgram.Student.CompletedSemesters >= 6);
            else if (cy >= 1)
                query = query.Where(studentProgram =>
                    studentProgram.Student.CompletedSemesters >= (cy - 1) * 2
                    && studentProgram.Student.CompletedSemesters <= cy * 2 - 1);
        }

        // Öğrenci No Aralığı (lexicografik — öğrenci numaraları eşit uzunlukta numerik string).
        if (!string.IsNullOrWhiteSpace(studentNoFrom))
            query = query.Where(studentProgram => string.Compare(studentProgram.Student.StudentNumber, studentNoFrom) >= 0);
        if (!string.IsNullOrWhiteSpace(studentNoTo))
            query = query.Where(studentProgram => string.Compare(studentProgram.Student.StudentNumber, studentNoTo) <= 0);

        // Kayıt Tarih Aralığı (StudentProgram.EnrollmentDate).
        if (enrollmentFrom.HasValue)
            query = query.Where(studentProgram => studentProgram.EnrollmentDate >= enrollmentFrom.Value);
        if (enrollmentTo.HasValue)
            query = query.Where(studentProgram => studentProgram.EnrollmentDate <= enrollmentTo.Value);

        // Ders Başarı Durumu: FF/NA notu olan kayıt = başarısız ders.
        if (hasFailingCourse.HasValue)
        {
            if (hasFailingCourse.Value)
                query = query.Where(studentProgram => studentProgram.Student.Registrations
                    .Any(registration => registration.Grade == GradeCode.FF || registration.Grade == GradeCode.NA));
            else
                query = query.Where(studentProgram => !studentProgram.Student.Registrations
                    .Any(registration => registration.Grade == GradeCode.FF || registration.Grade == GradeCode.NA));
        }

        // İsim/email araması Users tablosuna correlated EXISTS ile çevrilir — sayfalama DB tarafında doğru çalışsın diye.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(studentProgram =>
                studentProgram.Student.StudentNumber.Contains(normalizedSearch)
                || _context.Users.Any(user => user.Id == studentProgram.Student.UserId
                    && ((user.FirstName + " " + user.LastName).Contains(normalizedSearch)
                        || (user.Email != null && user.Email.Contains(normalizedSearch)))));
        }

        // Danışmansız öğrenci sayısı — danışman filtresinden ÖNCE hesaplanır ki
        // "Mevcut danışman" filtresi toggle edildikçe stat sabit kalsın.
        var withoutAdvisorCount = await query.CountAsync(
            studentProgram => studentProgram.Student.AdvisorId == null, cancellationToken);

        // Mevcut danışman filtresi: belirli bir danışman ya da danışmansız (hasAdvisor=false).
        if (advisorId.HasValue)
            query = query.Where(studentProgram => studentProgram.Student.AdvisorId == advisorId.Value);
        else if (hasAdvisor.HasValue)
            query = hasAdvisor.Value
                ? query.Where(studentProgram => studentProgram.Student.AdvisorId != null)
                : query.Where(studentProgram => studentProgram.Student.AdvisorId == null);

        var total = await query.CountAsync(cancellationToken);

        var pageItems = await query
            .Include(studentProgram => studentProgram.Student)
                .ThenInclude(student => student.Advisor)
            .Include(studentProgram => studentProgram.Program)
                .ThenInclude(program => program.Department)
                    .ThenInclude(dept => dept.Faculty)
            .Include(studentProgram => studentProgram.Curriculum)
            .OrderBy(studentProgram => studentProgram.Program.Code)
            .ThenBy(studentProgram => studentProgram.Student.StudentNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = pageItems
            .Select(studentProgram => studentProgram.Student.UserId)
            .Concat(pageItems
                .Where(studentProgram => studentProgram.Student.Advisor != null)
                .Select(studentProgram => studentProgram.Student.Advisor!.UserId))
            .Distinct()
            .ToList();

        var userMap = await _context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var students = pageItems
            .Select(studentProgram =>
            {
                var studentUser = userMap.GetValueOrDefault(studentProgram.Student.UserId);
                var advisor = studentProgram.Student.Advisor;
                var advisorUser = advisor == null ? null : userMap.GetValueOrDefault(advisor.UserId);

                return new DepartmentStudentListItemDto
                {
                    StudentId = studentProgram.StudentId,
                    StudentNumber = studentProgram.Student.StudentNumber,
                    StudentName = BuildUserFullName(studentUser, studentProgram.Student.StudentNumber),
                    Email = studentUser?.Email ?? string.Empty,
                    Status = studentProgram.Student.Status.ToString(),
                    Cgpa = studentProgram.Student.Cgpa,
                    CompletedSemesters = studentProgram.Student.CompletedSemesters,
                    FacultyId = studentProgram.Program.Department.FacultyId,
                    FacultyName = studentProgram.Program.Department.Faculty.Name,
                    ProgramId = studentProgram.ProgramId,
                    ProgramName = studentProgram.Program.Name,
                    ProgramCode = studentProgram.Program.Code,
                    CurriculumId = studentProgram.CurriculumId,
                    CurriculumVersion = studentProgram.Curriculum.Version,
                    CurriculumName = studentProgram.Curriculum.Name ?? studentProgram.Curriculum.Version,
                    EnrollmentDate = studentProgram.EnrollmentDate,
                    AdvisorId = advisor?.Id,
                    AdvisorName = advisor == null
                        ? null
                        : $"{advisor.Title} {BuildUserFullName(advisorUser, $"#{advisor.Id}")}".Trim(),
                };
            })
            .ToList();

        var notes = new List<string>(scope.Notes);
        if (students.Any(student => student.AdvisorId == null))
            notes.Add("Danismani atanmamis ogrenciler var.");

        return new DepartmentStudentListDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TotalStudents = total,
            WithoutAdvisorCount = withoutAdvisorCount,
            Page = page,
            PageSize = pageSize,
            Students = students,
            Notes = notes.Distinct().ToList()
        };
    }

    public async Task<List<DepartmentCurriculumOptionDto>?> GetDepartmentCurriculumOptionsAsync(
        int departmentId,
        int userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);
        if (scope.Departments.All(department => department.DepartmentId != departmentId))
            return null;

        return await _context.Curricula
            .AsNoTracking()
            .Include(curriculum => curriculum.Program)
            .Where(curriculum => curriculum.Program.DepartmentId == departmentId)
            .OrderBy(curriculum => curriculum.Program.Code)
            .ThenByDescending(curriculum => curriculum.IsActive)
            .ThenByDescending(curriculum => curriculum.Version)
            .Select(curriculum => new DepartmentCurriculumOptionDto
            {
                CurriculumId = curriculum.Id,
                ProgramId = curriculum.ProgramId,
                ProgramName = curriculum.Program.Name,
                ProgramCode = curriculum.Program.Code,
                CurriculumVersion = curriculum.Version,
                CurriculumName = curriculum.Name ?? curriculum.Version,
                IsActive = curriculum.IsActive,
                Status = curriculum.Status,
                ActiveStudentCount = _context.StudentPrograms.Count(studentProgram =>
                    studentProgram.CurriculumId == curriculum.Id
                    && studentProgram.IsActive
                    && studentProgram.Type == StudentProgramType.Main)
            })
            .ToListAsync(cancellationToken);
    }
}
