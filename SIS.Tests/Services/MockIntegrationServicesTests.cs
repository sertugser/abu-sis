using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Integration;

namespace SIS.Tests.Services;

public class MockIntegrationServicesTests
{
    private const int StudentId = 7;
    private const int MissingStudentId = 999;

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static void SeedStudent(AppDbContext context)
    {
        context.Faculties.Add(new Faculty { Id = 1, Code = "F1", Name = "Mühendislik" });
        context.Departments.Add(new Department { Id = 1, FacultyId = 1, Code = "YZM", Name = "Yazılım Mühendisliği" });
        context.AcademicPrograms.Add(new AcademicProgram { Id = 10, DepartmentId = 1, Code = "YZM-LIS", Name = "Yazılım Mühendisliği Lisans" });
        context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = 1000 + StudentId,
            StudentNumber = $"S{StudentId}",
            NationalId = $"N{StudentId}",
            PersonalInfo = new StudentPersonalInfo
            {
                StudentId = StudentId,
                BirthDate = new DateTime(2003, 5, 1),
                BirthPlace = "İzmir",
                AddressProvince = "İzmir",
                AddressDistrict = "Bornova",
            },
        });
        context.Set<StudentProgram>().Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = 10,
            CurriculumId = 1,
            Type = StudentProgramType.Main,
            EnrollmentDate = new DateTime(2021, 9, 1),
            IsActive = true,
        });
        context.SaveChanges();
    }

    private static IHttpClientFactory HttpFactory() => new Mock<IHttpClientFactory>().Object;

    private static ISystemConfigProvider ModeProvider(string mode)
    {
        var mock = new Mock<ISystemConfigProvider>();
        mock.Setup(p => p.GetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mode);
        return mock.Object;
    }

    // ── NVİ identity ────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ReturnsMockMatch_WhenModeIsDefaultMock()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context);
        var service = new MockNviIdentityService(context, new NullSystemConfigProvider(), HttpFactory());

        // Act
        var result = await service.VerifyAsync(StudentId);

        // Assert
        result.IsMock.Should().BeTrue();
        result.Matched.Should().BeTrue();
        result.FullName.Should().NotBeNullOrWhiteSpace();
        result.Mode.Should().Be("Mock");
    }

    [Fact]
    public async Task VerifyAsync_ThrowsKeyNotFound_WhenStudentMissing()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context);
        var service = new MockNviIdentityService(context, new NullSystemConfigProvider(), HttpFactory());

        // Act
        var act = async () => await service.VerifyAsync(MissingStudentId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task VerifyAsync_ThrowsInvalidOperation_WhenModeDisabled()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context);
        var service = new MockNviIdentityService(context, ModeProvider("Disabled"), HttpFactory());

        // Act
        var act = async () => await service.VerifyAsync(StudentId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task VerifyAsync_ThrowsNotImplemented_WhenModeLive()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context);
        var service = new MockNviIdentityService(context, ModeProvider("Live"), HttpFactory());

        // Act
        var act = async () => await service.VerifyAsync(StudentId);

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }

    // ── YÖKSİS reporting ────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ReturnsReportedStatus_WithMatchingRecordNo()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context);
        var service = new MockYoksisReportingService(context, new NullSystemConfigProvider(), HttpFactory());

        // Act
        var result = await service.GetStatusAsync(StudentId);

        // Assert
        result.IsMock.Should().BeTrue();
        result.Status.Should().Be("Bildirildi");
        result.YoksisRecordNo.Should().Be($"YOK-{result.QueriedAt:yyyy}-{StudentId:D6}");
    }

    [Fact]
    public async Task SubmitAsync_ReturnsSubmittedStatus_WithMatchingRecordNo()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context);
        var service = new MockYoksisReportingService(context, new NullSystemConfigProvider(), HttpFactory());

        // Act
        var result = await service.SubmitAsync(StudentId);

        // Assert
        result.IsMock.Should().BeTrue();
        result.Status.Should().Be("Gönderildi");
        result.YoksisRecordNo.Should().Be($"YOK-{result.QueriedAt:yyyy}-{StudentId:D6}");
    }

    [Fact]
    public async Task QueryPlacementAsync_ReturnsDeterministicOsymPlacement()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context);
        var service = new MockYoksisReportingService(context, new NullSystemConfigProvider(), HttpFactory());

        // Act
        var first = await service.QueryPlacementAsync(StudentId);
        var second = await service.QueryPlacementAsync(StudentId);

        // Assert
        first.IsMock.Should().BeTrue();
        first.EntranceType.Should().Be("Osym");
        first.Score.Should().Be(second.Score);
    }

    [Fact]
    public async Task QueryPlacementAsync_ThrowsKeyNotFound_WhenStudentMissing()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context);
        var service = new MockYoksisReportingService(context, new NullSystemConfigProvider(), HttpFactory());

        // Act
        var act = async () => await service.QueryPlacementAsync(MissingStudentId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
