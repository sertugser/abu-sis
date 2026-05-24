using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Services;

public class IssuedDocumentServiceTests
{
    private const int StudentId = 1;

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
        context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = 1001,
            StudentNumber = "S1",
            NationalId = "N1",
        });
        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task RecordAsync_InsertsRow_WithStudentInfoFormDocumentNumber()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new IssuedDocumentService(context);

        var id = await service.RecordAsync(StudentId, IssuedDocumentKind.StudentInfoForm, issuedByUserId: 42);

        id.Should().BeGreaterThan(0);
        var doc = await context.IssuedDocuments.SingleAsync();
        doc.Id.Should().Be(id);
        doc.StudentId.Should().Be(StudentId);
        doc.Kind.Should().Be(IssuedDocumentKind.StudentInfoForm);
        doc.IssuedByUserId.Should().Be(42);
        doc.DocumentNumber.Should().MatchRegex(@"^OBF-\d{4}-\d{6}$");
    }

    [Fact]
    public async Task RecordAsync_IncrementsSequence_ForSecondSameKindRecord()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new IssuedDocumentService(context);

        await service.RecordAsync(StudentId, IssuedDocumentKind.StudentInfoForm, null);
        await service.RecordAsync(StudentId, IssuedDocumentKind.StudentInfoForm, null);

        var numbers = await context.IssuedDocuments
            .OrderBy(d => d.Id)
            .Select(d => d.DocumentNumber)
            .ToListAsync();

        var year = DateTime.UtcNow.Year;
        numbers.Should().Equal($"OBF-{year:D4}-000001", $"OBF-{year:D4}-000002");
    }

    [Fact]
    public async Task RecordAsync_Throws_WhenStudentMissing()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new IssuedDocumentService(context);

        var act = () => service.RecordAsync(studentId: 9999, IssuedDocumentKind.EnrollmentCertificate, null);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsNull_WhenStudentMissing()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new IssuedDocumentService(context);

        var result = await service.GetHistoryAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsNewestFirst_AndResolvesIssuedByName()
    {
        await using var context = await CreateSeededContextAsync();
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 42, FirstName = "Ayse", LastName = "Memur" });
        await context.SaveChangesAsync();

        var older = new IssuedDocument
        {
            StudentId = StudentId,
            Kind = IssuedDocumentKind.StudentInfoForm,
            IssuedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            IssuedByUserId = 42,
            DocumentNumber = "OBF-2026-000001",
        };
        var newer = new IssuedDocument
        {
            StudentId = StudentId,
            Kind = IssuedDocumentKind.EnrollmentCertificate,
            IssuedAt = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
            IssuedByUserId = null,
            DocumentNumber = "OGB-2026-000001",
        };
        context.IssuedDocuments.AddRange(older, newer);
        await context.SaveChangesAsync();

        var service = new IssuedDocumentService(context);
        var result = await service.GetHistoryAsync(StudentId);

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);

        // Newest first.
        result[0].Kind.Should().Be(nameof(IssuedDocumentKind.EnrollmentCertificate));
        result[0].KindLabel.Should().Be("Öğrenci Belgesi");
        result[0].IssuedByName.Should().BeNull();

        result[1].Kind.Should().Be(nameof(IssuedDocumentKind.StudentInfoForm));
        result[1].KindLabel.Should().Be("Öğrenci Bilgi Formu");
        result[1].IssuedByName.Should().Be("Ayse Memur");
    }
}
