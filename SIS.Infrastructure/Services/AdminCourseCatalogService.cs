using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminCourseCatalogService(AppDbContext db) : IAdminCourseCatalogService
{
    public async Task<List<CourseListItemDto>> GetCoursesAsync(CourseFilterDto filter)
    {
        var query = db.Courses
            .Include(c => c.Department)
            .AsQueryable();

        if (filter.DepartmentId.HasValue)
        {
            var deptId = filter.DepartmentId.Value;
            // Müfredat bazlı filtreleme: sahip bölüm VEYA o bölümün müfredatında olan dersler
            var curriculumCourseIds = db.CurriculumCourses
                .Where(cc => cc.Curriculum.Program.DepartmentId == deptId)
                .Select(cc => cc.CourseId);

            query = query.Where(c => c.DepartmentId == deptId || curriculumCourseIds.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.Code.ToLower().Contains(s) ||
                c.Name.ToLower().Contains(s));
        }

        return await query
            .OrderBy(c => c.Code)
            .Select(c => new CourseListItemDto(
                c.Id, c.Code, c.Name, c.EnglishName,
                c.Ects, c.TheoryHours, c.LabHours,
                c.IsNonGpa, c.ExamType,
                c.DepartmentId, c.Department != null ? c.Department.Name : null,
                c.Prerequisites.Count, c.Equivalencies.Count))
            .ToListAsync();
    }

    public async Task<CourseDetailDto?> GetCourseByIdAsync(int id)
    {
        var course = await db.Courses
            .Include(c => c.Department)
            .Include(c => c.Prerequisites).ThenInclude(p => p.RequiredCourse)
            .Include(c => c.Equivalencies).ThenInclude(e => e.EquivalentCourse)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null) return null;

        var curriculumAssignments = await db.CurriculumCourses
            .Include(cc => cc.Curriculum).ThenInclude(cu => cu.Program)
            .Where(cc => cc.CourseId == id)
            .Select(cc => new CurriculumAssignmentDto(
                cc.CurriculumId,
                cc.Curriculum.Program.Name,
                cc.Curriculum.Version,
                cc.Curriculum.IsActive,
                cc.SuggestedSemester,
                cc.IsCompulsory))
            .ToListAsync();

        // Önceki dönem istatistikleri
        var stats = await BuildCourseStatsAsync(id);

        // Önkoşul zinciri (3 seviye derinlik)
        var chain = await BuildPrerequisiteChainAsync(id, maxDepth: 3);

        return new CourseDetailDto(
            course.Id, course.Code, course.Name, course.EnglishName,
            course.Ects, course.TheoryHours, course.LabHours,
            course.IsNonGpa, course.ExamType,
            course.DepartmentId, course.Department?.Name,
            course.Prerequisites.Select(p => new PrerequisiteDto(
                p.RequiredCourse.Id, p.RequiredCourse.Code, p.RequiredCourse.Name)).ToList(),
            course.Equivalencies.Select(e => new EquivalencyDto(
                e.EquivalentCourse.Id, e.EquivalentCourse.Code, e.EquivalentCourse.Name)).ToList(),
            curriculumAssignments,
            stats,
            chain
        );
    }

    private async Task<CourseStatsDto?> BuildCourseStatsAsync(int courseId)
    {
        // Toplam kaç dönem açılmış
        var totalTimesOpened = await db.CourseSections
            .Where(cs => cs.CourseId == courseId && cs.IsActive)
            .Select(cs => cs.TermId)
            .Distinct()
            .CountAsync();

        if (totalTimesOpened == 0) return null;

        // En son dönem
        var lastTermId = await db.CourseSections
            .Where(cs => cs.CourseId == courseId && cs.IsActive)
            .OrderByDescending(cs => cs.TermId)
            .Select(cs => cs.TermId)
            .FirstOrDefaultAsync();

        if (lastTermId == 0) return null;

        var lastTerm = await db.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == lastTermId);

        var lastTermSections = await db.CourseSections
            .Where(cs => cs.CourseId == courseId && cs.TermId == lastTermId && cs.IsActive)
            .ToListAsync();

        return new CourseStatsDto(
            LastTermName: lastTerm?.Name,
            LastTermSectionCount: lastTermSections.Count,
            LastTermTotalEnrolled: lastTermSections.Sum(s => s.EnrolledCount),
            LastTermAvgCapacity: lastTermSections.Count > 0
                ? (int)lastTermSections.Average(s => s.Capacity) : 0,
            TotalTimesOpened: totalTimesOpened
        );
    }

    private async Task<List<PrerequisiteChainNodeDto>> BuildPrerequisiteChainAsync(int courseId, int maxDepth)
    {
        if (maxDepth <= 0) return [];

        var prereqs = await db.CoursePrerequisites
            .Include(p => p.RequiredCourse)
            .Where(p => p.CourseId == courseId)
            .ToListAsync();

        var result = new List<PrerequisiteChainNodeDto>();
        foreach (var p in prereqs)
        {
            var children = await BuildPrerequisiteChainAsync(p.RequiredCourseId, maxDepth - 1);
            result.Add(new PrerequisiteChainNodeDto(
                p.RequiredCourse.Id, p.RequiredCourse.Code, p.RequiredCourse.Name, children));
        }
        return result;
    }

    public async Task<CourseDetailDto> CreateCourseAsync(CreateCourseDto dto)
    {
        if (await db.Courses.AnyAsync(c => c.Code == dto.Code))
            throw new InvalidOperationException($"'{dto.Code}' kodlu ders zaten mevcut.");
        if (dto.DepartmentId.HasValue && !await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId.Value))
            throw new KeyNotFoundException("Bölüm bulunamadı.");

        var course = new Course
        {
            Code = dto.Code,
            Name = dto.Name,
            EnglishName = dto.EnglishName,
            Ects = dto.Ects,
            TheoryHours = dto.TheoryHours,
            LabHours = dto.LabHours,
            IsNonGpa = dto.IsNonGpa,
            ExamType = dto.ExamType,
            DepartmentId = dto.DepartmentId
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return (await GetCourseByIdAsync(course.Id))!;
    }

    public async Task<CourseDetailDto> UpdateCourseAsync(int id, UpdateCourseDto dto)
    {
        var course = await db.Courses.FindAsync(id)
            ?? throw new KeyNotFoundException("Ders bulunamadı.");
        if (await db.Courses.AnyAsync(c => c.Code == dto.Code && c.Id != id))
            throw new InvalidOperationException($"'{dto.Code}' kodu başka bir ders tarafından kullanılıyor.");
        if (dto.DepartmentId.HasValue && !await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId.Value))
            throw new KeyNotFoundException("Bölüm bulunamadı.");

        course.Code = dto.Code;
        course.Name = dto.Name;
        course.EnglishName = dto.EnglishName;
        course.Ects = dto.Ects;
        course.TheoryHours = dto.TheoryHours;
        course.LabHours = dto.LabHours;
        course.IsNonGpa = dto.IsNonGpa;
        course.ExamType = dto.ExamType;
        course.DepartmentId = dto.DepartmentId;
        await db.SaveChangesAsync();
        return (await GetCourseByIdAsync(id))!;
    }

    public async Task DeleteCourseAsync(int id)
    {
        var inUse = await db.CourseSections.AnyAsync(s => s.CourseId == id)
            || await db.CourseRegistrations.AnyAsync(r => r.CourseSection.CourseId == id);
        if (inUse)
            throw new InvalidOperationException("Bu ders aktif şubelerde veya kayıtlarda kullanılıyor. Silinemez.");

        var course = await db.Courses.FindAsync(id)
            ?? throw new KeyNotFoundException("Ders bulunamadı.");
        db.Courses.Remove(course);
        await db.SaveChangesAsync();
    }

    public async Task AddPrerequisiteAsync(int courseId, AddPrerequisiteDto dto)
    {
        if (courseId == dto.RequiredCourseId)
            throw new InvalidOperationException("Bir ders kendisinin önkoşulu olamaz.");
        if (!await db.Courses.AnyAsync(c => c.Id == courseId))
            throw new KeyNotFoundException("Ders bulunamadı.");
        if (!await db.Courses.AnyAsync(c => c.Id == dto.RequiredCourseId))
            throw new KeyNotFoundException("Önkoşul ders bulunamadı.");
        if (await db.CoursePrerequisites.AnyAsync(p => p.CourseId == courseId && p.RequiredCourseId == dto.RequiredCourseId))
            throw new InvalidOperationException("Bu önkoşul zaten tanımlı.");

        // Döngü kontrolü: A→B→C→A engeli
        if (await WouldCreatePrerequisiteCycleAsync(courseId, dto.RequiredCourseId))
            throw new InvalidOperationException("Bu önkoşul ilişkisi döngü oluşturacak.");

        db.CoursePrerequisites.Add(new CoursePrerequisite { CourseId = courseId, RequiredCourseId = dto.RequiredCourseId });
        await db.SaveChangesAsync();
    }

    private async Task<bool> WouldCreatePrerequisiteCycleAsync(int courseId, int newRequiredCourseId)
    {
        // BFS: newRequiredCourseId'den geriye doğru ilerle, courseId'ye ulaşılıyor mu?
        var allPrereqs = await db.CoursePrerequisites.ToListAsync();
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(newRequiredCourseId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == courseId) return true; // Döngü tespit edildi

            if (!visited.Add(current)) continue;

            foreach (var prereq in allPrereqs.Where(p => p.CourseId == current))
                queue.Enqueue(prereq.RequiredCourseId);
        }

        return false;
    }

    public async Task RemovePrerequisiteAsync(int courseId, int requiredCourseId)
    {
        var prereq = await db.CoursePrerequisites
            .FirstOrDefaultAsync(p => p.CourseId == courseId && p.RequiredCourseId == requiredCourseId)
            ?? throw new KeyNotFoundException("Önkoşul bulunamadı.");
        db.CoursePrerequisites.Remove(prereq);
        await db.SaveChangesAsync();
    }

    public async Task AddEquivalencyAsync(int courseId, AddEquivalencyDto dto)
    {
        if (courseId == dto.EquivalentCourseId)
            throw new InvalidOperationException("Bir ders kendisinin eşdeğeri olamaz.");
        if (!await db.Courses.AnyAsync(c => c.Id == courseId))
            throw new KeyNotFoundException("Ders bulunamadı.");
        if (!await db.Courses.AnyAsync(c => c.Id == dto.EquivalentCourseId))
            throw new KeyNotFoundException("Eşdeğer ders bulunamadı.");
        if (await db.CourseEquivalencies.AnyAsync(e =>
            (e.CourseId == courseId && e.EquivalentCourseId == dto.EquivalentCourseId) ||
            (e.CourseId == dto.EquivalentCourseId && e.EquivalentCourseId == courseId)))
            throw new InvalidOperationException("Bu eşdeğerlik zaten tanımlı.");

        // Simetrik kayıt: A↔B her iki yönde de oluştur
        db.CourseEquivalencies.Add(new CourseEquivalency { CourseId = courseId, EquivalentCourseId = dto.EquivalentCourseId });
        db.CourseEquivalencies.Add(new CourseEquivalency { CourseId = dto.EquivalentCourseId, EquivalentCourseId = courseId });
        await db.SaveChangesAsync();
    }

    public async Task RemoveEquivalencyAsync(int courseId, int equivalentCourseId)
    {
        var equiv = await db.CourseEquivalencies
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.EquivalentCourseId == equivalentCourseId)
            ?? throw new KeyNotFoundException("Eşdeğerlik bulunamadı.");
        db.CourseEquivalencies.Remove(equiv);
        await db.SaveChangesAsync();
    }

    public async Task AssignToCurriculumAsync(int courseId, AssignToCurriculumDto dto)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == courseId))
            throw new KeyNotFoundException("Ders bulunamadı.");
        if (!await db.Curricula.AnyAsync(c => c.Id == dto.CurriculumId))
            throw new KeyNotFoundException("Müfredat bulunamadı.");
        if (await db.CurriculumCourses.AnyAsync(cc => cc.CurriculumId == dto.CurriculumId && cc.CourseId == courseId))
            throw new InvalidOperationException("Bu ders zaten bu müfredatta mevcut.");

        db.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = dto.CurriculumId,
            CourseId = courseId,
            SuggestedSemester = dto.SuggestedSemester,
            IsCompulsory = dto.IsCompulsory
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveFromCurriculumAsync(int courseId, int curriculumId)
    {
        var cc = await db.CurriculumCourses
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.CurriculumId == curriculumId)
            ?? throw new KeyNotFoundException("Müfredat ataması bulunamadı.");
        db.CurriculumCourses.Remove(cc);
        await db.SaveChangesAsync();
    }
}
