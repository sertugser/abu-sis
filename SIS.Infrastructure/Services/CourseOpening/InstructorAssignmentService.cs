using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CourseOpening;

/// <summary>
/// Toplu öğretim elemanı atama. UpdateSectionAsync'i her section için arka arkaya çağırır.
/// Faz 3.C: Eski CourseOpeningService'ten ayrıldı.
/// </summary>
public class InstructorAssignmentService(
    ICourseSectionLifecycleService lifecycleService,
    AppDbContext context) : IInstructorAssignmentService
{
    private readonly ICourseSectionLifecycleService _lifecycleService = lifecycleService;
    private readonly AppDbContext _context = context;

    public async Task<BulkAssignInstructorResultDto> BulkAssignInstructorAsync(
        int departmentId, int termId, BulkAssignInstructorRequest request, CancellationToken ct = default)
    {
        var result = new BulkAssignInstructorResultDto();

        foreach (var assignment in request.Assignments)
        {
            try
            {
                await _lifecycleService.UpdateSectionAsync(new UpdateOpenedSectionRequest
                {
                    SectionId = assignment.SectionId,
                    InstructorId = assignment.InstructorId,
                }, ct);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Şube #{assignment.SectionId}: {ex.Message}");
            }
        }

        return result;
    }

    public async Task<CopyInstructorAssignmentsResultDto> CopyInstructorAssignmentsFromTermAsync(
        CopyInstructorAssignmentsRequest request, CancellationToken ct = default)
    {
        var result = new CopyInstructorAssignmentsResultDto();

        if (request.SourceTermId == request.TargetTermId)
            throw new InvalidOperationException("Kaynak dönem hedef dönemle aynı olamaz.");

        // Kaynak dönem: bu bölümün aktif + hocası atanmış şubeleri.
        // Ders bazında, şube sırasına (Id) göre hoca listesi çıkar.
        var sourceInstructorsByCourse = (await _context.CourseSections
                .AsNoTracking()
                .Where(cs => cs.TermId == request.SourceTermId
                    && cs.Status == CourseSectionStatus.Active
                    && cs.Course.DepartmentId == request.DepartmentId
                    && cs.InstructorId != null)
                .OrderBy(cs => cs.CourseId).ThenBy(cs => cs.Id)
                .Select(cs => new { cs.CourseId, InstructorId = cs.InstructorId!.Value })
                .ToListAsync(ct))
            .GroupBy(x => x.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.InstructorId).ToList());

        if (sourceInstructorsByCourse.Count == 0)
            return result; // Kaynak dönemde atanmış hoca yok — hiçbir şey kopyalanmaz.

        // Hedef dönem: bu bölümün aktif + hocası BOŞ şubeleri (yalnızca bunlar doldurulur).
        var targetSections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == request.TargetTermId
                && cs.Status == CourseSectionStatus.Active
                && cs.Course.DepartmentId == request.DepartmentId
                && cs.InstructorId == null)
            .OrderBy(cs => cs.CourseId).ThenBy(cs => cs.Id)
            .ToListAsync(ct);

        foreach (var courseGroup in targetSections.GroupBy(cs => cs.CourseId))
        {
            if (!sourceInstructorsByCourse.TryGetValue(courseGroup.Key, out var sourceInstructors))
            {
                // Kaynak dönemde bu ders için atanmış hoca yok.
                result.SkippedNoSourceCount += courseGroup.Count();
                continue;
            }

            var ordered = courseGroup.OrderBy(cs => cs.Id).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                if (i >= sourceInstructors.Count)
                {
                    // Hedefte kaynaktan daha fazla boş şube var — fazlalar atlanır.
                    result.SkippedNoSourceCount++;
                    continue;
                }

                var section = ordered[i];
                try
                {
                    await _lifecycleService.UpdateSectionAsync(new UpdateOpenedSectionRequest
                    {
                        SectionId = section.Id,
                        InstructorId = sourceInstructors[i],
                    }, ct);
                    result.AssignedCount++;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"{section.Course.Code} (Şube #{section.Id}): {ex.Message}");
                }
            }
        }

        return result;
    }
}
