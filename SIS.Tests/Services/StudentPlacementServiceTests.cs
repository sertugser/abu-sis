using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Services;

public class StudentPlacementServiceTests
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

    private static UpsertStudentPlacementDto SampleDto(
        string? entranceType = "Osym",
        int? placementYear = 2024,
        decimal? score = 480.5m) => new(
        EntranceType: entranceType,
        ScoreType: "SAY",
        Score: score,
        RankOrder: 1500,
        QuotaType: "Genel",
        PlacementYear: placementYear,
        PreferenceOrder: 3,
        PlacedProgramName: "Bilgisayar Mühendisliği",
        Note: "ÖSYM yerleştirme");

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenStudentDoesNotExist()
    {
        await using var context = CreateContext();
        var service = new StudentPlacementService(context);

        var result = await service.GetAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsEmptyDto_WhenStudentExistsButNoRow()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPlacementService(context);

        // Act
        var result = await service.GetAsync(5);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeNull();
        result.EntranceType.Should().BeNull();
        result.EntranceTypeLabel.Should().BeNull();
        result.Score.Should().BeNull();
        result.PlacedProgramName.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_CreatesRow_WhenAbsent()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPlacementService(context);

        // Act
        await service.UpsertAsync(5, SampleDto());

        // Assert
        var stored = await context.StudentPlacements.AsNoTracking().SingleAsync();
        stored.StudentId.Should().Be(5);
        stored.Score.Should().Be(480.5m);
        stored.CreatedAt.Should().NotBe(default);
        stored.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_UpdatesSameRow_WhenPresent_NoDuplicate()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPlacementService(context);

        // Act
        await service.UpsertAsync(5, SampleDto(score: 400m));
        await service.UpsertAsync(5, SampleDto(score: 450m));

        // Assert
        var rows = await context.StudentPlacements.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Score.Should().Be(450m);
        rows[0].UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsLabel_WhenEntranceTypeSet()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPlacementService(context);

        // Act
        await service.UpsertAsync(5, SampleDto(entranceType: "Dgs"));
        var result = await service.GetAsync(5);

        // Assert
        result.Should().NotBeNull();
        result!.EntranceType.Should().Be("Dgs");
        result.EntranceTypeLabel.Should().Be("DGS");
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenEntranceTypeInvalid()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPlacementService(context);

        // Act
        var act = async () => await service.UpsertAsync(5, SampleDto(entranceType: "Bogus"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.StudentPlacements.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenPlacementYearOutOfRange()
    {
        // Arrange
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPlacementService(context);

        // Act
        var act = async () => await service.UpsertAsync(5, SampleDto(placementYear: 1900));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.StudentPlacements.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenStudentDoesNotExist()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new StudentPlacementService(context);

        // Act
        var act = async () => await service.UpsertAsync(999, SampleDto());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
