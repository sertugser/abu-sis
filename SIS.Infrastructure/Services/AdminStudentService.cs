using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminStudentService : IAdminStudentService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemConfigProvider _systemConfig;

    public AdminStudentService(AppDbContext context, UserManager<ApplicationUser> userManager, ISystemConfigProvider systemConfig)
    {
        _context = context;
        _userManager = userManager;
        _systemConfig = systemConfig;
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

        if (filter.DepartmentIds is { Count: > 0 })
        {
            var departmentIds = filter.DepartmentIds.ToList();
            query = query.Where(s => s.StudentPrograms.Any(sp =>
                sp.IsActive && departmentIds.Contains(sp.Program.DepartmentId)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<StudentStatus>(filter.Status, out var statusEnum))
            query = query.Where(s => s.Status == statusEnum);

        if (filter.MinCgpa.HasValue)
            query = query.Where(s => s.Cgpa >= filter.MinCgpa.Value);

        if (filter.MaxCgpa.HasValue)
            query = query.Where(s => s.Cgpa <= filter.MaxCgpa.Value);

        if (filter.AdvisorId.HasValue)
            query = query.Where(s => s.AdvisorId == filter.AdvisorId.Value);

        if (filter.HasAdvisor.HasValue)
            query = filter.HasAdvisor.Value
                ? query.Where(s => s.AdvisorId != null)
                : query.Where(s => s.AdvisorId == null);

        if (filter.CurriculumId.HasValue)
            query = query.Where(s => s.StudentPrograms.Any(sp =>
                sp.IsActive && sp.CurriculumId == filter.CurriculumId.Value));

        if (filter.ClassYear is int cy && cy >= 1)
        {
            query = cy >= 4
                ? query.Where(s => s.CompletedSemesters >= 6)
                : query.Where(s => s.CompletedSemesters >= (cy - 1) * 2
                                && s.CompletedSemesters <= cy * 2 - 1);
        }

        // string.Compare: AcademicOperationsService.Students.cs ile aynı (EF Core çevirebiliyor — kanıt: o servis canlı kullanımda).
        if (!string.IsNullOrWhiteSpace(filter.StudentNoFrom))
            query = query.Where(s => string.Compare(s.StudentNumber, filter.StudentNoFrom) >= 0);

        if (!string.IsNullOrWhiteSpace(filter.StudentNoTo))
            query = query.Where(s => string.Compare(s.StudentNumber, filter.StudentNoTo) <= 0);

        if (filter.EnrollmentFrom.HasValue)
            query = query.Where(s => s.StudentPrograms.Any(sp =>
                sp.IsActive && sp.EnrollmentDate >= filter.EnrollmentFrom.Value));

        if (filter.EnrollmentTo.HasValue)
            query = query.Where(s => s.StudentPrograms.Any(sp =>
                sp.IsActive && sp.EnrollmentDate <= filter.EnrollmentTo.Value));

        // Ders Başarı Durumu: FF/NA notu olan kayıt = başarısız ders.
        // (AcademicOperationsService.Students.cs satır 96-100 ile birebir aynı predicate.)
        // HasFailingCourse: AcademicOperationsService ile paritede — geçmişteki HERHANGİ bir FF/NA'yı işaretler (döneme göre daraltılmaz).
        if (filter.HasFailingCourse.HasValue)
        {
            query = filter.HasFailingCourse.Value
                ? query.Where(s => s.Registrations.Any(r =>
                    r.Grade == GradeCode.FF || r.Grade == GradeCode.NA))
                : query.Where(s => !s.Registrations.Any(r =>
                    r.Grade == GradeCode.FF || r.Grade == GradeCode.NA));
        }

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

        var advisorUserIds = students
            .Where(s => s.Advisor != null)
            .Select(s => s.Advisor!.UserId)
            .Distinct()
            .ToList();

        var advisorUsers = await _context.Set<ApplicationUser>()
            .Where(u => advisorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var items = students.Select(s =>
        {
            users.TryGetValue(s.UserId, out var user);

            var activeProgram = s.StudentPrograms.FirstOrDefault(sp => sp.IsActive);

            var advisorUser = s.Advisor != null
                ? advisorUsers.GetValueOrDefault(s.Advisor.UserId)
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
                advisorUser != null ? $"{advisorUser.FirstName} {advisorUser.LastName}" : null,
                s.NationalId,
                s.IsScholarshipStudent,
                activeProgram?.EnrollmentDate,
                Math.Min(4, s.CompletedSemesters / 2 + 1)
            );
        }).ToList();

        return new PagedResult<AdminStudentListItemDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<AdminStudentDetailDto?> GetStudentByIdAsync(int id)
    {
        var student = await _context.Students
            .Include(s => s.Advisor)
            .Include(s => s.Placement)
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return null;

        var user = await _context.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == student.UserId);

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        var mainProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive && sp.Type == StudentProgramType.Main);

        ApplicationUser? advisorUser = null;
        if (student.Advisor != null)
        {
            advisorUser = await _context.Set<ApplicationUser>()
                .FirstOrDefaultAsync(u => u.Id == student.Advisor.UserId);
        }

        return new AdminStudentDetailDto(
            student.Id,
            student.UserId,
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
            student.AdvisorId,
            mainProgram?.ProgramId,
            mainProgram?.CurriculumId,
            (mainProgram?.Type ?? StudentProgramType.Main).ToString(),
            mainProgram?.EnrollmentDate,
            Math.Min(4, student.CompletedSemesters / 2 + 1),
            student.DepartureDate,
            student.PreviousStudentNumber,
            student.NationalId,
            student.Placement?.EntranceType?.ToString(),
            student.Placement?.EntranceType is null
                ? null
                : EntranceTypeLabels.Of(student.Placement.EntranceType.Value)
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
            EmailConfirmed = true,
            PasswordChangedAtUtc = DateTime.UtcNow
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

        var enableAdvisorOptOutOptions = await _systemConfig.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true);
        if (enableAdvisorOptOutOptions && instructor.AdvisorOptOutRequested)
            throw new InvalidOperationException("Bu öğretim üyesi danışmanlık görevi almak istemediğini belirtmiştir.");
        if (instructor.DepartmentId != studentDepartmentId.Value)
        {
            throw new InvalidOperationException("Öğrenciye yalnızca kendi bölümündeki öğretim üyesi danışman atanabilir.");
        }

        student.AdvisorId = instructor.Id;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAcademicAsync(int studentId, UpdateStudentAcademicDto dto)
    {
        var student = await _context.Students.Include(s => s.StudentPrograms)
            .FirstOrDefaultAsync(s => s.Id == studentId)
            ?? throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");

        if (!Enum.TryParse<StudentStatus>(dto.Status, out var status))
            throw new InvalidOperationException($"Geçersiz durum: {dto.Status}");

        if (!Enum.TryParse<StudentProgramType>(dto.ProgramType, out var programType))
            throw new InvalidOperationException($"Geçersiz program tipi: {dto.ProgramType}");

        var program = await _context.AcademicPrograms.Include(p => p.Department)
            .FirstOrDefaultAsync(p => p.Id == dto.ProgramId)
            ?? throw new KeyNotFoundException($"Program bulunamadı: {dto.ProgramId}");

        var curriculumValid = await _context.Curricula
            .AnyAsync(c => c.Id == dto.CurriculumId && c.ProgramId == dto.ProgramId);
        if (!curriculumValid)
            throw new InvalidOperationException("Seçilen müfredat bu programa ait değil.");

        if (dto.AdvisorId.HasValue)
        {
            var instructor = await _context.Instructors.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == dto.AdvisorId.Value)
                ?? throw new KeyNotFoundException($"Öğretim üyesi bulunamadı: {dto.AdvisorId}");

            var optOutEnabled = await _systemConfig.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true);
            if (optOutEnabled && instructor.AdvisorOptOutRequested)
                throw new InvalidOperationException("Bu öğretim üyesi danışmanlık görevi almak istemediğini belirtmiştir.");

            if (instructor.DepartmentId != program.DepartmentId)
                throw new InvalidOperationException("Öğrenciye yalnızca kendi bölümündeki öğretim üyesi danışman atanabilir.");
        }

        var mainProgram = student.StudentPrograms
            .FirstOrDefault(p => p.IsActive && p.Type == StudentProgramType.Main)
            ?? throw new InvalidOperationException("Öğrencinin aktif ana programı bulunamadı.");

        student.Status = status;
        student.AdvisorId = dto.AdvisorId;
        student.IsScholarshipStudent = dto.IsScholarshipStudent;
        student.HasFinancialBlock = dto.HasFinancialBlock;
        student.DepartureDate = dto.DepartureDate;
        student.UpdatedAt = DateTime.UtcNow;

        if (mainProgram.ProgramId == dto.ProgramId)
        {
            // ProgramId composite key'in parçası; aynı kaldığında in-place güncellenebilir.
            mainProgram.CurriculumId = dto.CurriculumId;
            mainProgram.Type = programType;
        }
        else
        {
            // ProgramId (StudentId, ProgramId) composite key'in parçası olduğundan
            // değiştirilemez: eski kaydı sil, yenisini ekle.
            var enrollmentDate = mainProgram.EnrollmentDate;
            _context.Set<StudentProgram>().Remove(mainProgram);
            _context.Set<StudentProgram>().Add(new StudentProgram
            {
                StudentId = student.Id,
                ProgramId = dto.ProgramId,
                CurriculumId = dto.CurriculumId,
                Type = programType,
                EnrollmentDate = enrollmentDate,
                IsActive = true
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task ChangeStudentNumberAsync(int studentId, string newStudentNumber)
    {
        var trimmed = (newStudentNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Öğrenci numarası boş olamaz.");
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId)
            ?? throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");
        if (student.StudentNumber == trimmed) return;
        var taken = await _context.Students.AnyAsync(s => s.Id != studentId && s.StudentNumber == trimmed);
        if (taken)
            throw new InvalidOperationException($"Bu öğrenci numarası başka bir öğrenci tarafından kullanılıyor: {trimmed}");
        student.PreviousStudentNumber = student.StudentNumber;
        student.StudentNumber = trimmed;
        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
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


