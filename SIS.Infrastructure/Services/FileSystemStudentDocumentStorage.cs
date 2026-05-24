using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SIS.Application.Common.Services;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Öğrenci belgelerini yerel dosya sistemine yazan
/// <see cref="IStudentDocumentStorage"/> implementasyonu.
///
/// Güvenlik: Dosya adı tamamen SUNUCU tarafından üretilir
/// (<c>{studentId}_{documentId}.{ext}</c>) — istemci kaynaklı hiçbir dosya adı
/// kullanılmaz, dolayısıyla path traversal mümkün değildir. <c>studentId</c> ve
/// <c>documentId</c> int olduğundan path'e güvenle enterpole edilir. Uzantı yalnızca
/// whitelist'ten ("pdf"/"jpg"/"png") türetilir. Okuma/silmede stored uzantı tekrar
/// whitelist'e indirgenir ki bozuk bir DB değeri path traversal'a dönüşemesin.
/// </summary>
public sealed class FileSystemStudentDocumentStorage : IStudentDocumentStorage
{
    private const string PdfContentType = "application/pdf";
    private const string JpegContentType = "image/jpeg";
    private const string PngContentType = "image/png";

    private readonly string _basePath;

    public FileSystemStudentDocumentStorage(IConfiguration configuration, IHostEnvironment env)
    {
        var configured = configuration["StudentDocuments:BasePath"];
        _basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "student-documents")
            : (Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(env.ContentRootPath, configured));

        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(int studentId, int documentId, Stream content, string contentType, CancellationToken ct = default)
    {
        var extension = ExtensionFromContentType(contentType)
            ?? throw new InvalidOperationException($"Desteklenmeyen içerik tipi: {contentType}");

        var absolutePath = BuildPath(studentId, documentId, extension);

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

    public Task<(Stream Stream, string ContentType)?> GetAsync(int studentId, int documentId, string extension, CancellationToken ct = default)
    {
        var normalized = NormalizeExtension(extension);
        if (normalized is null)
            return Task.FromResult<(Stream, string)?>(null);

        var absolutePath = BuildPath(studentId, documentId, normalized);
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

    public Task DeleteAsync(int studentId, int documentId, string extension, CancellationToken ct = default)
    {
        var normalized = NormalizeExtension(extension);
        if (normalized is null)
            return Task.CompletedTask;

        var absolutePath = BuildPath(studentId, documentId, normalized);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        return Task.CompletedTask;
    }

    private string BuildPath(int studentId, int documentId, string extension)
        => Path.Combine(_basePath, $"{studentId}_{documentId}.{extension}");

    private static string? ExtensionFromContentType(string contentType) => contentType?.ToLowerInvariant() switch
    {
        PdfContentType => "pdf",
        JpegContentType => "jpg",
        PngContentType => "png",
        _ => null,
    };

    private static string ContentTypeFromExtension(string extension) => extension switch
    {
        "pdf" => PdfContentType,
        "png" => PngContentType,
        _ => JpegContentType, // "jpg"
    };

    /// <summary>
    /// Stored uzantıyı whitelist'e indirger (baştaki nokta atılır, küçük harfe çevrilir).
    /// "jpeg" → "jpg". Tanınmayan değer için null (path traversal koruması).
    /// </summary>
    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        var clean = extension.TrimStart('.').ToLowerInvariant();
        return clean switch
        {
            "pdf" => "pdf",
            "jpg" or "jpeg" => "jpg",
            "png" => "png",
            _ => null,
        };
    }
}
