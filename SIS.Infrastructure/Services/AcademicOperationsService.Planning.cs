using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Planlama özeti, bölüm sınıf listeleri ve müfredat genel bakışı sorguları.
/// </summary>
public partial class AcademicOperationsService
{
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
}
