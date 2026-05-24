using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Services;

public class FileSystemStudentPhotoStorageTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemStudentPhotoStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sis-photo-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private FileSystemStudentPhotoStorage CreateStorage()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StudentPhotos:BasePath"] = _tempDir,
            })
            .Build();

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_tempDir);

        return new FileSystemStudentPhotoStorage(config, env.Object);
    }

    [Fact]
    public async Task SaveAsync_WritesFile_AndReturnsJpgExtension()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 });

        var ext = await storage.SaveAsync(42, content, "image/jpeg");

        ext.Should().Be("jpg");
        File.Exists(Path.Combine(_tempDir, "42.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ReturnsPngExtension_ForPngContentType()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var ext = await storage.SaveAsync(7, content, "image/png");

        ext.Should().Be("png");
        File.Exists(Path.Combine(_tempDir, "7.png")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_Throws_ForUnsupportedContentType()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 0x00 });

        var act = async () => await storage.SaveAsync(1, content, "image/gif");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenFileMissing()
    {
        var storage = CreateStorage();

        var result = await storage.GetAsync(999, "jpg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsStreamAndContentType_WhenPresent()
    {
        var storage = CreateStorage();
        using (var content = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0x01 }))
            await storage.SaveAsync(5, content, "image/jpeg");

        var result = await storage.GetAsync(5, "jpg");

        result.Should().NotBeNull();
        result!.Value.ContentType.Should().Be("image/jpeg");
        await using var s = result.Value.Stream;
        s.Length.Should().Be(4);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var storage = CreateStorage();
        using (var content = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
            await storage.SaveAsync(3, content, "image/png");
        File.Exists(Path.Combine(_tempDir, "3.png")).Should().BeTrue();

        await storage.DeleteAsync(3, "png");

        File.Exists(Path.Combine(_tempDir, "3.png")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_IsNoOp_WhenFileMissing()
    {
        var storage = CreateStorage();

        var act = async () => await storage.DeleteAsync(123, "jpg");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_FallsBackToContentRoot_WhenBasePathNotConfigured()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_tempDir);

        var storage = new FileSystemStudentPhotoStorage(config, env.Object);

        Directory.Exists(Path.Combine(_tempDir, "App_Data", "student-photos")).Should().BeTrue();
    }
}
