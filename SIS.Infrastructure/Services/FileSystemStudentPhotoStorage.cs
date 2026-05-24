using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SIS.Application.Common.Services;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Öğrenci profil fotoğraflarını yerel dosya sistemine yazan
/// <see cref="IStudentPhotoStorage"/> implementasyonu.
///
/// Güvenlik: Dosya adı tamamen SUNUCU tarafından üretilir
/// (<c>{studentId}.{ext}</c>) — istemci kaynaklı hiçbir dosya adı kullanılmaz,
/// dolayısıyla path traversal mümkün değildir. <c>studentId</c> int olduğundan
/// path'e güvenle enterpole edilir. Uzantı yalnızca whitelist'ten ("jpg"/"png")
/// türetilir.
/// </summary>
public sealed class FileSystemStudentPhotoStorage : IStudentPhotoStorage
{
    private const string JpegContentType = "image/jpeg";
    private const string PngContentType = "image/png";

    private readonly string _basePath;

    public FileSystemStudentPhotoStorage(IConfiguration configuration, IHostEnvironment env)
    {
        var configured = configuration["StudentPhotos:BasePath"];
        _basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "student-photos")
            : (Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(env.ContentRootPath, configured));

        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(int studentId, Stream content, string contentType, CancellationToken ct = default)
    {
        var extension = ExtensionFromContentType(contentType)
            ?? throw new InvalidOperationException($"Desteklenmeyen içerik tipi: {contentType}");

        var absolutePath = BuildPath(studentId, extension);

        await using var fileStream = new FileStream(
            absolutePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        if (content.CanSeek)
            content.Position = 0;
        await content.CopyToAsync(fileStream, ct);

        return extension;
    }

    public Task<(Stream Stream, string ContentType)?> GetAsync(int studentId, string extension, CancellationToken ct = default)
    {
        var normalized = NormalizeExtension(extension);
        if (normalized is null)
            return Task.FromResult<(Stream, string)?>(null);

        var absolutePath = BuildPath(studentId, normalized);
        if (!File.Exists(absolutePath))
            return Task.FromResult<(Stream, string)?>(null);

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var contentType = ContentTypeFromExtension(normalized);
        return Task.FromResult<(Stream, string)?>((stream, contentType));
    }

    public Task DeleteAsync(int studentId, string extension, CancellationToken ct = default)
    {
        var normalized = NormalizeExtension(extension);
        if (normalized is null)
            return Task.CompletedTask;

        var absolutePath = BuildPath(studentId, normalized);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        return Task.CompletedTask;
    }

    private string BuildPath(int studentId, string extension)
        => Path.Combine(_basePath, $"{studentId}.{extension}");

    private static string? ExtensionFromContentType(string contentType) => contentType?.ToLowerInvariant() switch
    {
        JpegContentType => "jpg",
        PngContentType => "png",
        _ => null,
    };

    private static string ContentTypeFromExtension(string extension) => extension switch
    {
        "png" => PngContentType,
        _ => JpegContentType, // "jpg"
    };

    /// <summary>
    /// Stored uzantıyı whitelist'e indirger (baştaki nokta atılır, küçük harfe çevrilir).
    /// "jpeg" → "jpg". Tanınmayan değer için null.
    /// </summary>
    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        var clean = extension.TrimStart('.').ToLowerInvariant();
        return clean switch
        {
            "jpg" or "jpeg" => "jpg",
            "png" => "png",
            _ => null,
        };
    }
}
