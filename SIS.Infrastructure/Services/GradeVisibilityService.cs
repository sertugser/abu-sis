using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Harf notu ilan görünürlüğü — StudentTranscriptService içindeki özel mantıktan tek
/// kaynağa taşındı. Aktif dönemin notları "letter-grade-publication" (veya bütünleme
/// karşılığı) web modülü açıkken görünür; aksi halde gizlenir. Geçmiş dönemler her zaman görünür.
/// </summary>
public class GradeVisibilityService : IGradeVisibilityService
{
    private readonly AppDbContext _context;
    private readonly IWebModuleControlService _webModuleControls;

    public GradeVisibilityService(AppDbContext context, IWebModuleControlService webModuleControls)
    {
        _context = context;
        _webModuleControls = webModuleControls;
    }

    public async Task<Dictionary<int, bool>> BuildTermGradeVisibilityAsync(
        IEnumerable<int> termIds,
        CancellationToken ct = default)
    {
        var activeTermId = await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);

        var result = new Dictionary<int, bool>();
        foreach (var termId in termIds.Distinct())
        {
            result[termId] = termId != activeTermId
                || await CanStudentSeeLetterGradeAsync(termId, ct);
        }

        return result;
    }

    private async Task<bool> CanStudentSeeLetterGradeAsync(int termId, CancellationToken ct)
    {
        var finalAccess = await _webModuleControls.EvaluateAsync("letter-grade-publication", termId, ct);
        if (finalAccess.IsOpen)
            return true;

        var makeupAccess = await _webModuleControls.EvaluateAsync("makeup-letter-grade-publication", termId, ct);
        return makeupAccess.IsOpen;
    }
}
