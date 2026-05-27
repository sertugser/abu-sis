using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using StudentEntity = SIS.Domain.Entities.Student;
using StudentProgramEntity = SIS.Domain.Entities.StudentProgram;

namespace SIS.Infrastructure.Services;

/// <inheritdoc />
public sealed class StudentRegistrationCatalogService : IStudentRegistrationCatalogService
{
    private readonly AppDbContext _context;
    private readonly IEctsLimitResolver _ectsLimitResolver;
    private readonly IRegistrationGate _registrationGate;
    private readonly IElectivePoolProgressCalculator _poolProgressCalculator;
    private readonly IDateTimeProvider _clock;

    public StudentRegistrationCatalogService(
        AppDbContext context,
        IEctsLimitResolver ectsLimitResolver,
        IRegistrationGate registrationGate,
        IElectivePoolProgressCalculator poolProgressCalculator,
        IDateTimeProvider clock)
    {
        _context = context;
        _ectsLimitResolver = ectsLimitResolver;
        _registrationGate = registrationGate;
        _poolProgressCalculator = poolProgressCalculator;
        _clock = clock;
    }

    public async Task<StudentRegistrationCatalogDto> BuildAsync(
        StudentEntity student,
        StudentProgramEntity activeProgram,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        // 1. Resolve active term (must include AcademicCalendar for phase methods)
        var term = await _context.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .ActiveTerm()
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("ActiveTermNotFound");

        // 2. Curriculum for name + ECTS-limit override
        var curriculum = await _context.Curricula
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == activeProgram.CurriculumId, ct);

        // 3. ApplicationUser for FullName
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, ct);
        var fullName = user is ApplicationUser au
            ? $"{au.FirstName} {au.LastName}".Trim()
            : student.StudentNumber;

        // 4. Program name (navigate via activeProgram.Program if loaded, else query)
        var programName = activeProgram.Program?.Name
            ?? await _context.AcademicPrograms
                .AsNoTracking()
                .Where(p => p.Id == activeProgram.ProgramId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct)
            ?? string.Empty;

        // 5. Degree-level-driven normal duration (no hardcoded cap)
        var degreeLevel = activeProgram.Program?.DegreeLevel
            ?? await _context.AcademicPrograms
                .AsNoTracking()
                .Where(p => p.Id == activeProgram.ProgramId)
                .Select(p => p.DegreeLevel)
                .FirstOrDefaultAsync(ct);

        var normalDurationSemesters = degreeLevel switch
        {
            DegreeLevel.Associate => 4,
            DegreeLevel.Bachelor  => 8,
            DegreeLevel.Master    => 4,
            DegreeLevel.Doctorate => 8,
            _                     => 8,
        };

        var classYear = Math.Min(
            student.CompletedSemesters / 2 + 1,
            normalDurationSemesters / 2);

        // 6. ECTS limit (delegated to resolver — curriculum override or SystemConfig)
        var ectsLimit = await _ectsLimitResolver.ResolveAsync(student.Cgpa, curriculum, ct);

        // 7. Build header
        var header = new RegistrationStudentHeaderDto(
            Id:                  student.Id,
            StudentNumber:       student.StudentNumber,
            FullName:            fullName,
            ProgramName:         programName,
            CurriculumName:      curriculum?.Name ?? string.Empty,
            ClassYear:           classYear,
            CompletedSemesters:  student.CompletedSemesters,
            Cgpa:                student.Cgpa,
            EctsLimit:           ectsLimit,
            FinancialStatus:     student.HasFinancialBlock ? "Bloke" : "Uygun",
            StudyDurationStatus: student.CompletedSemesters < normalDurationSemesters
                                     ? "Normal Süresi İçinde"
                                     : "Süre Aşımı");

        // 8. Term info
        var termInfo = new RegistrationTermInfoDto(
            Id:                 term.Id,
            Name:               term.Name,
            Year:               term.Year,
            TermType:           term.TermType.ToString(),
            IsRegistrationOpen: term.IsRegistrationOpen(now),
            IsAddDropOpen:      term.IsAddDropOpen(now),
            HasAcademicCalendar: term.AcademicCalendar != null,
            RegistrationMessage: null);

        // 9. Tree / retake / pool / selected — not yet implemented
        return new StudentRegistrationCatalogDto(
            Student:          header,
            Term:             termInfo,
            ClassYears:       Array.Empty<CatalogClassYearDto>(),
            RetakeCourses:    Array.Empty<CatalogCourseRowDto>(),
            ElectivePoolSlots: Array.Empty<CatalogPoolSlotDto>(),
            SelectedCourses:  Array.Empty<SelectedCourseDto>());
    }
}
