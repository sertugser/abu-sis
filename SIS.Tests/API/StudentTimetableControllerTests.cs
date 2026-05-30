using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public sealed class StudentTimetableControllerTests
{
    [Fact]
    public async Task GetTimetable_ReturnsOk_WithServiceResult_WhenStudentExists()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 7, userId: 42);

        var expected = new StudentTimetableDto(
            StudentName: "Test Öğrenci",
            TermId: 10,
            TermName: "2025-2026 Bahar",
            Sections: new List<StudentTimetableSectionDto>(),
            LunchBreakStart: "11:40",
            LunchBreakEnd: "12:20",
            Slots: new List<TimetableSlotDto>());

        var serviceMock = new Mock<IStudentTimetableService>();
        serviceMock
            .Setup(s => s.GetTimetableAsync(7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.GetTimetable(termId: null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetTimetable_ReturnsUnauthorized_WhenUserHasNoStudent()
    {
        await using var context = CreateContext();
        // Hiç student seed edilmedi
        var serviceMock = new Mock<IStudentTimetableService>(MockBehavior.Strict);

        var controller = CreateController(context, serviceMock.Object, userId: 999);

        var result = await controller.GetTimetable(termId: null, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetTimetable_ReturnsBadRequest_WhenTermIdInvalid()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 7, userId: 42);
        var serviceMock = new Mock<IStudentTimetableService>(MockBehavior.Strict);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.GetTimetable(termId: -5, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetTerms_ReturnsOk_WithEnrolledTerms()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 7, userId: 42);

        var expected = new List<StudentTimetableTermDto>
        {
            new(10, "2025-2026 Bahar", IsDefault: true),
            new(11, "2025-2026 Güz", IsDefault: false),
        };
        var serviceMock = new Mock<IStudentTimetableService>();
        serviceMock
            .Setup(s => s.GetEnrolledTermsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.GetTerms(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetPdf_ReturnsFileResult_WithPdfContentType()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 7, userId: 42);

        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0xAA, 0xBB };
        var serviceMock = new Mock<IStudentTimetableService>();
        serviceMock
            .Setup(s => s.GeneratePdfAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.GetPdf(termId: 10, CancellationToken.None);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileContents.Should().BeEquivalentTo(pdfBytes);
        fileResult.FileDownloadName.Should().Be("ders-programi.pdf");
    }

    [Fact]
    public async Task GetPdf_ReturnsBadRequest_WhenTermIdMissing()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 7, userId: 42);
        var serviceMock = new Mock<IStudentTimetableService>(MockBehavior.Strict);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.GetPdf(termId: 0, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static void SeedStudent(AppDbContext context, int studentId, int userId)
    {
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "ogrenci@ankarabilim.edu.tr",
            NormalizedUserName = "OGRENCI@ANKARABILIM.EDU.TR",
            Email = "ogrenci@ankarabilim.edu.tr",
            NormalizedEmail = "OGRENCI@ANKARABILIM.EDU.TR",
            FirstName = "Sertuğ",
            LastName = "Ser",
        });
        context.Students.Add(new Student
        {
            Id = studentId,
            UserId = userId,
            StudentNumber = "20220001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            CreatedAt = DateTime.UtcNow,
        });
        context.SaveChanges();
    }

    private static StudentTimetableController CreateController(
        AppDbContext context,
        IStudentTimetableService service,
        int userId)
    {
        var controller = new StudentTimetableController(service, context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "TestAuth"))
            }
        };
        return controller;
    }
}
