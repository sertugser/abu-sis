using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SIS.Application.Announcement.Attachments;

namespace SIS.Infrastructure.Services.Announcement;

/// <summary>
/// Yerel dosya sistemine yazan <see cref="IAnnouncementAttachmentStorage"/> implementasyonu.
/// Dosyalar <c>{RootPath}/yyyy/MM/{GUID}{uzantı}</c> şeklinde tarih bazlı alt klasörlere
/// dağıtılır — tek klasörde milyonlarca dosya birikmesin.
/// </summary>
public sealed class FileSystemAnnouncementAttachmentStorage : IAnnouncementAttachmentStorage
{
    private readonly string _absoluteRoot;

    public FileSystemAnnouncementAttachmentStorage(
        IOptions<AttachmentStorageOptions> options,
        IHostEnvironment env)
    {
        var configured = options.Value.RootPath;
        _absoluteRoot = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);

        Directory.CreateDirectory(_absoluteRoot);
    }

    public async Task<string> StoreAsync(string originalFileName, Stream content, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
            throw new InvalidOperationException("Dosya uzantısı belirlenemedi.");

        // tarih bazlı alt klasör: 2026/05/abcd-...pdf
        var now = DateTime.UtcNow;
        var relativeDir = Path.Combine(now.Year.ToString("D4"), now.Month.ToString("D2"));
        var absoluteDir = Path.Combine(_absoluteRoot, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var storageName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativePath = Path.Combine(relativeDir, storageName).Replace('\\', '/');
        var absolutePath = Path.Combine(absoluteDir, storageName);

        await using (var fileStream = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return relativePath;
    }

    public Task<AttachmentReadResult?> OpenReadAsync(string storageFileName, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolutePath(storageFileName);
        if (absolutePath is null || !File.Exists(absolutePath))
            return Task.FromResult<AttachmentReadResult?>(null);

        var info = new FileInfo(absolutePath);
        var stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<AttachmentReadResult?>(new AttachmentReadResult(stream, info.Length));
    }

    public Task DeleteAsync(string storageFileName, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolutePath(storageFileName);
        if (absolutePath is not null && File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Storage adının kök dizinden çıkmadığını doğrular (path traversal koruması).
    /// </summary>
    private string? ResolveAbsolutePath(string storageFileName)
    {
        if (string.IsNullOrWhiteSpace(storageFileName))
            return null;

        // Mutlak path, ".." veya sürücü harfi kabul edilmez
        if (Path.IsPathRooted(storageFileName) || storageFileName.Contains("..", StringComparison.Ordinal))
            return null;

        var combined = Path.GetFullPath(Path.Combine(_absoluteRoot, storageFileName));
        var rootNormalized = Path.GetFullPath(_absoluteRoot);

        // Combined path mutlaka kök altında olmalı
        if (!combined.StartsWith(rootNormalized, StringComparison.OrdinalIgnoreCase))
            return null;

        return combined;
    }
}
