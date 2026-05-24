using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class IssuedDocumentService : IIssuedDocumentService
{
    private readonly AppDbContext _context;

    public IssuedDocumentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> RecordAsync(
        int studentId,
        IssuedDocumentKind kind,
        int? issuedByUserId,
        CancellationToken cancellationToken = default)
    {
        var studentExists = await _context.Students
            .AnyAsync(s => s.Id == studentId, cancellationToken);
        if (!studentExists)
            throw new KeyNotFoundException("Öğrenci bulunamadı.");

        var now = DateTime.UtcNow;

        var sameKindThisYear = await _context.IssuedDocuments
            .CountAsync(d => d.Kind == kind && d.IssuedAt.Year == now.Year, cancellationToken);
        var seq = sameKindThisYear + 1;

        var doc = new IssuedDocument
        {
            StudentId = studentId,
            Kind = kind,
            IssuedAt = now,
            IssuedByUserId = issuedByUserId,
            DocumentNumber = $"{KindPrefix(kind)}-{now.Year:D4}-{seq:D6}",
        };

        _context.IssuedDocuments.Add(doc);
        await _context.SaveChangesAsync(cancellationToken);
        return doc.Id;
    }

    public async Task<List<IssuedDocumentDto>?> GetHistoryAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var studentExists = await _context.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId, cancellationToken);
        if (!studentExists)
            return null;

        var documents = await _context.IssuedDocuments
            .AsNoTracking()
            .Where(d => d.StudentId == studentId)
            .OrderByDescending(d => d.IssuedAt)
            .ToListAsync(cancellationToken);

        var userIds = documents
            .Where(d => d.IssuedByUserId.HasValue)
            .Select(d => d.IssuedByUserId!.Value)
            .Distinct()
            .ToList();

        var names = userIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Set<ApplicationUser>()
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", cancellationToken);

        return documents
            .Select(d => new IssuedDocumentDto(
                Id: d.Id,
                Kind: d.Kind.ToString(),
                KindLabel: KindLabel(d.Kind),
                IssuedAt: d.IssuedAt,
                DocumentNumber: d.DocumentNumber,
                IssuedByName: d.IssuedByUserId.HasValue
                    && names.TryGetValue(d.IssuedByUserId.Value, out var name)
                        ? name
                        : null))
            .ToList();
    }

    private static string KindPrefix(IssuedDocumentKind kind) => kind switch
    {
        IssuedDocumentKind.StudentInfoForm => "OBF",
        IssuedDocumentKind.EnrollmentCertificate => "OGB",
        _ => "BLG",
    };

    private static string KindLabel(IssuedDocumentKind kind) => kind switch
    {
        IssuedDocumentKind.StudentInfoForm => "Öğrenci Bilgi Formu",
        IssuedDocumentKind.EnrollmentCertificate => "Öğrenci Belgesi",
        _ => kind.ToString(),
    };
}
