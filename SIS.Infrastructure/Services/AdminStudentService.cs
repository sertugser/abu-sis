using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminStudentService : IAdminStudentService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminStudentService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<PagedResult<AdminStudentListItemDto>> GetStudentsAsync(AdminStudentFilterDto filter)
    {
        var query = _context.Students
            .Include(s => s.Advisor)
                .ThenInclude(a => a != null ? a.Department : null)
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            var matchingUserIds = await _context.Set<ApplicationUser>()
                .Where(u => u.FirstName.ToLower().Contains(s) ||
                            u.LastName.ToLower().Contains(s) ||
                            (u.Email ?? "").ToLower().Contains(s))
                .Select(u => u.Id)
                .ToListAsync();

            query = query.Where(st =>
                st.StudentNumber.ToLower().Contains(s) ||
                matchingUserIds.Contains(st.UserId));
        }

        if (filter.ProgramId.HasValue)
            query = query.Where(s => s.StudentPrograms.Any(sp => sp.ProgramId == filter.ProgramId && sp.IsActive));

        if (filter.DepartmentId.HasValue)
            query = query.Where(s => s.StudentPrograms.Any(sp =>
                sp.IsActive && sp.Program.DepartmentId == filter.DepartmentId));

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<StudentStatus>(filter.Status, out var statusEnum))
            query = query.Where(s => s.Status == statusEnum);

        if (filter.MinCgpa.HasValue)
            query = query.Where(s => s.Cgpa >= filter.MinCgpa.Value);

        if (filter.MaxCgpa.HasValue)
            query = query.Where(s => s.Cgpa <= filter.MaxCgpa.Value);

        var totalCount = await query.CountAsync();

        var students = await query
            .OrderBy(s => s.StudentNumber)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var userIds = students.Select(s => s.UserId).ToList();

        var users = await _context.Set<ApplicationUser>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var items = students.Select(s =>
        {
            users.TryGetValue(s.UserId, out var user);

            var activeProgram = s.StudentPrograms.FirstOrDefault(sp => sp.IsActive);

            var advisorUser = s.Advisor != null
                ? _context.Set<ApplicationUser>().FirstOrDefault(u => u.Id == s.Advisor.UserId)
                : null;

            return new AdminStudentListItemDto(
                s.Id,
                s.StudentNumber,
                user?.FirstName ?? "",
                user?.LastName ?? "",
                user?.Email ?? "",
                s.Cgpa,
                s.CompletedSemesters,
                s.Status.ToString(),
                activeProgram?.Program?.Name ?? "-",
                activeProgram?.Program?.Code ?? "-",
                activeProgram?.Program?.Department?.Name ?? "-",
                advisorUser != null ? $"{advisorUser.FirstName} {advisorUser.LastName}" : null
            );
        }).ToList();

        return new PagedResult<AdminStudentListItemDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<AdminStudentDetailDto?> GetStudentByIdAsync(int id)
    {
        var student = await _context.Students
            .Include(s => s.Advisor)
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return null;

        var user = await _context.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == student.UserId);

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);

        ApplicationUser? advisorUser = null;
        if (student.Advisor != null)
        {
            advisorUser = await _context.Set<ApplicationUser>()
                .FirstOrDefaultAsync(u => u.Id == student.Advisor.UserId);
        }

        return new AdminStudentDetailDto(
            student.Id,
            student.StudentNumber,
            user?.FirstName ?? "",
            user?.LastName ?? "",
            user?.Email ?? "",
            student.Cgpa,
            student.Sgpa,
            student.CompletedSemesters,
            student.FrozenSemesterCount,
            student.Status.ToString(),
            student.IsScholarshipStudent,
            student.HasFinancialBlock,
            activeProgram?.Program?.Name ?? "-",
            activeProgram?.Program?.Code ?? "-",
            activeProgram?.Program?.Department?.Name ?? "-",
            advisorUser != null ? $"{advisorUser.FirstName} {advisorUser.LastName}" : null,
            student.AdvisorId
        );
    }

    public async Task<AdminStudentDetailDto> CreateStudentAsync(CreateStudentDto dto)
    {
        var program = await _context.AcademicPrograms
            .Include(p => p.Department)
            .FirstOrDefaultAsync(p => p.Id == dto.ProgramId)
            ?? throw new KeyNotFoundException($"Program bulunamadı: {dto.ProgramId}");

        var curriculum = await _context.Curricula
            .FirstOrDefaultAsync(c => c.ProgramId == dto.ProgramId)
            ?? throw new InvalidOperationException("Bu program için müfredat bulunamadı.");

        var studentNumber = dto.OverrideStudentNumber
            ?? await GenerateStudentNumberAsync(dto.EntryYear, program);

        var email = dto.OverrideEmail
            ?? GenerateStudentEmail(dto.FirstName, dto.LastName);

        int suffix = 1;
        var baseEmail = email;

        while (await _userManager.FindByEmailAsync(email) != null)
        {
            var atIdx = baseEmail.IndexOf('@');
            email = $"{baseEmail[..atIdx]}{suffix++}{baseEmail[atIdx..]}";
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);

        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, "Student");

        var student = new Student
        {
            UserId = user.Id,
            StudentNumber = studentNumber,
            NationalId = dto.NationalId,
            Status = StudentStatus.Active,
            Cgpa = 0,
            Sgpa = 0,
            CompletedSemesters = 0,
            FrozenSemesterCount = 0,
            WithdrawalCount = 0,
            IsScholarshipStudent = dto.IsScholarshipStudent,
            HasFinancialBlock = false,
            AdvisorId = dto.AdvisorId
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        _context.Set<StudentProgram>().Add(new StudentProgram
        {
            StudentId = student.Id,
            ProgramId = program.Id,
            CurriculumId = curriculum.Id,
            Type = StudentProgramType.Main,
            EnrollmentDate = new DateTime(dto.EntryYear, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        await _context.SaveChangesAsync();

        var detail = await GetStudentByIdAsync(student.Id);
        return detail!;
    }

    public async Task AssignAdvisorAsync(int studentId, int advisorId)
    {
        var student = await _context.Students.FindAsync(studentId)
            ?? throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");

        var studentDepartmentId = await _context.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
            .Where(sp =>
                sp.StudentId == studentId &&
                sp.IsActive &&
                sp.Type == StudentProgramType.Main)
            .Select(sp => (int?)sp.Program.DepartmentId)
            .FirstOrDefaultAsync();

        if (studentDepartmentId == null)
        {
            throw new InvalidOperationException("Öğrencinin aktif ana program/bölüm bilgisi bulunamadı.");
        }

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == advisorId)
            ?? throw new KeyNotFoundException($"Öğretim üyesi bulunamadı: {advisorId}");

        if (instructor.DepartmentId != studentDepartmentId.Value)
        {
            throw new InvalidOperationException("Öğrenciye yalnızca kendi bölümündeki öğretim üyesi danışman atanabilir.");
        }

        student.AdvisorId = instructor.Id;
        await _context.SaveChangesAsync();
    }

    public async Task<BulkAdvisorAssignmentPreviewResponseDto> PreviewBulkAdvisorAssignmentAsync(
    BulkAdvisorAssignmentPreviewRequestDto dto)
{
    var page = dto.Page < 1 ? 1 : dto.Page;
    var pageSize = dto.PageSize <= 0 ? 50 : Math.Min(dto.PageSize, 100);

    var department = await _context.Departments
        .AsNoTracking()
        .FirstOrDefaultAsync(d => d.Id == dto.DepartmentId)
        ?? throw new KeyNotFoundException($"Bölüm bulunamadı: {dto.DepartmentId}");

    AcademicProgram? selectedProgram = null;

    if (dto.ProgramId.HasValue)
    {
        selectedProgram = await _context.AcademicPrograms
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProgramId.Value)
            ?? throw new KeyNotFoundException($"Program bulunamadı: {dto.ProgramId.Value}");

        if (selectedProgram.DepartmentId != dto.DepartmentId)
            throw new InvalidOperationException("Seçilen program bu bölüme ait değil.");
    }

    var warnings = new List<string>();

    var studentsBaseQuery = _context.Students
        .AsNoTracking()
        .Where(s =>
            s.Status == StudentStatus.Active &&
            s.StudentPrograms.Any(sp =>
                sp.IsActive &&
                sp.Type == StudentProgramType.Main &&
                sp.Program.DepartmentId == dto.DepartmentId));

    if (dto.ProgramId.HasValue)
    {
        studentsBaseQuery = studentsBaseQuery.Where(s =>
            s.StudentPrograms.Any(sp =>
                sp.IsActive &&
                sp.Type == StudentProgramType.Main &&
                sp.ProgramId == dto.ProgramId.Value));
    }

    var totalStudentCount = await studentsBaseQuery.CountAsync();

    var alreadyAssignedCount = await studentsBaseQuery
        .CountAsync(s => s.AdvisorId.HasValue);

    var studentsToAssignQuery = dto.IncludeStudentsWithAdvisor
        ? studentsBaseQuery
        : studentsBaseQuery.Where(s => !s.AdvisorId.HasValue);

    if (!string.IsNullOrWhiteSpace(dto.StudentSearch))
    {
        var search = dto.StudentSearch.Trim().ToLower();
        var matchingUserIds = await _context.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u =>
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                (u.Email ?? "").ToLower().Contains(search))
            .Select(u => u.Id)
            .ToListAsync();

        studentsToAssignQuery = studentsToAssignQuery.Where(s =>
            s.StudentNumber.ToLower().Contains(search) ||
            matchingUserIds.Contains(s.UserId));
    }

    var advisorFilterId = dto.AdvisorFilterId.GetValueOrDefault();
    var advisorFilterType = dto.AdvisorFilterType?.Trim().ToLowerInvariant();

    if (advisorFilterId > 0 && advisorFilterType == "current")
    {
        studentsToAssignQuery = studentsToAssignQuery.Where(s => s.AdvisorId == advisorFilterId);
    }

    var totalAssignmentCount = await studentsToAssignQuery.CountAsync();

    var totalPages = totalAssignmentCount == 0
        ? 0
        : (int)Math.Ceiling(totalAssignmentCount / (double)pageSize);

    if (totalPages > 0 && page > totalPages)
        page = totalPages;

    var advisors = await _context.Instructors
        .AsNoTracking()
        .Include(i => i.Department)
        .Where(i => i.DepartmentId == dto.DepartmentId)
        .OrderBy(i => i.Id)
        .ToListAsync();

    if (advisors.Count == 0)
    {
        warnings.Add("Bu bölümde danışman olarak atanabilecek öğretim üyesi bulunamadı.");

        return new BulkAdvisorAssignmentPreviewResponseDto(
            department.Id,
            department.Name,
            selectedProgram?.Id,
            selectedProgram?.Name,
            totalStudentCount,
            0,
            alreadyAssignedCount,
            page,
            pageSize,
            totalAssignmentCount,
            totalPages,
            new List<BulkAdvisorCandidateDto>(),
            new List<BulkAdvisorAssignmentPreviewItemDto>(),
            warnings
        );
    }

    var advisorUserIds = advisors.Select(a => a.UserId).ToList();

    var advisorUsers = await _context.Set<ApplicationUser>()
        .AsNoTracking()
        .Where(u => advisorUserIds.Contains(u.Id))
        .ToDictionaryAsync(u => u.Id);

    var advisorIds = advisors.Select(a => a.Id).ToList();

    var currentAdvisorLoads = await _context.Students
        .AsNoTracking()
        .Where(s =>
            s.AdvisorId.HasValue &&
            advisorIds.Contains(s.AdvisorId.Value) &&
            s.StudentPrograms.Any(sp =>
                sp.IsActive &&
                sp.Type == StudentProgramType.Main &&
                sp.Program.DepartmentId == dto.DepartmentId))
        .Where(s => !dto.ProgramId.HasValue || s.StudentPrograms.Any(sp =>
            sp.IsActive &&
            sp.Type == StudentProgramType.Main &&
            sp.ProgramId == dto.ProgramId.Value))
        .GroupBy(s => s.AdvisorId!.Value)
        .Select(g => new
        {
            AdvisorId = g.Key,
            Count = g.Count()
        })
        .ToDictionaryAsync(x => x.AdvisorId, x => x.Count);

    if (totalAssignmentCount == 0)
    {
        warnings.Add(dto.IncludeStudentsWithAdvisor
            ? "Bu filtreye uygun öğrenci bulunamadı."
            : "Bu filtreye uygun danışmansız öğrenci bulunamadı. Mevcut danışmanlar korunuyor.");

        var emptyAdvisorDtos = advisors.Select(advisor =>
        {
            advisorUsers.TryGetValue(advisor.UserId, out var advisorUser);
            var currentCount = currentAdvisorLoads.GetValueOrDefault(advisor.Id);

            return new BulkAdvisorCandidateDto(
                advisor.Id,
                advisorUser != null ? $"{advisorUser.FirstName} {advisorUser.LastName}" : $"Danışman #{advisor.Id}",
                advisor.DepartmentId,
                advisor.Department?.Name ?? department.Name,
                currentCount,
                0,
                currentCount,
                advisor.IsAvailableForAdvisorAssignment != false,
                advisor.AdvisorAssignmentCapacity
            );
        }).ToList();

        return new BulkAdvisorAssignmentPreviewResponseDto(
            department.Id,
            department.Name,
            selectedProgram?.Id,
            selectedProgram?.Name,
            totalStudentCount,
            0,
            alreadyAssignedCount,
            page,
            pageSize,
            totalAssignmentCount,
            totalPages,
            emptyAdvisorDtos,
            new List<BulkAdvisorAssignmentPreviewItemDto>(),
            warnings
        );
    }

    var availableAdvisors = advisors
        .Where(a => a.IsAvailableForAdvisorAssignment != false)
        .Where(a => !a.AdvisorAssignmentCapacity.HasValue || a.AdvisorAssignmentCapacity.Value > 0)
        .ToList();

    if (availableAdvisors.Count == 0)
    {
        warnings.Add("Bu bölümde otomatik danışman ataması için uygun veya kontenjanı olan öğretim üyesi bulunamadı.");

        var advisorDtosWithoutAssignments = advisors.Select(advisor =>
        {
            advisorUsers.TryGetValue(advisor.UserId, out var advisorUser);
            var currentCount = currentAdvisorLoads.GetValueOrDefault(advisor.Id);

            return new BulkAdvisorCandidateDto(
                advisor.Id,
                advisorUser != null ? $"{advisorUser.FirstName} {advisorUser.LastName}" : $"Danışman #{advisor.Id}",
                advisor.DepartmentId,
                advisor.Department?.Name ?? department.Name,
                currentCount,
                0,
                currentCount,
                advisor.IsAvailableForAdvisorAssignment != false,
                advisor.AdvisorAssignmentCapacity
            );
        }).ToList();

        return new BulkAdvisorAssignmentPreviewResponseDto(
            department.Id,
            department.Name,
            selectedProgram?.Id,
            selectedProgram?.Name,
            totalStudentCount,
            0,
            alreadyAssignedCount,
            page,
            pageSize,
            totalAssignmentCount,
            totalPages,
            advisorDtosWithoutAssignments,
            new List<BulkAdvisorAssignmentPreviewItemDto>(),
            warnings
        );
    }

    var needsAssignedAdvisorFilter = advisorFilterId > 0 && advisorFilterType == "assigned";

    var studentsForPreviewQuery = studentsToAssignQuery
        .Include(s => s.StudentPrograms)
            .ThenInclude(sp => sp.Program)
                .ThenInclude(p => p.Department)
        .Include(s => s.Advisor)
        .OrderBy(s => s.CompletedSemesters)
        .ThenBy(s => s.StudentNumber);

    var studentsPage = needsAssignedAdvisorFilter
        ? await studentsForPreviewQuery.ToListAsync()
        : await studentsForPreviewQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    var studentUserIds = studentsPage.Select(s => s.UserId).ToList();

    var studentUsers = await _context.Set<ApplicationUser>()
        .AsNoTracking()
        .Where(u => studentUserIds.Contains(u.Id))
        .ToDictionaryAsync(u => u.Id);

    var currentAdvisorIds = studentsPage
        .Where(s => s.AdvisorId.HasValue)
        .Select(s => s.AdvisorId!.Value)
        .Distinct()
        .ToList();

    var currentAdvisorUsers = await _context.Instructors
        .AsNoTracking()
        .Where(i => currentAdvisorIds.Contains(i.Id))
        .Join(
            _context.Set<ApplicationUser>().AsNoTracking(),
            instructor => instructor.UserId,
            user => user.Id,
            (instructor, user) => new
            {
                InstructorId = instructor.Id,
                AdvisorName = $"{user.FirstName} {user.LastName}"
            })
        .ToDictionaryAsync(x => x.InstructorId, x => x.AdvisorName);

    var plannedLoads = advisors.ToDictionary(a => a.Id, _ => 0);

    var assignments = new List<BulkAdvisorAssignmentPreviewItemDto>();
    var skippedBecauseOfCapacity = 0;

    foreach (var student in studentsPage)
    {
        var activeProgram = student.StudentPrograms.First(sp =>
            sp.IsActive &&
            sp.Type == StudentProgramType.Main);

        var selectedAdvisor = availableAdvisors
            .Where(a =>
                !a.AdvisorAssignmentCapacity.HasValue ||
                plannedLoads[a.Id] < a.AdvisorAssignmentCapacity.Value)
            .OrderBy(a => currentAdvisorLoads.GetValueOrDefault(a.Id) + plannedLoads[a.Id])
            .ThenBy(a => plannedLoads[a.Id])
            .ThenBy(a => a.Id)
            .FirstOrDefault();

        if (selectedAdvisor == null)
        {
            skippedBecauseOfCapacity++;
            continue;
        }

        plannedLoads[selectedAdvisor.Id]++;

        studentUsers.TryGetValue(student.UserId, out var studentUser);
        advisorUsers.TryGetValue(selectedAdvisor.UserId, out var selectedAdvisorUser);

        string? currentAdvisorName = null;
        if (student.AdvisorId.HasValue)
            currentAdvisorUsers.TryGetValue(student.AdvisorId.Value, out currentAdvisorName);

        var reason = student.AdvisorId.HasValue
            ? "Mevcut danışman yeniden dağıtıma dahil edildi."
            : "Danışmansız öğrenci en uygun danışmana önerildi.";

        assignments.Add(new BulkAdvisorAssignmentPreviewItemDto(
            student.Id,
            student.StudentNumber,
            studentUser != null ? $"{studentUser.FirstName} {studentUser.LastName}" : $"Öğrenci #{student.Id}",
            activeProgram.ProgramId,
            activeProgram.Program?.Name ?? "-",
            activeProgram.Program?.DepartmentId ?? dto.DepartmentId,
            activeProgram.Program?.Department?.Name ?? department.Name,
            student.CompletedSemesters,
            student.AdvisorId,
            currentAdvisorName,
            selectedAdvisor.Id,
            selectedAdvisorUser != null ? $"{selectedAdvisorUser.FirstName} {selectedAdvisorUser.LastName}" : $"Danışman #{selectedAdvisor.Id}",
            reason
        ));
    }

    if (skippedBecauseOfCapacity > 0)
    {
        warnings.Add($"{skippedBecauseOfCapacity} öğrenci için kontenjan yetersiz olduğu için otomatik öneri üretilemedi.");
    }

    if (needsAssignedAdvisorFilter)
    {
        assignments = assignments
            .Where(a => a.SuggestedAdvisorId == advisorFilterId)
            .ToList();

        totalAssignmentCount = assignments.Count;
        totalPages = totalAssignmentCount == 0
            ? 0
            : (int)Math.Ceiling(totalAssignmentCount / (double)pageSize);

        if (totalPages > 0 && page > totalPages)
            page = totalPages;

        assignments = assignments
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    var advisorDtos = advisors.Select(advisor =>
    {
        advisorUsers.TryGetValue(advisor.UserId, out var advisorUser);

        var currentCount = currentAdvisorLoads.GetValueOrDefault(advisor.Id);
        var plannedNewCount = plannedLoads.GetValueOrDefault(advisor.Id);

        return new BulkAdvisorCandidateDto(
            advisor.Id,
            advisorUser != null ? $"{advisorUser.FirstName} {advisorUser.LastName}" : $"Danışman #{advisor.Id}",
            advisor.DepartmentId,
            advisor.Department?.Name ?? department.Name,
            currentCount,
            plannedNewCount,
            currentCount + plannedNewCount,
            advisor.IsAvailableForAdvisorAssignment != false,
            advisor.AdvisorAssignmentCapacity
        );
    }).ToList();

    if (!dto.IncludeStudentsWithAdvisor && alreadyAssignedCount > 0)
    {
        warnings.Add($"{alreadyAssignedCount} öğrencinin mevcut danışmanı korundu.");
    }

    return new BulkAdvisorAssignmentPreviewResponseDto(
        department.Id,
        department.Name,
        selectedProgram?.Id,
        selectedProgram?.Name,
        totalStudentCount,
        assignments.Count,
        alreadyAssignedCount,
        page,
        pageSize,
        totalAssignmentCount,
        totalPages,
        advisorDtos,
        assignments,
        warnings
    );
}

    public async Task<BulkAdvisorAssignmentApplyResponseDto> ApplyBulkAdvisorAssignmentAsync(
        BulkAdvisorAssignmentApplyRequestDto dto)
    {
        if (dto.Assignments == null || dto.Assignments.Count == 0)
            throw new InvalidOperationException("Uygulanacak danışman ataması bulunamadı.");

        var skippedItems = new List<BulkAdvisorAssignmentSkippedItemDto>();
        var updatedCount = 0;

        var uniqueAssignments = dto.Assignments
            .GroupBy(a => a.StudentId)
            .Select(g => g.Last())
            .ToList();

        foreach (var assignment in uniqueAssignments)
        {
            var student = await _context.Students
                .Include(s => s.StudentPrograms)
                    .ThenInclude(sp => sp.Program)
                .FirstOrDefaultAsync(s => s.Id == assignment.StudentId);

            if (student == null)
            {
                skippedItems.Add(new BulkAdvisorAssignmentSkippedItemDto(
                    assignment.StudentId,
                    "Öğrenci bulunamadı."
                ));
                continue;
            }

            var activeProgram = student.StudentPrograms.FirstOrDefault(sp =>
                sp.IsActive &&
                sp.Type == StudentProgramType.Main);

            if (activeProgram?.Program == null)
            {
                skippedItems.Add(new BulkAdvisorAssignmentSkippedItemDto(
                    assignment.StudentId,
                    "Öğrencinin aktif ana program/bölüm bilgisi bulunamadı."
                ));
                continue;
            }

            var advisor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == assignment.AdvisorId);

            if (advisor == null)
            {
                skippedItems.Add(new BulkAdvisorAssignmentSkippedItemDto(
                    assignment.StudentId,
                    "Danışman bulunamadı."
                ));
                continue;
            }

            if (advisor.DepartmentId != activeProgram.Program.DepartmentId)
            {
                skippedItems.Add(new BulkAdvisorAssignmentSkippedItemDto(
                    assignment.StudentId,
                    "Öğrenciye yalnızca kendi bölümündeki öğretim üyesi danışman atanabilir."
                ));
                continue;
            }

            student.AdvisorId = advisor.Id;
            updatedCount++;
        }

        await _context.SaveChangesAsync();

        return new BulkAdvisorAssignmentApplyResponseDto(
            updatedCount,
            skippedItems.Count,
            skippedItems
        );
    }

    public async Task<BulkAdvisorSettingsUpdateResponseDto> UpdateBulkAdvisorAssignmentSettingsAsync(
        BulkAdvisorSettingsUpdateRequestDto dto)
    {
        if (dto.Advisors == null || dto.Advisors.Count == 0)
            throw new InvalidOperationException("Güncellenecek danışman ayarı bulunamadı.");

        foreach (var item in dto.Advisors)
        {
            if (item.AdvisorAssignmentCapacity.HasValue && item.AdvisorAssignmentCapacity.Value < 0)
                throw new InvalidOperationException("Danışman kontenjanı negatif olamaz.");
        }

        var advisorIds = dto.Advisors
            .Select(a => a.AdvisorId)
            .Distinct()
            .ToList();

        var advisors = await _context.Instructors
            .Where(i => advisorIds.Contains(i.Id))
            .ToListAsync();

        var advisorMap = advisors.ToDictionary(a => a.Id);

        var updatedCount = 0;

        foreach (var item in dto.Advisors)
        {
            if (!advisorMap.TryGetValue(item.AdvisorId, out var advisor))
                throw new KeyNotFoundException($"Danışman bulunamadı: {item.AdvisorId}");

            advisor.IsAvailableForAdvisorAssignment = item.IsAvailableForAdvisorAssignment;
            advisor.AdvisorAssignmentCapacity = item.AdvisorAssignmentCapacity;
            advisor.UpdatedAt = DateTime.UtcNow;

            updatedCount++;
        }

        await _context.SaveChangesAsync();

        return new BulkAdvisorSettingsUpdateResponseDto(updatedCount);
    }
    private async Task<string> GenerateStudentNumberAsync(int entryYear, AcademicProgram program)
    {
        var yy = entryYear % 100;

        var deptIndex = program.Department.Code switch
        {
            "BM" => 0,
            "BSM" => 1,
            "BMTR" => 2,
            "EM" => 3,
            "EEM" => 4,
            "YM" => 5,
            _ => 9
        };

        var prefix = $"{yy:D2}{deptIndex:D1}";

        var maxExisting = await _context.Students
            .Where(s => s.StudentNumber.StartsWith(prefix))
            .Select(s => s.StudentNumber)
            .ToListAsync();

        int nextSeq = 1;

        if (maxExisting.Count > 0)
        {
            var maxSeq = maxExisting
                .Select(n => int.TryParse(n[prefix.Length..], out var num) ? num : 0)
                .Max();

            nextSeq = maxSeq + 1;
        }

        return $"{prefix}{nextSeq:D4}";
    }

    private static string GenerateStudentEmail(string firstName, string lastName)
    {
        var first = Normalize(firstName);
        var last = Normalize(lastName);

        return $"{first}.{last}@std.ankarabilim.edu.tr";
    }
    private static string Normalize(string name) =>
        name.ToLower()
            .Replace(' ', '.')
            .Replace('\u00e7', 'c')
            .Replace('\u011f', 'g')
            .Replace('\u0131', 'i')
            .Replace('\u00f6', 'o')
            .Replace('\u015f', 's')
            .Replace('\u00fc', 'u')
            .Replace('\u00e2', 'a')
            .Replace('\u00ee', 'i')
            .Replace('\u00fb', 'u');
}



