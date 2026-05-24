using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SIS.API.Authorization;
using SIS.API.Controllers;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class StudentManagementControllerTests
{
    [Fact]
    public async Task GetStudents_WhenTranscriptClosedForAcademicStaff_MasksTranscriptSummary()
    {
        await using var context = CreateContext();
        SeedDepartmentManager(context, userId: 42, instructorId: 7, departmentId: 1);

        var serviceMock = new Mock<IAdminStudentService>();
        serviceMock
            .Setup(x => x.GetStudentsAsync(It.IsAny<AdminStudentFilterDto>()))
            .ReturnsAsync(new PagedResult<AdminStudentListItemDto>(
                new List<AdminStudentListItemDto>
                {
                    new(5, "2024001", "Ali", "Veli", "ali@ankarabilim.edu.tr", 3.12m, 4, "Active", "Yazılım", "YZM", "Yazılım Mühendisliği", "Dr. Ada")
                },
                1,
                1,
                25));

        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetBoolAsync("Instructor.TranscriptClosed", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(context, serviceMock.Object, configMock.Object, 42, "DepartmentManager");

        var result = await controller.GetStudents(new AdminStudentFilterDto(null, null, null, null, null, null, 1, 25));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"TranscriptAccessible\":false");
        json.Should().Contain("\"Cgpa\":null");
        json.Should().Contain("\"CompletedSemesters\":null");
    }

    [Fact]
    public async Task UpdateAcademic_ReturnsForbid_WhenCallerCannotAccessStudentDepartment()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);

        var serviceMock = new Mock<IAdminStudentService>();
        var scopeGuardMock = new Mock<IAcademicScopeGuard>();
        scopeGuardMock
            .Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreateControllerWithScopeGuard(
            context, serviceMock.Object, scopeGuardMock.Object, userId: 42, "DepartmentManager");

        var dto = new UpdateStudentAcademicDto(
            Status: "Active",
            AdvisorId: null,
            ProgramId: 10,
            CurriculumId: 1,
            ProgramType: "Main",
            IsScholarshipStudent: false,
            HasFinancialBlock: false);

        var result = await controller.UpdateAcademic(5, dto);

        result.Should().BeOfType<ForbidResult>();
        serviceMock.Verify(
            s => s.UpdateAcademicAsync(It.IsAny<int>(), It.IsAny<UpdateStudentAcademicDto>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeStudentNumber_ReturnsForbid_WhenCallerCannotAccessDepartment()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);

        var serviceMock = new Mock<IAdminStudentService>();
        var scopeGuardMock = new Mock<IAcademicScopeGuard>();
        scopeGuardMock
            .Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreateControllerWithScopeGuard(
            context, serviceMock.Object, scopeGuardMock.Object, userId: 42, "DepartmentManager");

        var result = await controller.ChangeStudentNumber(5, new ChangeStudentNumberDto("2024999"));

        result.Should().BeOfType<ForbidResult>();
        serviceMock.Verify(
            s => s.ChangeStudentNumberAsync(It.IsAny<int>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPersonalInfo_ReturnsForbid_WhenCallerCannotAccessDepartment()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);

        var serviceMock = new Mock<IAdminStudentService>();
        var personalInfoMock = new Mock<IStudentPersonalInfoService>();
        var scopeGuardMock = new Mock<IAcademicScopeGuard>();
        scopeGuardMock
            .Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new StudentManagementController(
            serviceMock.Object,
            context,
            scopeGuardMock.Object,
            personalInfoMock.Object,
            new SIS.Application.Common.Services.NullStudentPhotoStorage());
        AttachUser(controller, userId: 42, new[] { "DepartmentManager" });

        var result = await controller.GetPersonalInfo(5);

        result.Should().BeOfType<ForbidResult>();
        personalInfoMock.Verify(
            s => s.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Photo upload / serve / delete ───────────────────────────────────────

    private static readonly byte[] ValidPngBytes =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG magic
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // (rest is arbitrary)
    };

    private static readonly byte[] JpegishBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

    private static IFormFile MakeFormFile(byte[] bytes, string contentType, string fileName = "photo.bin")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private sealed class TempPhotoStorageFixture : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "sis-ctrl-photo-" + Guid.NewGuid().ToString("N"));
        public SIS.Infrastructure.Services.FileSystemStudentPhotoStorage Storage { get; }

        public TempPhotoStorageFixture()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["StudentPhotos:BasePath"] = Dir })
                .Build();
            var env = new Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(Dir);
            Storage = new SIS.Infrastructure.Services.FileSystemStudentPhotoStorage(config, env.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
        }
    }

    private static StudentManagementController CreatePhotoController(
        AppDbContext context,
        IAcademicScopeGuard scopeGuard,
        SIS.Infrastructure.Services.FileSystemStudentPhotoStorage storage,
        int userId,
        params string[] roles)
    {
        var controller = new StudentManagementController(
            new Mock<IAdminStudentService>().Object,
            context,
            scopeGuard,
            new SIS.Infrastructure.Services.StudentPersonalInfoService(context),
            storage);
        AttachUser(controller, userId, roles);
        return controller;
    }

    private static Mock<IAcademicScopeGuard> AllowAllScopeGuard()
    {
        var mock = new Mock<IAcademicScopeGuard>();
        mock.Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    [Fact]
    public async Task UploadPhoto_ReturnsNoContent_AndSetsPhotoExtension_ForValidPng()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);
        using var fixture = new TempPhotoStorageFixture();

        var controller = CreatePhotoController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        var file = MakeFormFile(ValidPngBytes, "image/png", "me.png");

        var result = await controller.UploadPhoto(5, file);

        result.Should().BeOfType<NoContentResult>();
        var info = await context.StudentPersonalInfos.AsNoTracking().SingleAsync();
        info.PhotoExtension.Should().Be("png");
        File.Exists(Path.Combine(fixture.Dir, "5.png")).Should().BeTrue();
    }

    [Fact]
    public async Task UploadPhoto_Returns400_WhenContentTypeNotAllowed()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);
        using var fixture = new TempPhotoStorageFixture();

        var controller = CreatePhotoController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        var file = MakeFormFile(ValidPngBytes, "image/gif", "me.gif");

        var result = await controller.UploadPhoto(5, file);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await context.StudentPersonalInfos.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UploadPhoto_Returns400_WhenMagicBytesMismatchDeclaredType()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);
        using var fixture = new TempPhotoStorageFixture();

        var controller = CreatePhotoController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        // Declared png, but bytes are jpeg-ish.
        var file = MakeFormFile(JpegishBytes, "image/png", "fake.png");

        var result = await controller.UploadPhoto(5, file);

        result.Should().BeOfType<BadRequestObjectResult>();
        File.Exists(Path.Combine(fixture.Dir, "5.png")).Should().BeFalse();
    }

    [Fact]
    public async Task UploadPhoto_Returns400_WhenNoFile()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);
        using var fixture = new TempPhotoStorageFixture();

        var controller = CreatePhotoController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");

        var result = await controller.UploadPhoto(5, file: null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadPhoto_ReturnsForbid_ForScopedNonAdminWithoutAccess()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);
        using var fixture = new TempPhotoStorageFixture();

        var scopeGuard = new Mock<IAcademicScopeGuard>();
        scopeGuard.Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreatePhotoController(context, scopeGuard.Object, fixture.Storage, 42, "DepartmentManager");
        var file = MakeFormFile(ValidPngBytes, "image/png");

        var result = await controller.UploadPhoto(5, file);

        result.Should().BeOfType<ForbidResult>();
        File.Exists(Path.Combine(fixture.Dir, "5.png")).Should().BeFalse();
    }

    [Fact]
    public async Task GetPhoto_Returns404_WhenNoPhoto()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);
        using var fixture = new TempPhotoStorageFixture();

        var controller = CreatePhotoController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");

        var result = await controller.GetPhoto(5);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPhoto_ReturnsFile_AfterUpload()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);
        using var fixture = new TempPhotoStorageFixture();

        var controller = CreatePhotoController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        await controller.UploadPhoto(5, MakeFormFile(ValidPngBytes, "image/png"));

        var result = await controller.GetPhoto(5);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        // Akışı kapat ki temp-dir cleanup dosyayı silebilsin (Windows file lock).
        await fileResult.FileStream.DisposeAsync();
    }

    [Fact]
    public async Task DeletePhoto_RemovesFileAndNullsExtension()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);
        using var fixture = new TempPhotoStorageFixture();

        var controller = CreatePhotoController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        await controller.UploadPhoto(5, MakeFormFile(ValidPngBytes, "image/png"));
        File.Exists(Path.Combine(fixture.Dir, "5.png")).Should().BeTrue();

        var result = await controller.DeletePhoto(5);

        result.Should().BeOfType<NoContentResult>();
        File.Exists(Path.Combine(fixture.Dir, "5.png")).Should().BeFalse();
        var info = await context.StudentPersonalInfos.AsNoTracking().SingleAsync();
        info.PhotoExtension.Should().BeNull();
    }

    private static void SeedStudentInDepartment(AppDbContext context, int studentId, int programId, int departmentId)
    {
        context.Faculties.Add(new Faculty
        {
            Id = 1,
            Code = "F1",
            Name = "Mühendislik"
        });
        context.Departments.Add(new Department
        {
            Id = departmentId,
            FacultyId = 1,
            Code = "YZM",
            Name = "Yazılım Mühendisliği"
        });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = programId,
            DepartmentId = departmentId,
            Code = "YZM-LIS",
            Name = "Yazılım Mühendisliği Lisans"
        });
        context.Students.Add(new Student
        {
            Id = studentId,
            UserId = 1000 + studentId,
            StudentNumber = $"S{studentId}",
            NationalId = $"N{studentId}",
        });
        context.Set<StudentProgram>().Add(new StudentProgram
        {
            StudentId = studentId,
            ProgramId = programId,
            CurriculumId = 1,
            Type = SIS.Domain.Enums.StudentProgramType.Main,
            IsActive = true
        });
        context.SaveChanges();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedDepartmentManager(AppDbContext context, int userId, int instructorId, int departmentId)
    {
        context.Faculties.Add(new Faculty
        {
            Id = 1,
            Code = "F1",
            Name = "Mühendislik"
        });
        context.Departments.Add(new Department
        {
            Id = departmentId,
            FacultyId = 1,
            Code = "YZM",
            Name = "Yazılım Mühendisliği"
        });
        context.Instructors.Add(new Instructor
        {
            Id = instructorId,
            UserId = userId,
            DepartmentId = departmentId,
            Title = "Dr.",
            MaxWeeklyHours = 12
        });
        context.SaveChanges();
    }

    private static StudentManagementController CreateController(
        AppDbContext context,
        IAdminStudentService service,
        ISystemConfigProvider config,
        int userId,
        params string[] roles)
    {
        var controller = new StudentManagementController(service, context, config);
        AttachUser(controller, userId, roles);
        return controller;
    }

    private static StudentManagementController CreateControllerWithScopeGuard(
        AppDbContext context,
        IAdminStudentService service,
        IAcademicScopeGuard scopeGuard,
        int userId,
        params string[] roles)
    {
        var controller = new StudentManagementController(service, context, scopeGuard);
        AttachUser(controller, userId, roles);
        return controller;
    }

    private static void AttachUser(StudentManagementController controller, int userId, string[] roles)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    }.Concat(roles.Select(role => new Claim(ClaimTypes.Role, role))),
                    "TestAuth"))
            }
        };
    }
}
