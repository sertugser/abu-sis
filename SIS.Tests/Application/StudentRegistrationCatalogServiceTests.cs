using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;

namespace SIS.Tests.Application;

public sealed class StudentRegistrationCatalogServiceTests : IDisposable
{
    private readonly AppDbContext _context;

    public StudentRegistrationCatalogServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task BuildAsync_ReturnsHeader_WithStudentBasics()
    {
        // Arrange — seed minimum entities
        var now = DateTime.UtcNow;

        // Faculty (required by Department FK)
        var faculty = new Faculty { Id = 1, Name = "Mühendislik Fakültesi", Code = "MF", CreatedAt = now };
        _context.Set<Faculty>().Add(faculty);

        // Department
        var dept = new Department { Id = 1, Name = "Bilgisayar Mühendisliği", Code = "CENG", FacultyId = 1, CreatedAt = now };
        _context.Set<Department>().Add(dept);

        // AcademicProgram
        var program = new AcademicProgram
        {
            Id = 1,
            Name = "Bilgisayar Mühendisliği",
            Code = "CENG",
            DepartmentId = 1,
            DegreeLevel = DegreeLevel.Bachelor,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = now,
        };
        _context.Set<AcademicProgram>().Add(program);

        // Curriculum
        var curriculum = new Curriculum
        {
            Id = 1,
            ProgramId = 1,
            Version = "2022",
            Name = "CENG 2022 Müfredatı",
            IsActive = true,
            Status = CurriculumStatus.Published,
            CreatedAt = now,
        };
        _context.Set<Curriculum>().Add(curriculum);

        // Active AcademicTerm (IsActivated=true, not cancelled)
        var term = new AcademicTerm
        {
            Id = 10,
            Name = "2025-2026 Güz",
            ShortName = "25-26 Güz",
            EnglishName = "2025-2026 Fall",
            ShortEnglishName = "25-26 Fall",
            Year = 2025,
            TermType = TermType.Fall,
            IsActivated = true,
            IsCancelled = false,
            TermStart = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = now,
        };
        _context.Set<AcademicTerm>().Add(term);

        // ApplicationUser — for FullName lookup
        var user = new ApplicationUser
        {
            Id = 100,
            FirstName = "Ahmet",
            LastName = "Yılmaz",
            UserName = "ahmet.yilmaz",
            Email = "ahmet.yilmaz@test.com",
            NormalizedUserName = "AHMET.YILMAZ",
            NormalizedEmail = "AHMET.YILMAZ@TEST.COM",
        };
        _context.Set<ApplicationUser>().Add(user);

        // Student
        var student = new Student
        {
            Id = 1,
            UserId = 100,
            StudentNumber = "20220001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 3.10m,
            CompletedSemesters = 2,
            HasFinancialBlock = false,
            CreatedAt = now,
        };
        _context.Set<Student>().Add(student);

        // StudentProgram
        var sp = new StudentProgram
        {
            StudentId = 1,
            ProgramId = 1,
            CurriculumId = 1,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _context.Set<StudentProgram>().Add(sp);

        await _context.SaveChangesAsync();

        // Construct service with real dependencies
        var ectsResolver = new EctsLimitResolver(new NullSystemConfigProvider());
        var gate = new RegistrationGate(_context);
        var poolCalc = new ElectivePoolProgressCalculator(_context);
        var clock = new SystemDateTimeProvider();

        var service = new StudentRegistrationCatalogService(_context, ectsResolver, gate, poolCalc, clock);

        // Act
        var result = await service.BuildAsync(student, sp);

        // Assert — Student header
        result.Student.StudentNumber.Should().Be("20220001");
        result.Student.Cgpa.Should().Be(3.10m);
        result.Student.EctsLimit.Should().Be(48); // CGPA ≥ 2.50 → default 48
        result.Student.FinancialStatus.Should().Be("Uygun");
        result.Student.StudyDurationStatus.Should().Be("Normal Süresi İçinde");
        result.Student.ClassYear.Should().Be(2); // CompletedSemesters=2 → year=2
        result.Student.CompletedSemesters.Should().Be(2);

        // Assert — Term info
        result.Term.Id.Should().Be(10);
        result.Term.Year.Should().Be(2025);

        // Assert — lists are empty (not yet implemented)
        result.ClassYears.Should().BeEmpty();
        result.RetakeCourses.Should().BeEmpty();
        result.ElectivePoolSlots.Should().BeEmpty();
        result.SelectedCourses.Should().BeEmpty();
    }
}
