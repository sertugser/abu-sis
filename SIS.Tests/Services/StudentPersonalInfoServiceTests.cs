using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Services;

public class StudentPersonalInfoServiceTests
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

    private static UpdateStudentPersonalInfoDto SampleDto(
        string? mobile = "0500 000 00 00",
        string? gender = "Male",
        string? marital = "Single",
        string? military = "Deferred") => new(
        MobilePhone: mobile,
        Phone: "0312 000 00 00",
        SecondaryEmail: "alt@example.com",
        AddressProvince: "Ankara",
        AddressDistrict: "Çankaya",
        AddressLine: "Test Mah. 1. Cad. No:1",
        PostalCode: "06000",
        EmergencyContactName: "Veli Veli",
        EmergencyContactPhone: "0533 000 00 00",
        EmergencyContactRelation: "Baba",
        BirthDate: new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        BirthPlace: "İstanbul",
        Gender: gender,
        Nationality: "TC",
        BloodType: "A+",
        MaritalStatus: marital,
        PassportNo: "U1234567",
        MilitaryStatus: military,
        MilitaryDeferralUntil: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        MilitaryOffice: "Çankaya Askerlik Şubesi",
        PhotoExtension: ".jpg");

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenStudentDoesNotExist()
    {
        await using var context = CreateContext();
        var service = new StudentPersonalInfoService(context);

        var result = await service.GetAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsEmptyDto_WhenStudentExistsButNoRow()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPersonalInfoService(context);

        var result = await service.GetAsync(5);

        result.Should().NotBeNull();
        result!.StudentId.Should().Be(5);
        result.MobilePhone.Should().BeNull();
        result.Gender.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_CreatesRow_WhenAbsent()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPersonalInfoService(context);

        await service.UpsertAsync(5, SampleDto());

        var stored = await context.StudentPersonalInfos.AsNoTracking().SingleAsync();
        stored.StudentId.Should().Be(5);
        stored.MobilePhone.Should().Be("0500 000 00 00");
        stored.CreatedAt.Should().NotBe(default);
        stored.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_UpdatesRow_WhenPresent()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPersonalInfoService(context);

        await service.UpsertAsync(5, SampleDto(mobile: "0500 111 11 11"));
        await service.UpsertAsync(5, SampleDto(mobile: "0500 222 22 22"));

        var rows = await context.StudentPersonalInfos.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].MobilePhone.Should().Be("0500 222 22 22");
        rows[0].UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsSavedValues_WithEnumStringRoundTrip()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPersonalInfoService(context);

        await service.UpsertAsync(5, SampleDto(gender: "Female", marital: "Married", military: "Exempt"));

        var result = await service.GetAsync(5);
        result.Should().NotBeNull();
        result!.Gender.Should().Be("Female");
        result.MaritalStatus.Should().Be("Married");
        result.MilitaryStatus.Should().Be("Exempt");
        result.BirthPlace.Should().Be("İstanbul");
        result.PassportNo.Should().Be("U1234567");
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenStudentDoesNotExist()
    {
        await using var context = CreateContext();
        var service = new StudentPersonalInfoService(context);

        var act = async () => await service.UpsertAsync(999, SampleDto());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenEnumStringInvalid()
    {
        await using var context = CreateContext();
        SeedStudent(context, studentId: 5);
        var service = new StudentPersonalInfoService(context);

        var act = async () => await service.UpsertAsync(5, SampleDto(gender: "Helicopter"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        // No partial write should have occurred.
        (await context.StudentPersonalInfos.AnyAsync()).Should().BeFalse();
    }
}
