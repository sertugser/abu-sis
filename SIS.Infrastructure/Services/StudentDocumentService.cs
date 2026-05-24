using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class StudentDocumentService : IStudentDocumentService
{
    private readonly AppDbContext _context;

    public StudentDocumentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<StudentDocumentStatusDto>?> GetStatusAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var studentExists = await _context.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId, cancellationToken);
        if (!studentExists)
            return null;

        var catalog = await _context.DocumentTypes
            .AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ToListAsync(cancellationToken);

        var documents = await _context.StudentDocuments
            .AsNoTracking()
            .Where(d => d.StudentId == studentId)
            .ToListAsync(cancellationToken);

        // Left-join the full catalog with this student's documents.
        var byType = documents.ToDictionary(d => d.DocumentTypeId);

        return catalog
            .Select(type =>
            {
                byType.TryGetValue(type.Id, out var doc);
                return new StudentDocumentStatusDto(
                    DocumentTypeId: type.Id,
                    Code: type.Code,
                    Name: type.Name,
                    IsRequired: type.IsRequired,
                    SortOrder: type.SortOrder,
                    Uploaded: doc is not null,
                    DocumentId: doc?.Id,
                    OriginalFileName: doc?.OriginalFileName,
                    UploadedAt: doc?.UploadedAt);
            })
            .ToList();
    }

    public async Task<int> RecordUploadAsync(
        int studentId,
        int documentTypeId,
        string extension,
        string? originalFileName,
        int? uploadedByUserId,
        CancellationToken cancellationToken = default)
    {
        var typeExists = await _context.DocumentTypes
            .AnyAsync(d => d.Id == documentTypeId, cancellationToken);
        if (!typeExists)
            throw new KeyNotFoundException($"Belge tipi bulunamadı: {documentTypeId}");

        var doc = await _context.StudentDocuments
            .FirstOrDefaultAsync(
                d => d.StudentId == studentId && d.DocumentTypeId == documentTypeId,
                cancellationToken);

        var now = DateTime.UtcNow;
        if (doc is null)
        {
            doc = new StudentDocument
            {
                StudentId = studentId,
                DocumentTypeId = documentTypeId,
            };
            _context.StudentDocuments.Add(doc);
        }

        doc.FileExtension = extension;
        doc.OriginalFileName = originalFileName;
        doc.UploadedAt = now;
        doc.UploadedByUserId = uploadedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return doc.Id;
    }

    public async Task<StudentDocument?> GetDocumentAsync(
        int studentId,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StudentDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.Id == documentId && d.StudentId == studentId,
                cancellationToken);
    }

    public async Task RemoveAsync(
        int studentId,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var doc = await _context.StudentDocuments
            .FirstOrDefaultAsync(
                d => d.Id == documentId && d.StudentId == studentId,
                cancellationToken);
        if (doc is null)
            return;

        _context.StudentDocuments.Remove(doc);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
