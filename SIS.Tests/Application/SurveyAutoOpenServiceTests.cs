using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.BackgroundServices;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Application;

public class SurveyAutoOpenServiceTests
{
    [Fact]
    public async Task ProcessSurveysAsync_UpdatesScheduledAndExpiredSurveys()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new AppDbContext(options);
        await SeedAsync(context);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(provider => provider.GetService(typeof(AppDbContext)))
            .Returns(context);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock
            .SetupGet(scope => scope.ServiceProvider)
            .Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(factory => factory.CreateScope())
            .Returns(scopeMock.Object);

        var service = new SurveyAutoOpenService(
            scopeFactoryMock.Object,
            Mock.Of<ILogger<SurveyAutoOpenService>>());

        var changedCount = await service.ProcessSurveysAsync();

        changedCount.Should().Be(2);
        var surveys = await context.CourseIntentionSurveys
            .OrderBy(s => s.Id)
            .ToListAsync();

        surveys[0].Status.Should().Be(CourseIntentionSurveyStatus.Open);
        surveys[1].Status.Should().Be(CourseIntentionSurveyStatus.Closed);
    }

    private static async Task SeedAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;

        context.AcademicTerms.AddRange(
            new AcademicTerm
            {
                Id = 1,
                Name = "2025-2026 Fall",
                Year = 2025,
                Semester = SemesterType.Fall,
                RegistrationStart = now.AddDays(-2),
                RegistrationEnd = now.AddDays(5),
                AddDropStart = now.AddDays(6),
                AddDropEnd = now.AddDays(12),
                WithdrawalDeadline = now.AddDays(40),
                TermStart = now.AddDays(-10),
                TermEnd = now.AddMonths(4),
                IsActive = true,
                Status = TermStatus.RegistrationOpen
            },
            new AcademicTerm
            {
                Id = 2,
                Name = "2026-2027 Spring",
                Year = 2026,
                Semester = SemesterType.Spring,
                RegistrationStart = now.AddMonths(5),
                RegistrationEnd = now.AddMonths(5).AddDays(7),
                AddDropStart = now.AddMonths(5).AddDays(8),
                AddDropEnd = now.AddMonths(5).AddDays(14),
                WithdrawalDeadline = now.AddMonths(6),
                TermStart = now.AddMonths(5),
                TermEnd = now.AddMonths(9),
                IsActive = false,
                Status = TermStatus.Draft
            });

        context.CourseIntentionSurveys.AddRange(
            new CourseIntentionSurvey
            {
                Id = 1,
                TermId = 1,
                Title = "Fall Survey",
                Status = CourseIntentionSurveyStatus.Scheduled,
                OpenAtUtc = now.AddHours(-1),
                CloseAtUtc = now.AddDays(2),
                CreatedAt = now.AddDays(-1)
            },
            new CourseIntentionSurvey
            {
                Id = 2,
                TermId = 2,
                Title = "Spring Survey",
                Status = CourseIntentionSurveyStatus.Open,
                OpenAtUtc = now.AddDays(-3),
                CloseAtUtc = now.AddHours(-2),
                CreatedAt = now.AddDays(-4)
            });

        await context.SaveChangesAsync();
    }
}
