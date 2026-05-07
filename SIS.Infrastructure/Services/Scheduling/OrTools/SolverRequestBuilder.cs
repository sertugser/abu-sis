using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling.OrTools;

/// <summary>
/// EF Core AppDbContext'ten Domain entity'lerini okuyup CP-SAT için <see cref="SolverRequest"/>
/// DTO'sunu inşa eder. <see cref="CpSatSolverEngine"/> bu DTO ile beslenir; AutoScheduleEngine
/// (Plan 2 Task 19) ve Run 303 integration test (Task 20) bu builder'ı kullanır.
///
/// Plan 2 scope:
/// <list type="bullet">
///   <item>Sections: Active, !IsFinalized, course faculty'nin department'larında, opsiyonel dept filter.</item>
///   <item>Classrooms: ClassroomTermAssignments üzerinden TermId + FacultyId filter'ı.</item>
///   <item>TimeSlots: term-spesifik grid, day+slotOrder ordering ile deterministic.</item>
///   <item>ConstraintFlags: tüm ConstraintDefinitions Code → IsEnabled dictionary.</item>
///   <item>Corequisites: boş — Domain'de CourseRelationship entity yok (Plan 3'te entegrasyon).</item>
///   <item>SectionInput.ProgramId / Year: null — Plan 3'te curriculum entegrasyonu.</item>
/// </list>
/// </summary>
public sealed class SolverRequestBuilder
{
    private readonly AppDbContext _context;

    public SolverRequestBuilder(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SolverRequest> BuildAsync(
        int termId,
        int facultyId,
        int? departmentId,
        int? seed,
        CancellationToken cancellationToken)
    {
        // 1) Faculty'ye bağlı department ID'leri (downstream filter için)
        var departmentIds = await _context.Departments
            .AsNoTracking()
            .Where(d => d.FacultyId == facultyId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        // 2) Sections — Active + !IsFinalized + course faculty department'larında
        var sectionsQuery = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == termId
                         && !cs.IsFinalized
                         && cs.Status == CourseSectionStatus.Active
                         && cs.Course.DepartmentId.HasValue
                         && departmentIds.Contains(cs.Course.DepartmentId.Value));

        if (departmentId.HasValue)
        {
            sectionsQuery = sectionsQuery.Where(cs => cs.Course.DepartmentId == departmentId.Value);
        }

        var sectionEntities = await sectionsQuery.ToListAsync(cancellationToken);
        var sections = sectionEntities.Select(MapSection).ToList();

        // 3) Classrooms — faculty'nin term-assigned derslikleri
        var classroomEntities = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == termId && cta.FacultyId == facultyId)
            .Select(cta => cta.Classroom)
            .ToListAsync(cancellationToken);

        var classrooms = classroomEntities
            .Where(c => c != null)
            .Select(MapClassroom)
            .ToList();

        // 4) TimeSlots — term grid (day + slot ordering deterministic indexing için)
        var timeSlotEntities = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);
        var timeSlots = timeSlotEntities.Select(MapTimeSlot).ToList();

        // 5) Constraint flags — Code → IsEnabled
        var constraintFlags = await _context.ConstraintDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(cd => cd.Code, cd => cd.IsEnabled, cancellationToken);

        // 6) Corequisites — Plan 2'de boş (Domain'de CourseRelationship entity yok).
        //    Plan 3 H11 entegrasyonu eklendiğinde buradan beslenecek.
        var corequisites = new List<CourseRelationship>();

        return new SolverRequest
        {
            TermId = termId,
            FacultyId = facultyId,
            DepartmentId = departmentId,
            Seed = seed,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = timeSlots,
            ConstraintFlags = constraintFlags,
            Corequisites = corequisites
        };
    }

    private static SectionInput MapSection(CourseSection cs) => new()
    {
        Id = cs.Id,
        CourseId = cs.CourseId,
        CourseCode = cs.Course.Code,
        CourseName = cs.Course.Name,
        SectionNumber = cs.SectionNumber,
        Capacity = cs.Capacity,
        TheoryHours = cs.Course.TheoryHours,
        LabHours = cs.Course.LabHours,
        InstructorId = cs.InstructorId,
        DepartmentId = cs.Course.DepartmentId,
        TeachingMethod = cs.Course.TeachingMethod,
        IsPinned = cs.IsPinned,
        IsScheduleLocked = cs.IsScheduleLocked
        // ProgramId, Year — Plan 2'de null (Plan 3 curriculum entegrasyonu)
        // RequiredTheoryClassroomType / RequiredLabClassroomType — Plan 2'de null
        //   (Course üzerinde TheoryClassroomType/LabClassroomType var; bunlar Type enum
        //    int değerine ihtiyaç duyduğumuzda Plan 3'te map edilir).
    };

    private static ClassroomInput MapClassroom(Classroom c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Capacity = c.Capacity,
        Type = c.Type
    };

    private static TimeSlotInput MapTimeSlot(TimeSlot ts) => new()
    {
        Id = ts.Id,
        DayOfWeek = ts.DayOfWeek,
        SlotOrder = ts.SlotOrder,
        StartTime = ts.StartTime,
        EndTime = ts.EndTime,
        AllowedMode = ts.AllowedMode
    };
}
