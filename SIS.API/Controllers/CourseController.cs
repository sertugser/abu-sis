using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Time;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CourseController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IDateTimeProvider _clock;

    public CourseController(AppDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// GET api/course — list courses for the active term.
    /// Optionally filter by programId query parameter.
    /// </summary>
    [HttpGet]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetCourses([FromQuery] int? programId)
    {
        // Find active term
        var now = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms
            .FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= now && t.TermEnd >= now);

        if (activeTerm == null)
            return NotFound(new { error = "Aktif donem bulunamadi." });

        // Get sections for the active term with related data
        var sectionsQuery = _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == activeTerm.Id);

        // If programId is provided, filter courses that belong to that program's active curriculum
        HashSet<int>? programCourseIds = null;
        Dictionary<int, bool>? compulsoryMap = null;
        Dictionary<int, int>? semesterMap = null;

        if (programId.HasValue)
        {
            var curriculumCourses = await _context.CurriculumCourses
                .Include(cc => cc.Curriculum)
                .Where(cc => cc.Curriculum.ProgramId == programId.Value && cc.Curriculum.IsActive)
                .ToListAsync();

            programCourseIds = curriculumCourses.Select(cc => cc.CourseId).ToHashSet();
            compulsoryMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.IsCompulsory);
            semesterMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.SuggestedSemester);

            sectionsQuery = sectionsQuery.Where(cs => programCourseIds.Contains(cs.CourseId));
        }

        var sections = await sectionsQuery.ToListAsync();

        // We need instructor user info for full name — load users for instructors
        var instructorIds = sections
            .Where(s => s.Instructor != null)
            .Select(s => s.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNames = await _context.Users
            .Where(u => instructorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        // Load SectionSchedule records for schedule display
        var sectionIds = sections.Select(s => s.Id).ToList();
        var sectionSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => sectionIds.Contains(ss.CourseSectionId))
            .ToListAsync();
        var scheduleMap = sectionSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.FormatSchedule(g));

        // Group sections by course
        var courseGroups = sections
            .GroupBy(s => s.CourseId)
            .Select(g =>
            {
                var course = g.First().Course;
                return new CourseListDto
                {
                    CourseId = course.Id,
                    Code = course.Code,
                    Name = course.Name,
                    Ects = course.Ects,
                    TheoryHours = course.TheoryHours,
                    LabHours = course.LabHours,
                    IsCompulsory = compulsoryMap != null
                        && compulsoryMap.TryGetValue(course.Id, out var isComp) && isComp,
                    SuggestedSemester = semesterMap != null
                        && semesterMap.TryGetValue(course.Id, out var sem) ? sem : 0,
                    Sections = g.Select(s => new SectionDto
                    {
                        SectionId = s.Id,
                        InstructorName = s.Instructor != null && userNames.TryGetValue(s.Instructor.UserId, out var name)
                            ? $"{s.Instructor.Title} {name}".Trim()
                            : string.Empty,
                        Capacity = s.Capacity,
                        EnrolledCount = s.EnrolledCount,
                        AvailableSeats = Math.Max(0, s.Capacity - s.EnrolledCount),
                        ClassroomName = s.Classroom?.Name ?? string.Empty,
                        Schedule = scheduleMap.GetValueOrDefault(s.Id, string.Empty),
                        IsActive = s.IsActive
                    }).ToList()
                };
            })
            .OrderBy(c => c.Code)
            .ToList();

        return Ok(courseGroups);
    }

    /// <summary>
    /// GET api/course/student-catalog — courses for the authenticated student's active term.
    /// Each course includes: TakeStatus (IlkDefa/Alttan/OncedenAlindi), PreviousGrade, CurriculumType, PrerequisitesMet.
    /// </summary>
    [HttpGet("student-catalog")]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetStudentCatalog()
    {
        // 1. Authenticated student
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .Include(s => s.StudentPrograms)
            .FirstOrDefaultAsync(s => s.UserId == userId);
        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (activeProgram == null)
            return NotFound(new { error = "Aktif program bulunamadi." });

        // 2. Active term
        var nowCatalog = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= nowCatalog && t.TermEnd >= nowCatalog);
        if (activeTerm == null)
            return NotFound(new { error = "Aktif donem bulunamadi." });

        // 3. Curriculum data — which courses are in the student's program
        var curriculumCourses = await _context.CurriculumCourses
            .Where(cc => cc.CurriculumId == activeProgram.CurriculumId)
            .ToListAsync();

        var curriculumCourseIds = curriculumCourses.Select(cc => cc.CourseId).ToHashSet();
        var compulsoryMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.IsCompulsory);
        var semesterMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.SuggestedSemester);

        // Faz C C1: Müfredata bağlı pool slot'ları + her course'un aday olduğu slot'lar.
        // ElectiveGroupCourse üzerinden CourseId → Pool[] mapping kurulur. Bir ders
        // Karar #5 gereği birden fazla grupta olabilir → çoğul liste.
        var curriculumPools = await _context.CurriculumElectivePools
            .AsNoTracking()
            .Where(p => p.CurriculumId == activeProgram.CurriculumId)
            .Include(p => p.ElectiveGroup)
                .ThenInclude(eg => eg.Courses)
            .ToListAsync();

        var courseToPoolSlots = new Dictionary<int, List<SIS.Application.Common.DTOs.ApplicablePoolSlotDto>>();
        foreach (var pool in curriculumPools)
        {
            var slot = new SIS.Application.Common.DTOs.ApplicablePoolSlotDto
            {
                PoolId = pool.Id,
                SlotName = pool.SlotName,
                GroupCode = pool.ElectiveGroup.Code,
                GroupName = pool.ElectiveGroup.Name,
                EctsPerCourse = pool.EctsPerCourse,
            };
            foreach (var groupCourse in pool.ElectiveGroup.Courses)
            {
                if (!courseToPoolSlots.TryGetValue(groupCourse.CourseId, out var list))
                {
                    list = new List<SIS.Application.Common.DTOs.ApplicablePoolSlotDto>();
                    courseToPoolSlots[groupCourse.CourseId] = list;
                }
                list.Add(slot);
            }
        }

        // 4. Student's ALL past registrations (excluding plan drafts and dropped)
        var pastRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == student.Id
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        // Build per-course grade history
        // For each course: find the LATEST grade (most recent registration with a grade)
        var courseGradeMap = new Dictionary<int, (GradeCode grade, string gradeStr)>();
        var failedCourseIds = new HashSet<int>();

        foreach (var group in pastRegistrations.GroupBy(r => r.CourseSection.CourseId))
        {
            var latestWithGrade = group
                .Where(r => r.Grade != null && r.Status != RegistrationStatus.Withdrawn)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();

            if (latestWithGrade?.Grade == null) continue;

            var grade = latestWithGrade.Grade.Value;
            courseGradeMap[group.Key] = (grade, grade.ToString());

            if (grade == GradeCode.FF || grade == GradeCode.NA || grade == GradeCode.U)
                failedCourseIds.Add(group.Key);
        }

        // Passed courses = have a grade that is NOT FF/NA/U/W/I
        var passedCourseIds = courseGradeMap
            .Where(kv => kv.Value.grade != GradeCode.FF && kv.Value.grade != GradeCode.NA
                && kv.Value.grade != GradeCode.U && kv.Value.grade != GradeCode.W
                && kv.Value.grade != GradeCode.I)
            .Select(kv => kv.Key)
            .ToHashSet();

        // 5. Prerequisites & equivalencies
        var prereqMap = (await _context.CoursePrerequisites.ToListAsync())
            .GroupBy(p => p.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.RequiredCourseId).ToHashSet());

        var equivMap = (await _context.CourseEquivalencies.ToListAsync())
            .GroupBy(e => e.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.EquivalentCourseId).ToHashSet());

        // 6. Get ALL active term sections (filtered to curriculum + failed + passed courses)
        var relevantCourseIds = new HashSet<int>(curriculumCourseIds);
        relevantCourseIds.UnionWith(failedCourseIds);
        // Also include passed courses so student can see them for potential retake
        relevantCourseIds.UnionWith(passedCourseIds);

        // CourseAccessPolicy filtresi: öğrencinin bölüm/fakültesine açık dersler
        var studentDepartmentId = await _context.AcademicPrograms.AsNoTracking()
            .Where(p => p.Id == activeProgram.ProgramId)
            .Select(p => p.DepartmentId)
            .FirstOrDefaultAsync();

        var studentFacultyId = await _context.Departments.AsNoTracking()
            .Where(d => d.Id == studentDepartmentId)
            .Select(d => d.FacultyId)
            .FirstOrDefaultAsync();

        var accessibleCourseIds = await _context.CourseAccessPolicies
            .AsNoTracking()
            .Where(cap => cap.TermId == activeTerm.Id
                && (cap.DepartmentId == null || cap.DepartmentId == studentDepartmentId)
                && (cap.FacultyId == null || cap.FacultyId == studentFacultyId))
            .Select(cap => cap.CourseId)
            .Distinct()
            .ToListAsync();

        // DepartmentId null olan dersler (ortak dersler) her bölümden görünür
        var sharedCourseIds = await _context.Courses.AsNoTracking()
            .Where(c => c.DepartmentId == null)
            .Select(c => c.Id)
            .ToListAsync();

        // Müfredattaki dersler + erişim politikası olan dersler + ortak dersler
        var visibleCourseIds = new HashSet<int>(relevantCourseIds);
        visibleCourseIds.UnionWith(accessibleCourseIds);
        visibleCourseIds.UnionWith(sharedCourseIds);

        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == activeTerm.Id && visibleCourseIds.Contains(cs.CourseId))
            .ToListAsync();

        // Instructor names
        var instructorUserIds = sections
            .Where(s => s.Instructor != null)
            .Select(s => s.Instructor!.UserId)
            .Distinct().ToList();
        var userNames = await _context.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        // Load SectionSchedule records for schedule display
        var catalogSectionIds = sections.Select(s => s.Id).ToList();
        var catalogSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => catalogSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync();
        var catalogScheduleMap = catalogSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.FormatSchedule(g));

        // 7. Build result — group by course
        var result = sections
            .GroupBy(s => s.CourseId)
            .Select(g =>
            {
                var course = g.First().Course;
                var cid = course.Id;

                // TakeStatus: how does this student relate to this course?
                string takeStatus;
                string? previousGrade = null;

                if (failedCourseIds.Contains(cid))
                {
                    takeStatus = "Alttan"; // FF/NA/U — must retake
                    previousGrade = courseGradeMap.TryGetValue(cid, out var fg) ? fg.gradeStr : null;
                }
                else if (courseGradeMap.TryGetValue(cid, out var pg))
                {
                    takeStatus = "OncedenAlindi"; // Previously passed — can improve if desired
                    previousGrade = pg.gradeStr;
                }
                else
                {
                    takeStatus = "IlkDefa"; // Never taken
                }

                // CurriculumType: position in student's curriculum
                string curriculumType;
                if (compulsoryMap.TryGetValue(cid, out var isComp) && isComp)
                    curriculumType = "Zorunlu";
                else if (curriculumCourseIds.Contains(cid))
                    curriculumType = "Secmeli";
                else
                    curriculumType = "MufredatDisi";

                // Prerequisite check — skip if student already passed this course
                bool prereqsMet = true;
                if (!passedCourseIds.Contains(cid) && prereqMap.TryGetValue(cid, out var requiredIds))
                {
                    foreach (var reqId in requiredIds)
                    {
                        bool passed = passedCourseIds.Contains(reqId);
                        if (!passed && equivMap.TryGetValue(reqId, out var equivIds))
                            passed = equivIds.Any(eid => passedCourseIds.Contains(eid));
                        if (!passed) { prereqsMet = false; break; }
                    }
                }

                return new StudentCourseDto
                {
                    CourseId = cid,
                    Code = course.Code,
                    Name = course.Name,
                    Ects = course.Ects,
                    TheoryHours = course.TheoryHours,
                    LabHours = course.LabHours,
                    IsCompulsory = compulsoryMap.TryGetValue(cid, out var ic2) && ic2,
                    SuggestedSemester = semesterMap.TryGetValue(cid, out var sem) ? sem : 0,
                    TakeStatus = takeStatus,
                    PrerequisitesMet = prereqsMet,
                    PreviousGrade = previousGrade,
                    CurriculumType = curriculumType,
                    ApplicablePoolSlots = courseToPoolSlots.TryGetValue(cid, out var slots)
                        ? slots
                        : new List<SIS.Application.Common.DTOs.ApplicablePoolSlotDto>(),
                    Sections = g.Select(s => new SectionDto
                    {
                        SectionId = s.Id,
                        InstructorName = s.Instructor != null && userNames.TryGetValue(s.Instructor.UserId, out var name)
                            ? $"{s.Instructor.Title} {name}".Trim()
                            : string.Empty,
                        Capacity = s.Capacity,
                        EnrolledCount = s.EnrolledCount,
                        AvailableSeats = Math.Max(0, s.Capacity - s.EnrolledCount),
                        ClassroomName = s.Classroom?.Name ?? string.Empty,
                        Schedule = catalogScheduleMap.GetValueOrDefault(s.Id, string.Empty),
                        IsActive = s.IsActive
                    }).ToList()
                };
            })
            // Sort: Alttan first (must retake), then IlkDefa (new courses), then OncedenAlindi (improvement)
            .OrderBy(c => c.TakeStatus == "Alttan" ? 0 : c.TakeStatus == "IlkDefa" ? 1 : 2)
            .ThenBy(c => c.SuggestedSemester)
            .ThenBy(c => c.Code)
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// GET api/course/sections/{id} — get section detail.
    /// </summary>
    [HttpGet("sections/{id}")]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetSectionDetail(int id)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == id);

        if (section == null)
            return NotFound(new { error = "Ders sectionu bulunamadi." });

        string instructorName = string.Empty;
        if (section.Instructor != null)
        {
            var user = await _context.Users.FindAsync(section.Instructor.UserId);
            if (user != null)
                instructorName = $"{section.Instructor.Title} {user.FirstName} {user.LastName}".Trim();
        }

        var detailSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == section.Id)
            .ToListAsync();

        var dto = new SectionDto
        {
            SectionId = section.Id,
            InstructorName = instructorName,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            AvailableSeats = Math.Max(0, section.Capacity - section.EnrolledCount),
            ClassroomName = section.Classroom?.Name ?? string.Empty,
            Schedule = ScheduleHelper.FormatSchedule(detailSchedules),
            IsActive = section.IsActive
        };

        return Ok(new
        {
            Section = dto,
            Course = new
            {
                section.Course.Id,
                section.Course.Code,
                section.Course.Name,
                section.Course.Ects
            }
        });
    }

    /// <summary>
    /// GET api/course/terms/active — get the active term info.
    /// </summary>
    [HttpGet("terms/active")]
    [RequirePermission("lookup.term")]
    public async Task<IActionResult> GetActiveTerm()
    {
        var now = _clock.UtcNow;
        var term = await _context.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= now && t.TermEnd >= now);

        if (term == null)
            return NotFound(new { error = "Aktif donem bulunamadi." });

        var dto = new ActiveTermDto
        {
            Id = term.Id,
            Name = term.Name,
            Year = term.Year,
            Semester = term.TermType.ToString(),
            RegistrationStart = term.AcademicCalendar?.RegistrationStart ?? term.TermStart,
            RegistrationEnd = term.AcademicCalendar?.RegistrationEnd ?? term.TermStart,
            AddDropStart = term.AcademicCalendar?.AddDropStart ?? term.TermStart,
            AddDropEnd = term.AcademicCalendar?.AddDropEnd ?? term.TermStart,
            IsRegistrationOpen = term.IsRegistrationOpen(_clock.UtcNow),
            IsAddDropOpen = term.IsAddDropOpen(_clock.UtcNow),
            Status = term.GetStatus(_clock.UtcNow).ToString(),
            HasAcademicCalendar = term.AcademicCalendar != null
        };

        return Ok(dto);
    }

}
