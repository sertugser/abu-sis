using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicOperationsService : IAcademicOperationsService
{
    private readonly AppDbContext _context;
    private readonly IEnrollmentForecastService _forecastService;
    private readonly IEnrollmentForecastCacheInvalidator _forecastCacheInvalidator;
    private readonly ISchedulingService _schedulingService;
    private readonly ILogger<AcademicOperationsService> _logger;

    public AcademicOperationsService(
        AppDbContext context,
        IEnrollmentForecastService forecastService,
        IEnrollmentForecastCacheInvalidator forecastCacheInvalidator,
        ISchedulingService schedulingService,
        ILogger<AcademicOperationsService> logger)
    {
        _context = context;
        _forecastService = forecastService;
        _forecastCacheInvalidator = forecastCacheInvalidator;
        _schedulingService = schedulingService;
        _logger = logger;
    }

    public async Task<AcademicOperationsContextDto> GetContextAsync(int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);

        return new AcademicOperationsContextDto
        {
            RoleLabel = ResolveRoleLabel(roles),
            CanViewAllDepartments = scope.CanViewAllDepartments,
            DefaultDepartmentId = scope.Departments.FirstOrDefault()?.DepartmentId,
            AccessibleDepartments = scope.Departments,
            Notes = scope.Notes
        };
    }

    public async Task<AcademicPlanningSummaryDto?> GetPlanningSummaryAsync(
        int departmentId,
        int? termId,
        int userId,
        IReadOnlyCollection<string> roles,
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

        var term = await ResolveTermAsync(termId, cancellationToken);
        if (term == null)
            return new AcademicPlanningSummaryDto
            {
                DepartmentId = departmentId,
                DepartmentName = department.Name,
                Notes = { "Degerlendirilecek akademik donem bulunamadi." }
            };

        var forecastSummary = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        var scheduleValidation = await _schedulingService.ValidateExistingByDepartmentAsync(departmentId, term.Id, cancellationToken);

        var notes = new List<string>(scope.Notes);
        if (forecastSummary == null)
            notes.Add("Bu bolum icin tahminleme ozeti olusturulamadi.");

        return new AcademicPlanningSummaryDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TermId = term.Id,
            TermName = term.Name,
            ForecastSummary = forecastSummary,
            ScheduleValidation = scheduleValidation,
            Notes = notes.Distinct().ToList()
        };
    }

    public async Task<DepartmentClassListDto?> GetDepartmentClassListAsync(
        int departmentId,
        int? termId,
        int userId,
        IReadOnlyCollection<string> roles,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAccessibleDepartmentsAsync(userId, roles, cancellationToken);
        if (scope.Departments.All(department => department.DepartmentId != departmentId))
            return null;

        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(currentDepartment => currentDepartment.Id == departmentId, cancellationToken);

        var term = await ResolveTermAsync(termId, cancellationToken);
        if (department == null || term == null)
            return null;

        var sectionsQuery = _context.CourseSections
            .AsNoTracking()
            .Include(section => section.Course)
            .Include(section => section.Instructor)
            .Include(section => section.Classroom)
            .Include(section => section.Registrations)
                .ThenInclude(registration => registration.Student)
            .Where(section => section.TermId == term.Id)
            // C11: Department isolation — filter by course department only
            .Where(section => section.Course.DepartmentId == departmentId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            sectionsQuery = sectionsQuery.Where(section =>
                section.Course.Code.ToLower().Contains(normalizedSearch)
                || section.Course.Name.ToLower().Contains(normalizedSearch)
                || (section.Classroom != null && section.Classroom.Name.ToLower().Contains(normalizedSearch)));
        }

        var sections = await sectionsQuery
            .OrderBy(section => section.Course.Code)
            .ThenBy(section => section.Id)
            .ToListAsync(cancellationToken);

        var instructorUserIds = sections
            .Where(section => section.Instructor != null)
            .Select(section => section.Instructor!.UserId)
            .Distinct()
            .ToList();

        var studentUserIds = sections
            .SelectMany(section => section.Registrations.Select(registration => registration.Student.UserId))
            .Distinct()
            .ToList();

        var userMap = await _context.Users
            .AsNoTracking()
            .Where(user => instructorUserIds.Contains(user.Id) || studentUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        // Load SectionSchedule records for schedule display
        var classSectionIds = sections.Select(s => s.Id).ToList();
        var sectionScheduleRecords = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => classSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(cancellationToken);
        var scheduleMap = sectionScheduleRecords
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sectionDtos = sections
            .Select(section => MapSectionToDto(section, userMap, scheduleMap))
            .ToList();

        // Madde 14: Cross-list peer kodlarını doldur ve grup içindeki öğrencileri birleştir
        var groupIds = sectionDtos
            .Where(s => s.CrossListGroupId.HasValue)
            .Select(s => s.CrossListGroupId!.Value)
            .Distinct()
            .ToList();
        if (groupIds.Count > 0)
        {
            // Grup üyelerinin course kodlarını al (bu sorgu mevcut sections listesinin dışına taşar,
            // çünkü bazı üyeler başka bölümde açılmış olabilir)
            var peerSections = await _context.CourseSections
                .AsNoTracking()
                .Include(cs => cs.Course)
                .Include(cs => cs.Registrations).ThenInclude(r => r.Student)
                .Where(cs => cs.CrossListGroupId != null && groupIds.Contains(cs.CrossListGroupId!.Value))
                .ToListAsync(cancellationToken);

            var peersByGroup = peerSections
                .GroupBy(cs => cs.CrossListGroupId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var dto in sectionDtos)
            {
                if (!dto.CrossListGroupId.HasValue) continue;
                if (!peersByGroup.TryGetValue(dto.CrossListGroupId.Value, out var peers)) continue;

                // Peer kodlar (kendisi hariç)
                dto.CrossListPeerCodes = peers
                    .Where(p => p.Id != dto.SectionId)
                    .Select(p => p.Course.Code)
                    .Distinct()
                    .ToList();

                // Tek roster: gruptaki tüm section'ların öğrencileri bu section'ın listesine eklenir
                // (aynı section'ı tekrar ekleme)
                var currentIds = dto.Students.Select(s => s.StudentId).ToHashSet();
                foreach (var peer in peers.Where(p => p.Id != dto.SectionId))
                {
                    var peerRegs = peer.Registrations
                        .Where(r => r.Status != RegistrationStatus.Dropped && r.Status != RegistrationStatus.Draft)
                        .Where(r => !currentIds.Contains(r.StudentId));
                    foreach (var r in peerRegs)
                    {
                        var user = userMap.GetValueOrDefault(r.Student.UserId);
                        dto.Students.Add(new DepartmentSectionStudentDto
                        {
                            StudentId = r.StudentId,
                            StudentNumber = r.Student.StudentNumber,
                            StudentName = user == null
                                ? r.Student.StudentNumber
                                : $"{user.FirstName} {user.LastName}".Trim(),
                            Email = user?.Email ?? string.Empty,
                            Status = r.Status.ToString(),
                            Grade = r.Grade?.ToString(),
                            EnrolledCourseCode = peer.Course.Code,
                        });
                        currentIds.Add(r.StudentId);
                    }
                }
                // Öğrencileri yeniden sıralayalım
                dto.Students = dto.Students
                    .OrderBy(s => s.StudentName)
                    .ThenBy(s => s.StudentNumber)
                    .ToList();
                dto.ActiveStudentCount = dto.Students.Count;
            }
        }

        var notes = new List<string>(scope.Notes);
        if (sectionDtos.Any(section => string.IsNullOrWhiteSpace(section.ScheduleSummary)))
            notes.Add("Bazi sectionlarda schedule tanimi henuz tamamlanmamis.");
        if (sectionDtos.Any(section => string.IsNullOrWhiteSpace(section.ClassroomName)))
            notes.Add("Bazi sectionlarda derslik atamasi eksik.");

        return new DepartmentClassListDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TermId = term.Id,
            TermName = term.Name,
            TotalSections = sectionDtos.Count,
            TotalStudents = sectionDtos.Sum(section => section.ActiveStudentCount),
            Sections = sectionDtos,
            Notes = notes.Distinct().ToList()
        };
    }

    public async Task<DepartmentCurriculumOverviewDto?> GetCurriculumOverviewAsync(
        int departmentId,
        int userId,
        IReadOnlyCollection<string> roles,
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

        var programs = await _context.AcademicPrograms
            .AsNoTracking()
            .Include(program => program.Curricula)
                .ThenInclude(curriculum => curriculum.CurriculumCourses)
                    .ThenInclude(curriculumCourse => curriculumCourse.Course)
            .Where(program => program.DepartmentId == departmentId)
            .OrderBy(program => program.Name)
            .ToListAsync(cancellationToken);

        var programDtos = programs
            .SelectMany(program => program.Curricula.Select(curriculum => new { Program = program, Curriculum = curriculum }))
            .Select(item =>
            {
                var curriculumCourses = item.Curriculum.CurriculumCourses
                    .Where(curriculumCourse => curriculumCourse.SuggestedSemester > 0)
                    .OrderBy(curriculumCourse => curriculumCourse.SuggestedSemester)
                    .ThenBy(curriculumCourse => curriculumCourse.Course.Code)
                    .ToList();

                var semesterSummaries = curriculumCourses
                    .GroupBy(curriculumCourse => curriculumCourse.SuggestedSemester)
                    .Select(group => new CurriculumSemesterOverviewDto
                    {
                        SemesterNumber = group.Key,
                        Label = BuildSemesterLabel(group.Key),
                        CourseCount = group.Count(),
                        Credits = group.Sum(curriculumCourse => curriculumCourse.Course.TheoryHours + curriculumCourse.Course.LabHours),
                        Ects = group.Sum(curriculumCourse => curriculumCourse.Course.Ects),
                        CompulsoryCourseCount = group.Count(curriculumCourse => curriculumCourse.IsCompulsory),
                        ElectiveCourseCount = group.Count(curriculumCourse => !curriculumCourse.IsCompulsory)
                    })
                    .OrderBy(summary => summary.SemesterNumber)
                    .ToList();

                return new ProgramCurriculumOverviewDto
                {
                    ProgramId = item.Program.Id,
                    ProgramName = item.Program.Name,
                    ProgramCode = item.Program.Code,
                    CurriculumId = item.Curriculum.Id,
                    CurriculumVersion = item.Curriculum.Version,
                    IsActiveCurriculum = item.Curriculum.IsActive,
                    TotalCourses = curriculumCourses.Count,
                    TotalCredits = curriculumCourses.Sum(curriculumCourse => curriculumCourse.Course.TheoryHours + curriculumCourse.Course.LabHours),
                    TotalEcts = curriculumCourses.Sum(curriculumCourse => curriculumCourse.Course.Ects),
                    CompulsoryCourseCount = curriculumCourses.Count(curriculumCourse => curriculumCourse.IsCompulsory),
                    ElectiveCourseCount = curriculumCourses.Count(curriculumCourse => !curriculumCourse.IsCompulsory),
                    SemesterSummaries = semesterSummaries
                };
            })
            .OrderBy(program => program.ProgramName)
            .ThenByDescending(program => program.IsActiveCurriculum)
            .ThenByDescending(program => program.CurriculumVersion)
            .ToList();

        var notes = new List<string>(scope.Notes);
        if (programDtos.All(program => !program.IsActiveCurriculum))
            notes.Add("Bolumde aktif isaretlenmis mufredat bulunmuyor.");

        return new DepartmentCurriculumOverviewDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            ProgramCount = programs.Count,
            ActiveCurriculumCount = programDtos.Count(program => program.IsActiveCurriculum),
            TotalCourses = programDtos.Sum(program => program.TotalCourses),
            TotalEcts = programDtos.Sum(program => program.TotalEcts),
            Programs = programDtos,
            Notes = notes.Distinct().ToList()
        };
    }

    public async Task<DepartmentStudentListDto?> GetDepartmentStudentsAsync(
        int departmentId,
        int userId,
        IReadOnlyCollection<string> roles,
        string? search = null,
        string? status = null,
        int? programId = null,
        int? curriculumId = null,
        int? advisorId = null,
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

        var query = _context.StudentPrograms
            .AsNoTracking()
            .Include(studentProgram => studentProgram.Student)
                .ThenInclude(student => student.Advisor)
            .Include(studentProgram => studentProgram.Program)
            .Include(studentProgram => studentProgram.Curriculum)
            .Where(studentProgram => studentProgram.IsActive
                && studentProgram.Type == StudentProgramType.Main
                && studentProgram.Program.DepartmentId == departmentId);

        if (programId.HasValue)
            query = query.Where(studentProgram => studentProgram.ProgramId == programId.Value);

        if (curriculumId.HasValue)
            query = query.Where(studentProgram => studentProgram.CurriculumId == curriculumId.Value);

        if (advisorId.HasValue)
            query = query.Where(studentProgram => studentProgram.Student.AdvisorId == advisorId.Value);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<StudentStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(studentProgram => studentProgram.Student.Status == parsedStatus);
        }

        var studentPrograms = await query
            .OrderBy(studentProgram => studentProgram.Program.Code)
            .ThenBy(studentProgram => studentProgram.Student.StudentNumber)
            .ToListAsync(cancellationToken);

        var userIds = studentPrograms
            .Select(studentProgram => studentProgram.Student.UserId)
            .Concat(studentPrograms
                .Where(studentProgram => studentProgram.Student.Advisor != null)
                .Select(studentProgram => studentProgram.Student.Advisor!.UserId))
            .Distinct()
            .ToList();

        var userMap = await _context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var filtered = studentPrograms;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            filtered = studentPrograms
                .Where(studentProgram =>
                {
                    var user = userMap.GetValueOrDefault(studentProgram.Student.UserId);
                    var fullName = user == null ? string.Empty : $"{user.FirstName} {user.LastName}".Trim();
                    return studentProgram.Student.StudentNumber.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                        || fullName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                        || (user?.Email?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) == true);
                })
                .ToList();
        }

        var students = filtered
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
                    ProgramId = studentProgram.ProgramId,
                    ProgramName = studentProgram.Program.Name,
                    ProgramCode = studentProgram.Program.Code,
                    CurriculumId = studentProgram.CurriculumId,
                    CurriculumVersion = studentProgram.Curriculum.Version,
                    CurriculumName = studentProgram.Curriculum.Name ?? studentProgram.Curriculum.Version,
                    AdvisorId = advisor?.Id,
                    AdvisorName = advisor == null
                        ? null
                        : $"{advisor.Title} {BuildUserFullName(advisorUser, $"#{advisor.Id}")}".Trim(),
                };
            })
            .OrderBy(student => student.StudentName)
            .ThenBy(student => student.StudentNumber)
            .ToList();

        var notes = new List<string>(scope.Notes);
        if (students.Any(student => student.AdvisorId == null))
            notes.Add("Danismani atanmamis ogrenciler var.");

        return new DepartmentStudentListDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TotalStudents = students.Count,
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

        if (targetCurriculum.Status == CurriculumStatus.Archived)
        {
            result.Errors.Add("Arsivlenmis mufredat ogrencilere atanamaz.");
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

    private async Task<Dictionary<int, ApplicationUser>> LoadUsersForStudentsAsync(
        IEnumerable<Student> students,
        CancellationToken cancellationToken)
    {
        var userIds = students
            .Select(student => student.UserId)
            .Distinct()
            .ToList();

        return await _context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
    }

    private static string BuildUserFullName(ApplicationUser? user, string fallback)
    {
        if (user == null)
            return fallback;

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? fallback : fullName;
    }

    private static BulkAcademicOperationResultItemDto FailedResult(int studentId, string message) =>
        new()
        {
            StudentId = studentId,
            Success = false,
            Message = message
        };

    private static void FinalizeBulkResult(BulkAcademicOperationResultDto result)
    {
        result.SuccessCount = result.Results.Count(item => item.Success);
        result.FailedCount = result.Results.Count(item => !item.Success);
    }

    private async Task<AcademicTerm?> ResolveTermAsync(int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue)
        {
            return await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(term => term.Id == termId.Value, cancellationToken);
        }

        var now = DateTime.UtcNow;
        return await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(term => !term.IsCancelled && term.IsActivated && term.TermStart <= now && term.TermEnd >= now, cancellationToken);
    }

    private async Task<DepartmentScopeResolution> ResolveAccessibleDepartmentsAsync(
        int userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .OrderBy(department => department.Name)
                .Select(department => new AccessibleDepartmentDto
                {
                    DepartmentId = department.Id,
                    DepartmentName = department.Name,
                    DepartmentCode = department.Code
                })
                .ToListAsync(cancellationToken);

            return new DepartmentScopeResolution
            {
                CanViewAllDepartments = true,
                Departments = departments
            };
        }

        if (roles.Contains("Dean"))
        {
            var facultyId = await _context.Instructors
                .AsNoTracking()
                .Where(instructor => instructor.UserId == userId)
                .Select(instructor => (int?)instructor.Department.FacultyId)
                .FirstOrDefaultAsync(cancellationToken);

            if (facultyId.HasValue)
            {
                var departments = await _context.Departments
                    .AsNoTracking()
                    .Where(department => department.FacultyId == facultyId.Value)
                    .OrderBy(department => department.Name)
                    .Select(department => new AccessibleDepartmentDto
                    {
                        DepartmentId = department.Id,
                        DepartmentName = department.Name,
                        DepartmentCode = department.Code
                    })
                    .ToListAsync(cancellationToken);

                return new DepartmentScopeResolution
                {
                    CanViewAllDepartments = false,
                    Departments = departments,
                    Notes = ["Dekan görünümü bağlı fakültenizdeki bölümlerle sınırlıdır."]
                };
            }

            return new DepartmentScopeResolution
            {
                CanViewAllDepartments = false,
                Notes = ["Dekan kullanıcısı için bağlı fakülte bulunamadı."]
            };
        }

        if (roles.Contains("DepartmentManager") || roles.Contains("Instructor") || roles.Contains("AcademicAdvisor"))
        {
            var department = await _context.Instructors
                .AsNoTracking()
                .Where(instructor => instructor.UserId == userId)
                .Join(
                    _context.Departments.AsNoTracking(),
                    instructor => instructor.DepartmentId,
                    department => department.Id,
                    (_, department) => new AccessibleDepartmentDto
                    {
                        DepartmentId = department.Id,
                        DepartmentName = department.Name,
                        DepartmentCode = department.Code
                    })
                .FirstOrDefaultAsync(cancellationToken);

            if (department != null)
            {
                var note = roles.Contains("DepartmentManager")
                    ? "Bölüm başkanı görünümü kendi bölümünüzle sınırlıdır."
                    : "Bölüm işlemleri görünümü bağlı olduğunuz bölümle sınırlıdır.";

                return new DepartmentScopeResolution
                {
                    CanViewAllDepartments = false,
                    Departments = [department],
                    Notes = [note]
                };
            }

            return new DepartmentScopeResolution
            {
                CanViewAllDepartments = false,
                Notes = ["Bu kullanıcı için bağlı bölüm bulunamadı."]
            };
        }

        return new DepartmentScopeResolution
        {
            CanViewAllDepartments = false,
            Notes = ["Bu kullanici icin akademik operasyon kapsamı tanimlanmamis."]
        };
    }

    private static string ResolveRoleLabel(IReadOnlyCollection<string> roles)
    {
        if (roles.Contains("SystemAdministrator"))
            return "Sistem Yönetimi";

        if (roles.Contains("AdministrativeStaff"))
            return "Akademik İşler";

        if (roles.Contains("Dean"))
            return "Dekanlık";

        if (roles.Contains("DepartmentManager"))
            return "Bölüm Başkanlığı";

        if (roles.Contains("AcademicAdvisor"))
            return "Akademik Danışmanlık";

        if (roles.Contains("Instructor"))
            return "Öğretim Elemanı";

        return "Akademik Operasyonlar";
    }

    private static string BuildSemesterLabel(int semesterNumber)
    {
        var classYear = (semesterNumber - 1) / 2 + 1;
        var termLabel = semesterNumber % 2 == 1 ? "Güz" : "Bahar";
        return $"{classYear}. Sınıf {termLabel}";
    }

    private static DepartmentSectionClassListDto MapSectionToDto(
        CourseSection section,
        Dictionary<int, ApplicationUser> userMap,
        Dictionary<int, List<SectionSchedule>> scheduleMap)
    {
        var activeRegistrations = section.Registrations
            .Where(r => r.Status != RegistrationStatus.Dropped && r.Status != RegistrationStatus.Draft)
            .ToList();

        var studentDtos = activeRegistrations
            .Select(r =>
            {
                var user = userMap.GetValueOrDefault(r.Student.UserId);
                return new DepartmentSectionStudentDto
                {
                    StudentId = r.StudentId,
                    StudentNumber = r.Student.StudentNumber,
                    StudentName = user == null
                        ? r.Student.StudentNumber
                        : $"{user.FirstName} {user.LastName}".Trim(),
                    Email = user?.Email ?? string.Empty,
                    Status = r.Status.ToString(),
                    Grade = r.Grade?.ToString(),
                    EnrolledCourseCode = section.Course.Code,
                };
            })
            .OrderBy(s => s.StudentName)
            .ThenBy(s => s.StudentNumber)
            .ToList();

        var instructorName = string.Empty;
        if (section.Instructor != null)
        {
            var instructorUser = userMap.GetValueOrDefault(section.Instructor.UserId);
            instructorName = instructorUser == null
                ? section.Instructor.Title
                : $"{section.Instructor.Title} {instructorUser.FirstName} {instructorUser.LastName}".Trim();
        }

        var sectionScheds = scheduleMap.GetValueOrDefault(section.Id) ?? new List<SectionSchedule>();

        return new DepartmentSectionClassListDto
        {
            SectionId = section.Id,
            SectionNumber = section.SectionNumber,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            InstructorName = instructorName,
            ClassroomName = section.Classroom?.Name ?? string.Empty,
            ScheduleSummary = ScheduleHelper.FormatSchedule(sectionScheds),
            Capacity = section.Capacity,
            InternalQuota = section.InternalQuota,
            ExternalQuota = section.ExternalQuota,
            EnrolledCount = section.Registrations.Count,
            ActiveStudentCount = activeRegistrations.Count,
            IsActive = section.IsActive,
            Students = studentDtos,
            CrossListGroupId = section.CrossListGroupId,
            // CrossListPeerCodes servis tarafından post-process edilir (tek section'ın full context'i yok)
        };
    }

    private sealed class DepartmentScopeResolution
    {
        public bool CanViewAllDepartments { get; init; }
        public List<AccessibleDepartmentDto> Departments { get; init; } = new();
        public List<string> Notes { get; init; } = new();
    }
}
