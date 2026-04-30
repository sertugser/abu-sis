using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class CourseControllerTests
{
    [Fact]
    public async Task GetActiveTerm_WhenAcademicCalendarClosedAndWebWindowInactive_DoesNotReturnWebOpenAtMessage()
    {
        await using var context = CreateContext();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 2, 16, 6, 0, 0, DateTimeKind.Utc) };
        SeedActiveTerm(context, clock.UtcNow, registrationOpen: false);

        var webSettingsMock = new Mock<IWebModuleSettingsProvider>(MockBehavior.Strict);
        webSettingsMock
            .Setup(service => service.GetBoolAsync("StudentPanel", "CourseRegistrationEnabled", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        webSettingsMock
            .Setup(service => service.GetBoolAsync("StudentPanel", "CourseRegistrationEnabled", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new CourseController(context, clock, webSettingsMock.Object);

        var result = await controller.GetActiveTerm();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ActiveTermDto>().Subject;
        dto.IsRegistrationOpen.Should().BeFalse();
        dto.StudentRegistrationMessage.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveTerm_WhenAcademicCalendarOpenButOpenAtNotReached_ReturnsOpenAtMessage()
    {
        await using var context = CreateContext();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 2, 16, 4, 0, 0, DateTimeKind.Utc) }; // 07:00 TR
        SeedActiveTerm(context, clock.UtcNow, registrationOpen: true);

        var webSettingsMock = new Mock<IWebModuleSettingsProvider>(MockBehavior.Strict);
        webSettingsMock
            .Setup(service => service.GetBoolAsync("StudentPanel", "CourseRegistrationEnabled", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        webSettingsMock
            .Setup(service => service.GetBoolAsync("StudentPanel", "CourseRegistrationEnabled", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        webSettingsMock
            .Setup(service => service.GetTimeAsync("StudentPanel", "CourseRegistrationOpenAt", TimeOnly.MinValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimeOnly(9, 0));

        var controller = new CourseController(context, clock, webSettingsMock.Object);

        var result = await controller.GetActiveTerm();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ActiveTermDto>().Subject;
        dto.IsRegistrationOpen.Should().BeFalse();
        dto.StudentRegistrationMessage.Should().Be("Ders kayıt işlemleri bugün saat 09:00 itibarıyla açılacaktır.");
    }

    [Fact]
    public async Task GetActiveTerm_WhenAcademicCalendarClosedButWebWindowActive_ReturnsRegistrationOpen()
    {
        await using var context = CreateContext();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 2, 16, 8, 0, 0, DateTimeKind.Utc) }; // 11:00 TR
        SeedActiveTerm(context, clock.UtcNow, registrationOpen: false);

        var webSettingsMock = new Mock<IWebModuleSettingsProvider>(MockBehavior.Strict);
        webSettingsMock
            .Setup(service => service.GetBoolAsync("StudentPanel", "CourseRegistrationEnabled", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        webSettingsMock
            .Setup(service => service.GetBoolAsync("StudentPanel", "CourseRegistrationEnabled", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        webSettingsMock
            .Setup(service => service.GetTimeAsync("StudentPanel", "CourseRegistrationOpenAt", TimeOnly.MinValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimeOnly(9, 0));

        var controller = new CourseController(context, clock, webSettingsMock.Object);

        var result = await controller.GetActiveTerm();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ActiveTermDto>().Subject;
        dto.IsRegistrationOpen.Should().BeTrue();
        dto.IsAddDropOpen.Should().BeFalse();
        dto.StudentRegistrationMessage.Should().BeNull();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedActiveTerm(AppDbContext context, DateTime nowUtc, bool registrationOpen)
    {
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Name = "2025-2026 Bahar",
            Year = 2025,
            TermType = TermType.Spring,
            TermStart = nowUtc.AddDays(-30),
            TermEnd = nowUtc.AddDays(30),
            AcademicCalendar = new TermAcademicCalendar
            {
                RegistrationStart = registrationOpen ? nowUtc.AddDays(-1) : nowUtc.AddDays(-10),
                RegistrationEnd = registrationOpen ? nowUtc.AddDays(1) : nowUtc.AddDays(-5),
                AddDropStart = nowUtc.AddDays(2),
                AddDropEnd = nowUtc.AddDays(7),
                WithdrawalDeadline = nowUtc.AddDays(20),
                GradeEntryStart = nowUtc.AddDays(25),
                GradeEntryEnd = nowUtc.AddDays(35),
                CreatedAt = nowUtc.AddDays(-40)
            }
        });

        context.SaveChanges();
    }
}
