namespace SIS.Application.Common.Services;

/// <summary>
/// Hiçbir şey yapmayan <see cref="IStudentPhotoStorage"/> — fotoğraf depolaması
/// gerekmeyen constructor yolları ve testler için no-op fallback.
/// </summary>
public sealed class NullStudentPhotoStorage : IStudentPhotoStorage
{
    public Task<string> SaveAsync(int studentId, Stream content, string contentType, CancellationToken ct = default)
        => Task.FromResult("jpg");

    public Task<(Stream Stream, string ContentType)?> GetAsync(int studentId, string extension, CancellationToken ct = default)
        => Task.FromResult<(Stream, string)?>(null);

    public Task DeleteAsync(int studentId, string extension, CancellationToken ct = default)
        => Task.CompletedTask;
}
