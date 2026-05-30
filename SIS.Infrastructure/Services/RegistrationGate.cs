using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public sealed class RegistrationGate : IRegistrationGate
{
    private readonly AppDbContext _context;
    public RegistrationGate(AppDbContext context) => _context = context;

    public async Task<RegistrationGateResult> EvaluateSectionAsync(CourseSection section, CancellationToken ct = default)
    {
        var results = await EvaluateSectionsAsync(new[] { section }, ct);
        return results[section.Id];
    }

    public async Task<IReadOnlyDictionary<int, RegistrationGateResult>> EvaluateSectionsAsync(
        IReadOnlyCollection<CourseSection> sections, CancellationToken ct = default)
    {
        // Bölüm→fakülte haritası + onaylı (fakülte, dönem) kümesi tek toplu sorguyla (N+1 önlenir).
        var deptIds = sections
            .Where(s => s.Course?.DepartmentId != null)
            .Select(s => s.Course!.DepartmentId!.Value)
            .Distinct()
            .ToList();

        var deptFaculty = deptIds.Count == 0
            ? new Dictionary<int, int>()
            : await _context.Departments
                .Where(d => deptIds.Contains(d.Id))
                .Select(d => new { d.Id, d.FacultyId })
                .ToDictionaryAsync(x => x.Id, x => x.FacultyId, ct);

        var facultyIds = deptFaculty.Values.Distinct().ToList();
        var termIds = sections.Select(s => s.TermId).Distinct().ToList();
        var approvedPairs = facultyIds.Count == 0
            ? new HashSet<(int FacultyId, int TermId)>()
            : (await _context.FacultyScheduleApprovals
                .Where(a => facultyIds.Contains(a.FacultyId) && termIds.Contains(a.TermId))
                .Select(a => new { a.FacultyId, a.TermId })
                .ToListAsync(ct))
                .Select(a => (a.FacultyId, a.TermId))
                .ToHashSet();

        var result = new Dictionary<int, RegistrationGateResult>(sections.Count);
        foreach (var section in sections)
        {
            result[section.Id] = Evaluate(section, deptFaculty, approvedPairs);
        }
        return result;
    }

    // Kural tek kaynak: Active mi + suspend değil mi + (bölüme bağlıysa) fakültesi onaylı mı.
    private static RegistrationGateResult Evaluate(
        CourseSection section,
        IReadOnlyDictionary<int, int> deptFaculty,
        IReadOnlySet<(int FacultyId, int TermId)> approvedPairs)
    {
        if (section.Status != CourseSectionStatus.Active)
            return new(false, "SectionNotActive", "Bu ders şubesi henüz seçime açık değil.");

        if (section.IsEnrollmentSuspended)
            return new(false, "EnrollmentSuspended", "Bu ders şubesi şu anda düzenlemede — geçici olarak yeni kayda kapalı.");

        if (section.Course?.DepartmentId is int deptId)
        {
            var facultyId = deptFaculty.GetValueOrDefault(deptId);
            if (!approvedPairs.Contains((facultyId, section.TermId)))
                return new(false, "FacultyScheduleNotApproved", "Bu dersin programı henüz yayınlanmadı.");
        }

        return RegistrationGateResult.Open;
    }
}
