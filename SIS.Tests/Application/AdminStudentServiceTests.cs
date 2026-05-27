using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Admin.DTOs;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AdminStudentServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminStudentSvc-{Guid.NewGuid()}")
            .Options);

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    [Fact]
    public async Task AssignAdvisorAsync_RejectsOptedOutInstructor_WhenSettingEnabled()
    {
        await using var context = CreateContext();
        context.Departments.Add(new Department { Id = 1, Name = "Bilgisayar", Code = "CENG", FacultyId = 1 });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 100,
            DepartmentId = 1,
            Name = "Bilgisayar Muhendisligi",
            Code = "CENG",
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = DateTime.UtcNow
        });
        context.Curricula.Add(new Curriculum
        {
            Id = 1000,
            ProgramId = 100,
            Version = "2024",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Instructors.Add(new Instructor
        {
            Id = 10,
            UserId = 100,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 10,
            AdvisorOptOutRequested = true,
        });
        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 20,
            ProgramId = 100,
            CurriculumId = 1000,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        config.Setup(x => x.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var action = async () => await service.AssignAdvisorAsync(20, 10);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AssignAdvisorAsync_AllowsOptedOutInstructor_WhenSettingDisabled()
    {
        await using var context = CreateContext();
        context.Departments.Add(new Department { Id = 1, Name = "Bilgisayar", Code = "CENG", FacultyId = 1 });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 100,
            DepartmentId = 1,
            Name = "Bilgisayar Muhendisligi",
            Code = "CENG",
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = DateTime.UtcNow
        });
        context.Curricula.Add(new Curriculum
        {
            Id = 1000,
            ProgramId = 100,
            Version = "2024",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Instructors.Add(new Instructor
        {
            Id = 10,
            UserId = 100,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 10,
            AdvisorOptOutRequested = true,
        });
        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 20,
            ProgramId = 100,
            CurriculumId = 1000,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        config.Setup(x => x.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        await service.AssignAdvisorAsync(20, 10);

        var student = await context.Students.SingleAsync(x => x.Id == 20);
        student.AdvisorId.Should().Be(10);
    }

    [Fact]
    public async Task GetStudentsAsync_HasAdvisorFalse_ReturnsOnlyStudentsWithoutAdvisor()
    {
        await using var context = CreateContext();
        context.Departments.Add(new Department { Id = 1, Name = "Bilgisayar", Code = "CENG", FacultyId = 1 });
        context.Instructors.Add(new Instructor
        {
            Id = 10,
            UserId = 100,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 10,
        });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 100, FirstName = "Ali", LastName = "Hoca" });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 200, FirstName = "Ayse", LastName = "Ogrenci" });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 201, FirstName = "Veli", LastName = "Ogrenci" });

        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
            AdvisorId = 10,
        });
        context.Students.Add(new Student
        {
            Id = 21,
            UserId = 201,
            StudentNumber = "2024002",
            NationalId = "22222222222",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            Sgpa = 3.0m,
            CompletedSemesters = 2,
            AdvisorId = null,
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentsAsync(
            new SIS.Application.Admin.DTOs.AdminStudentFilterDto(
                null, null, null, null, null, null, HasAdvisor: false));

        result.Items.Should().OnlyContain(i => i.AdvisorName == null);
        result.Items.Should().ContainSingle(i => i.StudentNumber == "2024002");
    }

    [Fact]
    public async Task GetStudentsAsync_FilterByCurriculumId_ReturnsMatchingStudents()
    {
        await using var context = CreateContext();
        context.Departments.Add(new Department { Id = 1, Name = "Bilgisayar", Code = "CENG", FacultyId = 1 });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 100,
            DepartmentId = 1,
            Name = "Bilgisayar Muhendisligi",
            Code = "CENG",
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = DateTime.UtcNow
        });
        context.Curricula.Add(new Curriculum { Id = 1000, ProgramId = 100, Version = "2024", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Curricula.Add(new Curriculum { Id = 1001, ProgramId = 100, Version = "2025", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 200, FirstName = "Ayse", LastName = "Ogrenci" });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 201, FirstName = "Veli", LastName = "Ogrenci" });

        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        context.Students.Add(new Student
        {
            Id = 21,
            UserId = 201,
            StudentNumber = "2025001",
            NationalId = "22222222222",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            Sgpa = 3.0m,
            CompletedSemesters = 2,
        });
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 20,
            ProgramId = 100,
            CurriculumId = 1000,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 21,
            ProgramId = 100,
            CurriculumId = 1001,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentsAsync(
            new SIS.Application.Admin.DTOs.AdminStudentFilterDto(
                null, null, null, null, null, null, CurriculumId: 1001));

        result.Items.Should().ContainSingle(i => i.StudentNumber == "2025001");
        result.Items.Should().OnlyContain(i => i.StudentNumber == "2025001");
    }

    [Fact]
    public async Task GetStudentsAsync_ClassYearZero_SkipsFilterAndReturnsAllStudents()
    {
        await using var context = CreateContext();
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 200, FirstName = "Ayse", LastName = "Ogrenci" });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 201, FirstName = "Veli", LastName = "Ogrenci" });

        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        context.Students.Add(new Student
        {
            Id = 21,
            UserId = 201,
            StudentNumber = "2024002",
            NationalId = "22222222222",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            Sgpa = 3.0m,
            CompletedSemesters = 4,
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentsAsync(
            new SIS.Application.Admin.DTOs.AdminStudentFilterDto(
                null, null, null, null, null, null, ClassYear: 0));

        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(i => i.StudentNumber == "2024001");
        result.Items.Should().Contain(i => i.StudentNumber == "2024002");
    }

    [Fact]
    public async Task GetStudentByIdAsync_ReturnsUserId()
    {
        await using var context = CreateContext();
        const int studentId = 20;
        const int userId = 200;

        context.Students.Add(new Student
        {
            Id = studentId,
            UserId = userId,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentByIdAsync(studentId);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
    }

    private static async Task<(AppDbContext context, AdminStudentService service)> SeedStudentNumberScenario()
    {
        var context = CreateContext();
        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        context.Students.Add(new Student
        {
            Id = 21,
            UserId = 201,
            StudentNumber = "2024002",
            NationalId = "22222222222",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            Sgpa = 3.0m,
            CompletedSemesters = 2,
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);
        return (context, service);
    }

    [Fact]
    public async Task ChangeStudentNumberAsync_UpdatesNumber_WhenUnique()
    {
        var (context, service) = await SeedStudentNumberScenario();
        await using var _ = context;

        await service.ChangeStudentNumberAsync(20, "2024999");

        var student = await context.Students.SingleAsync(x => x.Id == 20);
        student.StudentNumber.Should().Be("2024999");
    }

    [Fact]
    public async Task ChangeStudentNumberAsync_Throws_WhenNumberAlreadyUsed()
    {
        var (context, service) = await SeedStudentNumberScenario();
        await using var _ = context;

        await FluentActions
            .Invoking(() => service.ChangeStudentNumberAsync(20, "2024002"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ChangeStudentNumberAsync_Throws_WhenStudentNotFound()
    {
        var (context, service) = await SeedStudentNumberScenario();
        await using var _ = context;

        await FluentActions
            .Invoking(() => service.ChangeStudentNumberAsync(999999, "2024999"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ChangeStudentNumberAsync_SetsPreviousStudentNumber_ToOldValue()
    {
        var (context, service) = await SeedStudentNumberScenario();
        await using var _ = context;

        await service.ChangeStudentNumberAsync(20, "2024999");

        var student = await context.Students.SingleAsync(x => x.Id == 20);
        student.StudentNumber.Should().Be("2024999");
        student.PreviousStudentNumber.Should().Be("2024001");
    }

    [Fact]
    public async Task UpdateAcademicAsync_PersistsDepartureDate()
    {
        var (context, service, studentId) = await SeedAcademicEditScenario();
        await using var _ = context;

        var departure = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        await service.UpdateAcademicAsync(studentId, new UpdateStudentAcademicDto(
            "Active", 5, 1, 10, "Main", false, false, departure));

        var s = await context.Students.FirstAsync(x => x.Id == studentId);
        s.DepartureDate.Should().Be(departure);
    }

    [Fact]
    public async Task GetStudentByIdAsync_ReturnsEntranceTypeLabelAndNationalId()
    {
        await using var context = CreateContext();
        const int studentId = 20;

        context.Students.Add(new Student
        {
            Id = studentId,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        context.Set<StudentPlacement>().Add(new StudentPlacement
        {
            StudentId = studentId,
            EntranceType = StudentEntranceType.Dgs,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentByIdAsync(studentId);

        result.Should().NotBeNull();
        result!.NationalId.Should().Be("11111111111");
        result.EntranceType.Should().Be("Dgs");
        result.EntranceTypeLabel.Should().Be(EntranceTypeLabels.Of(StudentEntranceType.Dgs));
        result.ClassYear.Should().Be(2);
        result.EnrollmentDate.Should().BeNull();
    }

    private static async Task<(AppDbContext context, AdminStudentService service, int studentId)> SeedAcademicEditScenario(int advisorDepartmentId = 1)
    {
        var context = CreateContext();
        context.Departments.Add(new Department { Id = 1, Name = "Bilgisayar", Code = "CENG", FacultyId = 1 });
        context.Departments.Add(new Department { Id = 2, Name = "Elektrik", Code = "EE", FacultyId = 1 });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 1,
            DepartmentId = 1,
            Name = "Program 1",
            Code = "P1",
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = DateTime.UtcNow
        });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 2,
            DepartmentId = 1,
            Name = "Program 2",
            Code = "P2",
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = DateTime.UtcNow
        });
        context.Curricula.Add(new Curriculum { Id = 10, ProgramId = 1, Version = "2023", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Curricula.Add(new Curriculum { Id = 20, ProgramId = 2, Version = "2024", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Instructors.Add(new Instructor
        {
            Id = 5,
            UserId = 500,
            DepartmentId = advisorDepartmentId,
            Title = "Dr.",
            MaxWeeklyHours = 10,
        });
        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
            AdvisorId = null,
        });
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 20,
            ProgramId = 1,
            CurriculumId = 10,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        config.Setup(x => x.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);
        return (context, service, 20);
    }

    [Fact]
    public async Task UpdateAcademicAsync_UpdatesStatusProgramCurriculumAndFlags()
    {
        var (context, service, studentId) = await SeedAcademicEditScenario();
        await using var _ = context;

        await service.UpdateAcademicAsync(studentId, new UpdateStudentAcademicDto(
            "Frozen", 5, 2, 20, "Main", true, false, null));

        var s = await context.Students.Include(x => x.StudentPrograms).FirstAsync(x => x.Id == studentId);
        s.Status.Should().Be(StudentStatus.Frozen);
        s.AdvisorId.Should().Be(5);
        s.IsScholarshipStudent.Should().BeTrue();
        var mp = s.StudentPrograms.Single(p => p.IsActive && p.Type == StudentProgramType.Main);
        mp.ProgramId.Should().Be(2);
        mp.CurriculumId.Should().Be(20);
    }

    [Fact]
    public async Task UpdateAcademicAsync_Throws_WhenAdvisorNotInProgramDepartment()
    {
        var (context, service, studentId) = await SeedAcademicEditScenario(advisorDepartmentId: 2);
        await using var _ = context;

        await FluentActions
            .Invoking(() => service.UpdateAcademicAsync(studentId, new UpdateStudentAcademicDto(
                "Active", 5, 2, 20, "Main", false, false, null)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAcademicAsync_RemovesOldProgramRow_OnProgramChange()
    {
        var (context, service, studentId) = await SeedAcademicEditScenario();
        await using var _ = context;

        // Program 1 -> 2 (advisor in dept 1; program 2 also in dept 1, so advisor stays valid).
        await service.UpdateAcademicAsync(studentId, new UpdateStudentAcademicDto(
            "Active", 5, 2, 20, "Main", false, false, null));

        var s = await context.Students.Include(x => x.StudentPrograms).FirstAsync(x => x.Id == studentId);
        s.StudentPrograms.Count(p => p.IsActive && p.Type == StudentProgramType.Main).Should().Be(1);
        s.StudentPrograms.Should().NotContain(p => p.ProgramId == 1);
    }

    [Fact]
    public async Task UpdateAcademicAsync_Throws_WhenCurriculumNotInProgram()
    {
        var (context, service, studentId) = await SeedAcademicEditScenario();
        await using var _ = context;

        // Curriculum 10 belongs to program 1, but we target program 2 -> mismatch.
        await FluentActions
            .Invoking(() => service.UpdateAcademicAsync(studentId, new UpdateStudentAcademicDto(
                "Active", null, 2, 10, "Main", false, false, null)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAcademicAsync_UpdatesCurriculumInPlace_WhenProgramUnchanged()
    {
        var (context, service, studentId) = await SeedAcademicEditScenario();
        await using var _ = context;

        // Add a second curriculum (id 11) for the SAME program 1 so we can switch in place.
        context.Curricula.Add(new Curriculum { Id = 11, ProgramId = 1, Version = "2024b", IsActive = true, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var before = await context.Students.Include(x => x.StudentPrograms).FirstAsync(x => x.Id == studentId);
        var rowCountBefore = before.StudentPrograms.Count;

        // Keep program 1, change only curriculum (10 -> 11) and type.
        await service.UpdateAcademicAsync(studentId, new UpdateStudentAcademicDto(
            "Active", null, 1, 11, "DoubleMajor", false, false, null));

        var s = await context.Students.Include(x => x.StudentPrograms).FirstAsync(x => x.Id == studentId);
        s.StudentPrograms.Count.Should().Be(rowCountBefore);
        var mp = s.StudentPrograms.Single(p => p.IsActive);
        mp.ProgramId.Should().Be(1);
        mp.CurriculumId.Should().Be(11);
        mp.Type.Should().Be(StudentProgramType.DoubleMajor);
    }
}
