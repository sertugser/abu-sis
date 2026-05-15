using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services.Scheduling.Rules;

/// <summary>
/// Plan 11 Faz 2.5 — V1 <c>TermSnapshot</c> + <c>SectionSnapshot</c> yapılarını yeni
/// <c>RuntimeSnapshot</c> + <c>RuntimeCandidate</c> tiplerine çevirir. SchedulingService
/// hâlâ V1 snapshot inşa ediyor; RuleEngine'a delegasyon yapacak Faz 4 task'ları bu
/// adapter'ı kullanır. Faz 5'te V1 TermSnapshot silinince <c>RuntimeSnapshotFactory</c>
/// DB'den doğrudan RuntimeSnapshot inşa edip bu adapter silinecek.
/// </summary>
public static class RuntimeSnapshotAdapter
{
    public static RuntimeCandidate FromSectionSnapshot(SectionSnapshot s)
    {
        var blocks = s.TimeBlocks.Select(b =>
        {
            // SectionSnapshot.ClassroomType paralel-split'te primary classroom tipini yansıtır;
            // per-block tip için fallback olarak kullanılır. ClassroomCapacity de primary window
            // combined kapasitesidir — block-level oda yoksa snapshot.ClassroomCapacity kullanılır.
            return new RuntimeBlock(
                Day: (int)b.Day,
                StartTime: b.StartTime,
                EndTime: b.EndTime,
                ClassroomId: b.ClassroomId,
                ClassroomName: s.ClassroomName,
                ClassroomType: s.ClassroomType,
                ClassroomCapacity: s.ClassroomCapacity);
        }).ToList();

        return new RuntimeCandidate
        {
            SectionId = s.SectionId,
            SectionNumber = s.SectionNumber,
            CourseId = s.CourseId,
            CourseCode = s.CourseCode,
            CourseName = s.CourseName,
            DepartmentId = s.DepartmentId,
            InstructorId = s.InstructorId,
            InstructorName = s.InstructorName,
            InstructorMaxWeeklyHours = s.InstructorMaxWeeklyHours,
            Capacity = s.Capacity,
            TheoryHours = s.TheoryHours,
            LabHours = s.LabHours,
            TeachingMethod = s.TeachingMethod,
            RequiredTheoryClassroomType = (int?)s.TheoryClassroomType,
            RequiredLabClassroomType = (int?)s.LabClassroomType,
            SuggestedSemester = s.SuggestedSemester,
            SuggestedSemestersByDepartment = s.SuggestedSemestersByDepartment,
            AccessibleDepartmentIds = s.AccessibleDepartmentIds,
            Blocks = blocks,
        };
    }

    public static RuntimeSnapshot FromTermSnapshot(TermSnapshot t, string curriculumConflictMode = "curriculum")
    {
        var sections = t.Sections.Select(FromSectionSnapshot).ToList();

        var classroomAssignments = t.ClassroomAssignments
            .Select(cta => new ClassroomTermAssignmentInfo(cta.ClassroomId, cta.FacultyId))
            .ToList();

        var corequisites = t.Corequisites
            .Select(cp => new CorequisitePair(cp.CourseId, cp.RequiredCourseId))
            .ToList();

        // Slot allowed modes: (DayOfWeek, TimeOnly) -> int gün dönüşümü.
        var slotAllowedModes = t.SlotAllowedModes
            .ToDictionary(
                kv => ((int)kv.Key.Item1, kv.Key.Item2),
                kv => kv.Value);

        // CohortConflictPairs ve EnrollmentConflicts ConflictPairsBuilder tarafından doldurulur.
        var cohortPairs = t.CohortConflictPairs != null
            ? new HashSet<(int, int)>(t.CohortConflictPairs)
            : new HashSet<(int, int)>();

        Application.Scheduling.Rules.EnrollmentConflictMatrix? enrollmentMatrix = null;
        if (t.EnrollmentConflicts != null)
        {
            enrollmentMatrix = new Application.Scheduling.Rules.EnrollmentConflictMatrix
            {
                HardConflictPairs = t.EnrollmentConflicts.HardConflictPairs,
            };
        }

        return new RuntimeSnapshot
        {
            TermId = t.TermId,
            TermName = t.TermName,
            ScopeName = t.ScopeName,
            Sections = sections,
            ClassroomAssignments = classroomAssignments,
            DeptFacultyMap = t.DeptFacultyMap,
            Corequisites = corequisites,
            SlotAllowedModes = slotAllowedModes,
            CohortConflictPairs = cohortPairs,
            CurriculumConflictMode = curriculumConflictMode,
            EnrollmentConflicts = enrollmentMatrix,
        };
    }
}
