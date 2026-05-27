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
        if (section.Status != CourseSectionStatus.Active)
            return new(false, "SectionNotActive", "Bu ders şubesi henüz seçime açık değil.");

        if (section.IsEnrollmentSuspended)
            return new(false, "EnrollmentSuspended", "Bu ders şubesi şu anda düzenlemede — geçici olarak yeni kayda kapalı.");

        if (section.Course.DepartmentId.HasValue)
        {
            var facultyId = await _context.Departments
                .Where(d => d.Id == section.Course.DepartmentId.Value)
                .Select(d => d.FacultyId)
                .FirstOrDefaultAsync(ct);
            var approved = await _context.FacultyScheduleApprovals
                .AnyAsync(a => a.FacultyId == facultyId && a.TermId == section.TermId, ct);
            if (!approved)
                return new(false, "FacultyScheduleNotApproved", "Bu dersin programı henüz yayınlanmadı.");
        }

        return RegistrationGateResult.Open;
    }
}
