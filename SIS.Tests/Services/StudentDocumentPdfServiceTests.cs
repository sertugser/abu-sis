using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Services;

public class StudentDocumentPdfServiceTests
{
    private const int StudentId = 5;

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 5, 25, 9, 0, 0, DateTimeKind.Utc);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<AppDbContext> CreateSeededContextAsync()
    {
        var context = CreateContext();

        context.Faculties.Add(new Faculty { Id = 1, Code = "F1", Name = "Mühendislik Fakültesi" });
        context.Departments.Add(new Department { Id = 1, FacultyId = 1, Code = "YZM", Name = "Yazılım Mühendisliği" });
        context.AcademicPrograms.Add(new AcademicProgram { Id = 10, DepartmentId = 1, Code = "YZM-LIS", Name = "Yazılım Mühendisliği Lisans" });

        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 1005, FirstName = "Ahmet", LastName = "Yılmaz" });

        context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = 1005,
            StudentNumber = "2024001",
            NationalId = "N5",
            Status = StudentStatus.Active,
            CompletedSemesters = 4,
        });
        context.Set<StudentProgram>().Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = 10,
            CurriculumId = 1,
            Type = StudentProgramType.Main,
            IsActive = true,
        });
        context.StudentPersonalInfos.Add(new StudentPersonalInfo
        {
            StudentId = StudentId,
            BirthDate = new DateTime(2003, 3, 15),
            BirthPlace = "Ankara",
            Gender = Gender.Male,
            Nationality = "T.C.",
            BloodType = "A Rh+",
            MaritalStatus = MaritalStatus.Single,
            MobilePhone = "5551234567",
            SecondaryEmail = "ahmet@example.com",
            AddressProvince = "Ankara",
            AddressDistrict = "Çankaya",
            AddressLine = "Atatürk Bulvarı No:1",
            MilitaryStatus = MilitaryServiceStatus.Deferred,
            CreatedAt = DateTime.UtcNow,
        });

        await context.SaveChangesAsync();
        return context;
    }

    private static StudentDocumentPdfService CreateService(AppDbContext context) =>
        new(context, new NullStudentPhotoStorage(), new FixedClock());

    [Fact]
    public async Task GenerateStudentInfoFormAsync_ReturnsPdfBytes_ForSeededStudent()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateService(context);

        var pdf = await service.GenerateStudentInfoFormAsync(StudentId);

        pdf.Should().NotBeNull();
        pdf!.Length.Should().BeGreaterThan(0);
        IsPdf(pdf).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateStudentInfoFormAsync_ReturnsNull_WhenStudentMissing()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateService(context);

        var pdf = await service.GenerateStudentInfoFormAsync(9999);

        pdf.Should().BeNull();
    }

    [Fact]
    public async Task GenerateEnrollmentCertificateAsync_ReturnsPdfBytes_ForSeededStudent()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateService(context);

        var pdf = await service.GenerateEnrollmentCertificateAsync(StudentId);

        pdf.Should().NotBeNull();
        pdf!.Length.Should().BeGreaterThan(0);
        IsPdf(pdf).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEnrollmentCertificateAsync_ReturnsNull_WhenStudentMissing()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateService(context);

        var pdf = await service.GenerateEnrollmentCertificateAsync(9999);

        pdf.Should().BeNull();
    }

    [Fact]
    public async Task GenerateStudentInfoFormAsync_Succeeds_WhenPersonalInfoMissing()
    {
        await using var context = await CreateSeededContextAsync();
        var info = await context.StudentPersonalInfos.SingleAsync();
        context.StudentPersonalInfos.Remove(info);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var pdf = await service.GenerateStudentInfoFormAsync(StudentId);

        pdf.Should().NotBeNull();
        IsPdf(pdf!).Should().BeTrue();
    }

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length >= 5
        && bytes[0] == (byte)'%'
        && bytes[1] == (byte)'P'
        && bytes[2] == (byte)'D'
        && bytes[3] == (byte)'F'
        && bytes[4] == (byte)'-';
}
