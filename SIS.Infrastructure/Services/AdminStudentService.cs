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

        if (student == null) return null;

        var user = await _context.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == student.UserId);

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);

        ApplicationUser? advisorUser = null;
        if (student.Advisor != null)
            advisorUser = await _context.Set<ApplicationUser>()
                .FirstOrDefaultAsync(u => u.Id == student.Advisor.UserId);

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

        // Ensure email uniqueness
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
            throw new InvalidOperationException(
                string.Join(", ", createResult.Errors.Select(e => e.Description)));

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

        var instructor = await _context.Instructors.FindAsync(advisorId)
            ?? throw new KeyNotFoundException($"Öğretim üyesi bulunamadı: {advisorId}");

        student.AdvisorId = instructor.Id;
        await _context.SaveChangesAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<string> GenerateStudentNumberAsync(int entryYear, AcademicProgram program)
    {
        var yy = entryYear % 100;
        // Program index based on department code order
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
            .Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i')
            .Replace('ö', 'o').Replace('ş', 's').Replace('ü', 'u')
            .Replace('â', 'a').Replace('î', 'i').Replace('û', 'u');
}
