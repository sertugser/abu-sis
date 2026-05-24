namespace SIS.Application.Common.Services;

/// <summary>
/// Hiçbir şey yapmayan <see cref="IStudentDocumentStorage"/> — belge depolaması
/// gerekmeyen constructor yolları ve testler için no-op fallback.
/// </summary>
public sealed class NullStudentDocumentStorage : IStudentDocumentStorage
{
    public Task<string> SaveAsync(int studentId, int documentId, Stream content, string contentType, CancellationToken ct = default)
        => Task.FromResult("pdf");

    public Task<(Stream Stream, string ContentType)?> GetAsync(int studentId, int documentId, string extension, CancellationToken ct = default)
        => Task.FromResult<(Stream, string)?>(null);

    public Task DeleteAsync(int studentId, int documentId, string extension, CancellationToken ct = default)
        => Task.CompletedTask;
}
