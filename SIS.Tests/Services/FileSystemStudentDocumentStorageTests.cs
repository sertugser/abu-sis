using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Services;

public class FileSystemStudentDocumentStorageTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemStudentDocumentStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sis-doc-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private FileSystemStudentDocumentStorage CreateStorage()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StudentDocuments:BasePath"] = _tempDir,
            })
            .Build();

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_tempDir);

        return new FileSystemStudentDocumentStorage(config, env.Object);
    }

    [Fact]
    public async Task SaveAsync_WritesFile_AndReturnsPdfExtension()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var ext = await storage.SaveAsync(42, 3, content, "application/pdf");

        ext.Should().Be("pdf");
        File.Exists(Path.Combine(_tempDir, "42_3.pdf")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ReturnsJpgExtension_ForJpegContentType()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 });

        var ext = await storage.SaveAsync(7, 1, content, "image/jpeg");

        ext.Should().Be("jpg");
        File.Exists(Path.Combine(_tempDir, "7_1.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ReturnsPngExtension_ForPngContentType()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var ext = await storage.SaveAsync(8, 2, content, "image/png");

        ext.Should().Be("png");
        File.Exists(Path.Combine(_tempDir, "8_2.png")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_Throws_ForUnsupportedContentType()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 0x00 });

        var act = async () => await storage.SaveAsync(1, 1, content, "image/gif");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenFileMissing()
    {
        var storage = CreateStorage();

        var result = await storage.GetAsync(999, 1, "pdf");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenStoredExtensionNotWhitelisted()
    {
        var storage = CreateStorage();

        var result = await storage.GetAsync(1, 1, "exe");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsStreamAndContentType_WhenPresent()
    {
        var storage = CreateStorage();
        using (var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x01 }))
            await storage.SaveAsync(5, 9, content, "application/pdf");

        var result = await storage.GetAsync(5, 9, "pdf");

        result.Should().NotBeNull();
        result!.Value.ContentType.Should().Be("application/pdf");
        await using var s = result.Value.Stream;
        s.Length.Should().Be(5);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var storage = CreateStorage();
        using (var content = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
            await storage.SaveAsync(3, 4, content, "image/png");
        File.Exists(Path.Combine(_tempDir, "3_4.png")).Should().BeTrue();

        await storage.DeleteAsync(3, 4, "png");

        File.Exists(Path.Combine(_tempDir, "3_4.png")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_IsNoOp_WhenFileMissing()
    {
        var storage = CreateStorage();

        var act = async () => await storage.DeleteAsync(123, 1, "pdf");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_FallsBackToContentRoot_WhenBasePathNotConfigured()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_tempDir);

        var storage = new FileSystemStudentDocumentStorage(config, env.Object);

        Directory.Exists(Path.Combine(_tempDir, "App_Data", "student-documents")).Should().BeTrue();
    }
}
