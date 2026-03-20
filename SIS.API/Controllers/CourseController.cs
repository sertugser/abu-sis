using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CourseController : ControllerBase
{
    private readonly AppDbContext _context;

    public CourseController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET api/course — list courses for the active term.
    /// Optionally filter by programId query parameter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCourses([FromQuery] int? programId)
    {
        // Find active term
        var activeTerm = await _context.AcademicTerms
            .FirstOrDefaultAsync(t => t.IsActive);

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
                        Schedule = FormatSchedule(s.ScheduleJson),
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
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.IsActive);
        if (activeTerm == null)
            return NotFound(new { error = "Aktif donem bulunamadi." });

        // 3. Curriculum data — which courses are in the student's program
        var curriculumCourses = await _context.CurriculumCourses
            .Where(cc => cc.CurriculumId == activeProgram.CurriculumId)
            .ToListAsync();

        var curriculumCourseIds = curriculumCourses.Select(cc => cc.CourseId).ToHashSet();
        var compulsoryMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.IsCompulsory);
        var semesterMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.SuggestedSemester);

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

        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == activeTerm.Id && relevantCourseIds.Contains(cs.CourseId))
            .ToListAsync();

        // Instructor names
        var instructorUserIds = sections
            .Where(s => s.Instructor != null)
            .Select(s => s.Instructor!.UserId)
            .Distinct().ToList();
        var userNames = await _context.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

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
                        Schedule = FormatSchedule(s.ScheduleJson),
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

        var dto = new SectionDto
        {
            SectionId = section.Id,
            InstructorName = instructorName,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            AvailableSeats = Math.Max(0, section.Capacity - section.EnrolledCount),
            ClassroomName = section.Classroom?.Name ?? string.Empty,
            Schedule = FormatSchedule(section.ScheduleJson),
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
    public async Task<IActionResult> GetActiveTerm()
    {
        var term = await _context.AcademicTerms
            .FirstOrDefaultAsync(t => t.IsActive);

        if (term == null)
            return NotFound(new { error = "Aktif donem bulunamadi." });

        var now = DateTime.UtcNow;

        var dto = new ActiveTermDto
        {
            Id = term.Id,
            Name = term.Name,
            Year = term.Year,
            Semester = term.Semester.ToString(),
            RegistrationStart = term.RegistrationStart,
            RegistrationEnd = term.RegistrationEnd,
            AddDropStart = term.AddDropStart,
            AddDropEnd = term.AddDropEnd,
            IsRegistrationOpen = now >= term.RegistrationStart && now <= term.RegistrationEnd,
            IsAddDropOpen = now >= term.AddDropStart && now <= term.AddDropEnd
        };

        return Ok(dto);
    }

    /// <summary>
    /// Converts ScheduleJson (list of ScheduleSlot) to a human-readable Turkish string.
    /// Example: "Pazartesi 09:00-11:00, Carsamba 13:00-15:00"
    /// </summary>
    private static string FormatSchedule(string scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson) || scheduleJson == "[]")
            return string.Empty;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var slots = JsonSerializer.Deserialize<List<ScheduleSlot>>(scheduleJson, options);

            if (slots == null || slots.Count == 0)
                return string.Empty;

            var formatted = slots
                .OrderBy(s => s.Day)
                .ThenBy(s => s.StartTime)
                .Select(s => $"{DayOfWeekToTurkish(s.Day)} {s.StartTime:HH:mm}-{s.EndTime:HH:mm}");

            return string.Join(", ", formatted);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string DayOfWeekToTurkish(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Sali",
        DayOfWeek.Wednesday => "Carsamba",
        DayOfWeek.Thursday => "Persembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => day.ToString()
    };
}
