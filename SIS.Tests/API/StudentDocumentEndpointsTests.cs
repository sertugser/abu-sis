using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using SIS.API.Authorization;
using SIS.API.Controllers;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Domain.Entities;

namespace SIS.Tests.API;

public class StudentDocumentEndpointsTests
{
    // "%PDF-1.4\n" — geçerli PDF magic + sürüm.
    private static readonly byte[] ValidPdfBytes =
    {
        0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A,
    };

    private static readonly byte[] ValidPngBytes =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D,
    };

    private static IFormFile MakeFormFile(byte[] bytes, string contentType, string fileName = "doc.bin")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private sealed class TempDocStorageFixture : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "sis-ctrl-doc-" + Guid.NewGuid().ToString("N"));
        public FileSystemStudentDocumentStorage Storage { get; }

        public TempDocStorageFixture()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["StudentDocuments:BasePath"] = Dir })
                .Build();
            var env = new Mock<IHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(Dir);
            Storage = new FileSystemStudentDocumentStorage(config, env.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
        }
    }

    private static StudentManagementController CreateController(
        AppDbContext context,
        IAcademicScopeGuard scopeGuard,
        FileSystemStudentDocumentStorage storage,
        int userId,
        params string[] roles)
    {
        var controller = new StudentManagementController(
            new Mock<IAdminStudentService>().Object,
            context,
            scopeGuard,
            new StudentDocumentService(context),
            storage,
            new ConfigurationBuilder().Build());
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
    public async Task UploadDocument_ReturnsNoContent_AndStatusShowsUploaded_ForValidPdf()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        var file = MakeFormFile(ValidPdfBytes, "application/pdf", "diploma.pdf");

        var result = await controller.UploadDocument(5, 1, file);

        result.Should().BeOfType<NoContentResult>();

        var statusResult = await controller.GetDocuments(5);
        var ok = statusResult.Should().BeOfType<OkObjectResult>().Subject;
        var status = ok.Value.Should().BeAssignableTo<List<StudentDocumentStatusDto>>().Subject;
        var row = status.Single(s => s.DocumentTypeId == 1);
        row.Uploaded.Should().BeTrue();
        row.OriginalFileName.Should().Be("diploma.pdf");

        var docId = row.DocumentId!.Value;
        File.Exists(Path.Combine(fixture.Dir, $"5_{docId}.pdf")).Should().BeTrue();
    }

    [Fact]
    public async Task UploadDocument_Returns400_WhenContentTypeNotAllowed()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        var file = MakeFormFile(ValidPdfBytes, "application/zip", "x.zip");

        var result = await controller.UploadDocument(5, 1, file);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await context.StudentDocuments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UploadDocument_Returns400_WhenMagicBytesMismatchDeclaredType()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        // Declared pdf, but bytes are png.
        var file = MakeFormFile(ValidPngBytes, "application/pdf", "fake.pdf");

        var result = await controller.UploadDocument(5, 1, file);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await context.StudentDocuments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UploadDocument_Returns400_WhenOversize()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        // 1-byte max via config: any real PDF exceeds it (buffered.Length check).
        var controller = new StudentManagementController(
            new Mock<IAdminStudentService>().Object,
            context,
            AllowAllScopeGuard().Object,
            new StudentDocumentService(context),
            fixture.Storage,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["StudentDocuments:MaxBytes"] = "1" })
                .Build());
        AttachUser(controller, 1, new[] { "SystemAdministrator" });

        var file = MakeFormFile(ValidPdfBytes, "application/pdf", "big.pdf");

        var result = await controller.UploadDocument(5, 1, file);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await context.StudentDocuments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UploadDocument_Returns404_WhenDocumentTypeMissing()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        var file = MakeFormFile(ValidPdfBytes, "application/pdf", "diploma.pdf");

        var result = await controller.UploadDocument(5, 999, file);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UploadDocument_ReturnsForbid_ForScopedNonAdminWithoutAccess()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var scopeGuard = new Mock<IAcademicScopeGuard>();
        scopeGuard.Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreateController(context, scopeGuard.Object, fixture.Storage, 42, "DepartmentManager");
        var file = MakeFormFile(ValidPdfBytes, "application/pdf");

        var result = await controller.UploadDocument(5, 1, file);

        result.Should().BeOfType<ForbidResult>();
        (await context.StudentDocuments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task GetDocuments_Returns200_WithCatalogStatus()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");

        var result = await controller.GetDocuments(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var status = ok.Value.Should().BeAssignableTo<List<StudentDocumentStatusDto>>().Subject;
        status.Should().ContainSingle(s => s.DocumentTypeId == 1 && !s.Uploaded);
    }

    [Fact]
    public async Task GetDocument_ReturnsFile_AfterUpload()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        await controller.UploadDocument(5, 1, MakeFormFile(ValidPdfBytes, "application/pdf"));
        var docId = (await context.StudentDocuments.AsNoTracking().SingleAsync()).Id;

        var result = await controller.GetDocument(5, docId);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        await fileResult.FileStream.DisposeAsync();
    }

    [Fact]
    public async Task GetDocument_Returns404_ForDocumentBelongingToAnotherStudent()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedStudent(context, studentId: 6, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        // Document uploaded for student 5.
        await controller.UploadDocument(5, 1, MakeFormFile(ValidPdfBytes, "application/pdf"));
        var docId = (await context.StudentDocuments.AsNoTracking().SingleAsync()).Id;

        // Try to fetch student 5's document via student 6's route (IDOR).
        var result = await controller.GetDocument(6, docId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteDocument_RemovesFileAndRow()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        await controller.UploadDocument(5, 1, MakeFormFile(ValidPdfBytes, "application/pdf"));
        var docId = (await context.StudentDocuments.AsNoTracking().SingleAsync()).Id;
        File.Exists(Path.Combine(fixture.Dir, $"5_{docId}.pdf")).Should().BeTrue();

        var result = await controller.DeleteDocument(5, docId);

        result.Should().BeOfType<NoContentResult>();
        File.Exists(Path.Combine(fixture.Dir, $"5_{docId}.pdf")).Should().BeFalse();
        (await context.StudentDocuments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDocument_Returns404_ForDocumentBelongingToAnotherStudent()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5, departmentId: 1);
        SeedStudent(context, studentId: 6, departmentId: 1);
        SeedDocumentType(context, id: 1, code: "LISE_DIPLOMA");
        using var fixture = new TempDocStorageFixture();

        var controller = CreateController(context, AllowAllScopeGuard().Object, fixture.Storage, 1, "SystemAdministrator");
        await controller.UploadDocument(5, 1, MakeFormFile(ValidPdfBytes, "application/pdf"));
        var docId = (await context.StudentDocuments.AsNoTracking().SingleAsync()).Id;

        var result = await controller.DeleteDocument(6, docId);

        result.Should().BeOfType<NotFoundObjectResult>();
        (await context.StudentDocuments.AnyAsync()).Should().BeTrue(); // not removed
    }

    private static void SeedStudent(AppDbContext context, int studentId, int departmentId)
    {
        if (!context.Faculties.Any(f => f.Id == 1))
            context.Faculties.Add(new Faculty { Id = 1, Code = "F1", Name = "Mühendislik" });
        if (!context.Departments.Any(d => d.Id == departmentId))
            context.Departments.Add(new Department { Id = departmentId, FacultyId = 1, Code = "YZM", Name = "Yazılım Mühendisliği" });
        if (!context.AcademicPrograms.Any(p => p.Id == departmentId * 100))
            context.AcademicPrograms.Add(new AcademicProgram { Id = departmentId * 100, DepartmentId = departmentId, Code = $"P{departmentId}", Name = "Program" });

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
            ProgramId = departmentId * 100,
            CurriculumId = 1,
            Type = SIS.Domain.Enums.StudentProgramType.Main,
            IsActive = true
        });
        context.SaveChanges();
    }

    private static void SeedDocumentType(AppDbContext context, int id, string code)
    {
        context.DocumentTypes.Add(new DocumentType
        {
            Id = id,
            Code = code,
            Name = code,
            IsRequired = true,
            SortOrder = id,
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
