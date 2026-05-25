using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Services;

public class StudentScholarshipServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static void SeedStudent(AppDbContext context, int studentId)
    {
        context.Students.Add(new Student
        {
            Id = studentId,
            UserId = 1000 + studentId,
            StudentNumber = $"S{studentId}",
            NationalId = $"N{studentId}",
        });
        context.SaveChanges();
    }

    private static UpsertScholarshipDetailDto SampleDto(
        string type = "MeritScholarship",
        decimal? ratePercent = 50m,
        string? source = "Üniversite",
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool isActive = true,
        string? note = "Burs notu") => new(
        Type: type,
        RatePercent: ratePercent,
        Source: source,
        StartDate: startDate,
        EndDate: endDate,
        IsActive: isActive,
        Note: note);

    [Fact]
    public async Task CreateAsync_InsertsRow_AndReturnsRetrievableId()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentScholarshipService(context);

        // Act
        var id = await service.CreateAsync(5, SampleDto());

        // Assert
        id.Should().BeGreaterThan(0);
        var list = await service.GetForStudentAsync(5);
        list.Should().NotBeNull();
        list!.Should().ContainSingle(d => d.Id == id);
        list![0].Type.Should().Be("MeritScholarship");
        list[0].TypeLabel.Should().Be("Başarı Bursu");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenRatePercentExceedsHundred()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentScholarshipService(context);

        // Act
        var act = async () => await service.CreateAsync(5, SampleDto(ratePercent: 150m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.StudentScholarshipDetails.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenRatePercentBelowZero()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentScholarshipService(context);

        // Act
        var act = async () => await service.CreateAsync(5, SampleDto(ratePercent: -1m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.StudentScholarshipDetails.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenEndDateBeforeStartDate()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentScholarshipService(context);

        // Act
        var act = async () => await service.CreateAsync(5, SampleDto(
            startDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.StudentScholarshipDetails.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTypeIsInvalid()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentScholarshipService(context);

        // Act
        var act = async () => await service.CreateAsync(5, SampleDto(type: "Bogus"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.StudentScholarshipDetails.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenStudentDoesNotExist()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new StudentScholarshipService(context);

        // Act
        var act = async () => await service.CreateAsync(999, SampleDto());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetForStudentAsync_ReturnsNull_WhenStudentDoesNotExist()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new StudentScholarshipService(context);

        // Act
        var result = await service.GetForStudentAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetForStudentAsync_OrdersActiveFirstThenStartDateDescending()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentScholarshipService(context);

        var inactiveId = await service.CreateAsync(5, SampleDto(
            isActive: false,
            startDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var olderActiveId = await service.CreateAsync(5, SampleDto(
            isActive: true,
            startDate: new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var newerActiveId = await service.CreateAsync(5, SampleDto(
            isActive: true,
            startDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Act
        var result = await service.GetForStudentAsync(5);

        // Assert
        result.Should().NotBeNull();
        result!.Select(d => d.Id).Should().ContainInOrder(newerActiveId, olderActiveId, inactiveId);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenRecordBelongsToDifferentStudent()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        SeedStudent(context, studentId: 6);
        var service = new StudentScholarshipService(context);
        var id = await service.CreateAsync(5, SampleDto());

        // Act
        var act = async () => await service.UpdateAsync(6, id, SampleDto(ratePercent: 25m));

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenRecordBelongsToDifferentStudent()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        SeedStudent(context, studentId: 6);
        var service = new StudentScholarshipService(context);
        var id = await service.CreateAsync(5, SampleDto());

        // Act
        var act = async () => await service.DeleteAsync(6, id);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
        (await context.StudentScholarshipDetails.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_RemovesRow()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentScholarshipService(context);
        var id = await service.CreateAsync(5, SampleDto());

        // Act
        await service.DeleteAsync(5, id);

        // Assert
        (await context.StudentScholarshipDetails.AnyAsync()).Should().BeFalse();
    }
}
