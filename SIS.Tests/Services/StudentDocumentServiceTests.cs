using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Seeds;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Services;

public class StudentDocumentServiceTests
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

        // Seed the 8-row document-type catalog.
        await DocumentTypeSeed.SeedAsync(context);
        return context;
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNull_WhenStudentDoesNotExist()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new StudentDocumentService(context);

        var result = await service.GetStatusAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsEightRowsAllNotUploaded_Initially()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new StudentDocumentService(context);

        var result = await service.GetStatusAsync(StudentId);

        result.Should().NotBeNull();
        result!.Should().HaveCount(8);
        result.Should().OnlyContain(r => !r.Uploaded);
        result.Should().OnlyContain(r => r.DocumentId == null);
        result.Select(r => r.SortOrder).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task RecordUploadAsync_MarksTypeUploaded_WithDocumentId()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new StudentDocumentService(context);
        var type = await context.DocumentTypes.FirstAsync(d => d.Code == "FOTO");

        var docId = await service.RecordUploadAsync(StudentId, type.Id, "jpg", "photo.jpg", uploadedByUserId: 42);

        docId.Should().BeGreaterThan(0);

        var status = await service.GetStatusAsync(StudentId);
        var fotoRow = status!.Single(r => r.Code == "FOTO");
        fotoRow.Uploaded.Should().BeTrue();
        fotoRow.DocumentId.Should().Be(docId);
        fotoRow.OriginalFileName.Should().Be("photo.jpg");
        fotoRow.UploadedAt.Should().NotBeNull();

        // Other rows remain not-uploaded.
        status!.Count(r => r.Uploaded).Should().Be(1);
    }

    [Fact]
    public async Task RecordUploadAsync_Upserts_DoesNotCreateSecondRow()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new StudentDocumentService(context);
        var type = await context.DocumentTypes.FirstAsync(d => d.Code == "NUFUS");

        var firstId = await service.RecordUploadAsync(StudentId, type.Id, "pdf", "first.pdf", null);
        var secondId = await service.RecordUploadAsync(StudentId, type.Id, "png", "second.png", null);

        secondId.Should().Be(firstId);
        var rows = await context.StudentDocuments
            .Where(d => d.StudentId == StudentId && d.DocumentTypeId == type.Id)
            .ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].FileExtension.Should().Be("png");
        rows[0].OriginalFileName.Should().Be("second.png");
    }

    [Fact]
    public async Task RecordUploadAsync_Throws_WhenDocumentTypeMissing()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new StudentDocumentService(context);

        var act = () => service.RecordUploadAsync(StudentId, documentTypeId: 9999, "pdf", null, null);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RemoveAsync_RevertsUploadedStatus()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new StudentDocumentService(context);
        var type = await context.DocumentTypes.FirstAsync(d => d.Code == "SAGLIK");
        var docId = await service.RecordUploadAsync(StudentId, type.Id, "pdf", "saglik.pdf", null);

        await service.RemoveAsync(StudentId, docId);

        var status = await service.GetStatusAsync(StudentId);
        status!.Single(r => r.Code == "SAGLIK").Uploaded.Should().BeFalse();
        status.Should().OnlyContain(r => !r.Uploaded);
    }

    [Fact]
    public async Task RemoveAsync_DoesNothing_WhenDocumentBelongsToAnotherStudent()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new StudentDocumentService(context);
        var type = await context.DocumentTypes.FirstAsync(d => d.Code == "DENKLIK");
        var docId = await service.RecordUploadAsync(StudentId, type.Id, "pdf", null, null);

        await service.RemoveAsync(studentId: 777, docId);

        // Still present for the real owner.
        (await context.StudentDocuments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnsDoc_OnlyForOwningStudent()
    {
        await using var context = await CreateSeededContextAsync();
        var service = new StudentDocumentService(context);
        var type = await context.DocumentTypes.FirstAsync(d => d.Code == "LISE_DIPLOMA");
        var docId = await service.RecordUploadAsync(StudentId, type.Id, "pdf", "diploma.pdf", null);

        var owned = await service.GetDocumentAsync(StudentId, docId);
        owned.Should().NotBeNull();
        owned!.DocumentTypeId.Should().Be(type.Id);

        var foreign = await service.GetDocumentAsync(studentId: 777, docId);
        foreign.Should().BeNull();
    }
}
