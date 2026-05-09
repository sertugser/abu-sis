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

        // Bölüm filtresi (bölüm seçiliyse fakülteden öncelikli)
        if (filter.DepartmentId.HasValue)
        {
            var deptId = filter.DepartmentId.Value;
            var curriculumCourseIds = db.CurriculumCourses
                .Where(cc => cc.Curriculum.Program.DepartmentId == deptId)
                .Select(cc => cc.CourseId);

            query = query.Where(c => c.DepartmentId == deptId || curriculumCourseIds.Contains(c.Id));
        }
        else if (filter.FacultyId.HasValue)
        {
            // Fakülte filtresi — sadece bölüm seçilmemişse
            var facultyDeptIds = db.Departments
                .Where(d => d.FacultyId == filter.FacultyId.Value)
                .Select(d => d.Id);

            var curriculumCourseIds = db.CurriculumCourses
                .Where(cc => facultyDeptIds.Contains(cc.Curriculum.Program.DepartmentId))
                .Select(cc => cc.CourseId);

            query = query.Where(c =>
                (c.DepartmentId.HasValue && facultyDeptIds.Contains(c.DepartmentId.Value)) ||
                curriculumCourseIds.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.Code.ToLower().Contains(s) ||
                c.Name.ToLower().Contains(s));
        }

        if (filter.Language.HasValue)
            query = query.Where(c => c.Language == filter.Language.Value);

        if (filter.Level.HasValue)
            query = query.Where(c => c.Level == filter.Level.Value);

        if (filter.Status.HasValue)
            query = query.Where(c => c.Status == filter.Status.Value);

        // 1. Ders satırları (anonim — sonra DTO'ya kuracağız)
        var rows = await query
            .OrderBy(c => c.Code)
            .Select(c => new
            {
                c.Id, c.Code, c.Name, c.EnglishName,
                c.Ects, c.TheoryHours, c.LabHours,
                c.IsNonGpa, c.ExamType,
                c.DepartmentId,
                DepartmentName = c.Department != null ? c.Department.Name : null,
                PrereqCount = c.Prerequisites.Count,
                EquivCount = c.Equivalencies.Count,
                c.Language, c.TeachingMethod, c.Status, c.Level,
                c.NationalCredit
            })
            .ToListAsync();

        var courseIds = rows.Select(r => r.Id).ToList();

        // 2a. Doğrudan müfredat atamaları
        var directAssignments = await db.CurriculumCourses
            .Where(cc => courseIds.Contains(cc.CourseId))
            .Select(cc => new
            {
                cc.CourseId,
                cc.SuggestedSemester,
                cc.IsCompulsory
            })
            .ToListAsync();

        // 2b. Havuz üzerinden atamalar (ElectiveGroup üyeliği → CurriculumElectivePool)
        var poolAssignments = await db.ElectiveGroupCourses
            .Where(egc => courseIds.Contains(egc.CourseId))
            .SelectMany(egc => db.CurriculumElectivePools
                .Where(p => p.ElectiveGroupId == egc.ElectiveGroupId)
                .Select(p => new
                {
                    egc.CourseId,
                    SuggestedSemester = p.SuggestedSemesterStart,
                    IsCompulsory = false  // havuz üyeleri seçmeli sayılır
                }))
            .ToListAsync();

        // 2c. İki kaynağı birleştir, ders bazında agregat
        var assignmentMap = directAssignments.Concat(poolAssignments)
            .GroupBy(x => x.CourseId)
            .ToDictionary(g => g.Key, g => new
            {
                MinSemester = g.Min(x => x.SuggestedSemester),
                MaxSemester = g.Max(x => x.SuggestedSemester),
                HasCompulsory = g.Any(x => x.IsCompulsory),
                HasElective = g.Any(x => !x.IsCompulsory)
            });

        // 3. Aktif dönem section bilgisi (KO/DO/Kon + Öğretim Üyesi)
        var now = DateTime.UtcNow;
        var activeTerm = await db.AcademicTerms
            .Where(t => !t.IsCancelled && t.TermStart <= now && t.TermEnd >= now)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync();

        var sectionMap = new Dictionary<int, (bool IsOpen, string? InstructorName)>();
        if (activeTerm != null)
        {
            var sections = await db.CourseSections
                .Where(s => courseIds.Contains(s.CourseId) && s.TermId == activeTerm.Id)
                .Include(s => s.Instructor)
                .Select(s => new
                {
                    s.CourseId,
                    s.InstructorId,
                    InstructorTitle = s.Instructor != null ? s.Instructor.Title : null,
                    InstructorUserId = s.Instructor != null ? (int?)s.Instructor.UserId : null
                })
                .ToListAsync();

            var userIds = sections.Where(s => s.InstructorUserId.HasValue)
                .Select(s => s.InstructorUserId!.Value).Distinct().ToList();
            var userMap = await db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            sectionMap = sections
                .GroupBy(s => s.CourseId)
                .ToDictionary(g => g.Key, g =>
                {
                    var withInstructor = g.FirstOrDefault(x => x.InstructorUserId.HasValue);
                    string? name = null;
                    if (withInstructor != null && userMap.TryGetValue(withInstructor.InstructorUserId!.Value, out var fullName))
                    {
                        name = string.IsNullOrWhiteSpace(withInstructor.InstructorTitle)
                            ? fullName
                            : $"{withInstructor.InstructorTitle} {fullName}";
                    }
                    return (IsOpen: true, InstructorName: name);
                });
        }

        // 4. DTO'ya birleştir
        return rows.Select(r =>
        {
            assignmentMap.TryGetValue(r.Id, out var a);
            string requirementType = a == null ? "None"
                : a.HasCompulsory && a.HasElective ? "Mixed"
                : a.HasCompulsory ? "Compulsory"
                : a.HasElective ? "Elective"
                : "None";

            sectionMap.TryGetValue(r.Id, out var sec);

            return new CourseListItemDto(
                r.Id, r.Code, r.Name, r.EnglishName,
                r.Ects, r.TheoryHours, r.LabHours,
                r.IsNonGpa, r.ExamType,
                r.DepartmentId, r.DepartmentName,
                r.PrereqCount, r.EquivCount,
                r.Language, r.TeachingMethod, r.Status, r.Level,
                r.NationalCredit,
                a?.MinSemester,
                a?.MaxSemester,
                requirementType,
                sec.IsOpen,
                sec.InstructorName);
        }).ToList();
    }

    public async Task<CourseDetailDto?> GetCourseByIdAsync(int id)
    {
        var course = await db.Courses
            .Include(c => c.Department)
            .Include(c => c.Prerequisites).ThenInclude(p => p.RequiredCourse)
            .Include(c => c.Equivalencies).ThenInclude(e => e.EquivalentCourse)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null) return null;

        // Doğrudan müfredat atamaları
        var direct = await db.CurriculumCourses
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

        // Havuz üzerinden müfredat atamaları (ElectiveGroup üyeliği → CurriculumElectivePool)
        var viaPool = await db.ElectiveGroupCourses
            .Where(egc => egc.CourseId == id)
            .SelectMany(egc => db.CurriculumElectivePools
                .Include(p => p.Curriculum).ThenInclude(cu => cu.Program)
                .Where(p => p.ElectiveGroupId == egc.ElectiveGroupId)
                .Select(p => new CurriculumAssignmentDto(
                    p.CurriculumId,
                    p.Curriculum.Program.Name,
                    p.Curriculum.Version,
                    p.Curriculum.IsActive,
                    p.SuggestedSemesterStart,
                    false))) // havuz üyeleri seçmeli sayılır
            .ToListAsync();

        var curriculumAssignments = direct.Concat(viaPool).ToList();

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
            course.Language, course.TeachingMethod, course.Status,
            course.Description, course.Level, course.NationalCredit,
            course.MinCreditsRequired,
            course.MinEctsRequired,
            course.RequiresNoFailedLowerCourses,
            course.CanBeTakenStandalone,
            // Phase 2 — Proliz B "Genel Bilgiler"
            course.AlternativeName,
            course.ShowInInfoPackage,
            course.ApplicationHours,
            course.OpenType,
            // Plan 10 — Derslik tipi tercihleri
            course.TheoryClassroomType,
            course.LabClassroomType,
            stats,
            chain
        );
    }

    private async Task<CourseStatsDto?> BuildCourseStatsAsync(int courseId)
    {
        // Toplam kaç dönem açılmış
        var totalTimesOpened = await db.CourseSections
            .Where(cs => cs.CourseId == courseId)
            .Select(cs => cs.TermId)
            .Distinct()
            .CountAsync();

        if (totalTimesOpened == 0) return null;

        // Tüm dönemlerde toplam öğrenci
        var totalStudentsAllTime = await db.CourseRegistrations
            .Where(cr => cr.CourseSection.CourseId == courseId)
            .CountAsync();

        // Bu ders kaç dersin önkoşulu
        var prerequisiteForCount = await db.CoursePrerequisites
            .Where(cp => cp.RequiredCourseId == courseId)
            .CountAsync();

        // En son dönem
        var lastTermId = await db.CourseSections
            .Where(cs => cs.CourseId == courseId)
            .OrderByDescending(cs => cs.TermId)
            .Select(cs => cs.TermId)
            .FirstOrDefaultAsync();

        var lastTerm = lastTermId > 0
            ? await db.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == lastTermId)
            : null;

        var lastTermSections = lastTermId > 0
            ? await db.CourseSections
                .Where(cs => cs.CourseId == courseId && cs.TermId == lastTermId)
                .ToListAsync()
            : new List<CourseSection>();

        var lastTermEnrolled = lastTermSections.Sum(s => s.EnrolledCount);
        var lastTermCapacity = lastTermSections.Sum(s => s.Capacity);
        var fillRate = lastTermCapacity > 0 ? (double)lastTermEnrolled / lastTermCapacity * 100 : 0;

        // Not dağılımı (tamamlanmış kayıtlar)
        var gradeData = await db.CourseRegistrations
            .Where(cr => cr.CourseSection.CourseId == courseId && cr.Grade != null)
            .GroupBy(cr => cr.Grade)
            .Select(g => new { Grade = g.Key!.Value, Count = g.Count() })
            .ToListAsync();

        var gradeDistribution = gradeData.ToDictionary(g => g.Grade.ToString(), g => g.Count);

        // Başarı oranı: AA-DD arası geçen / toplam notlu
        var totalGraded = gradeData.Sum(g => g.Count);
        var passGrades = new[] { "AA", "BA", "BB", "CB", "CC", "DC", "DD", "S" };
        var totalPassed = gradeData.Where(g => passGrades.Contains(g.Grade.ToString())).Sum(g => g.Count);
        var passRate = totalGraded > 0 ? (double)totalPassed / totalGraded * 100 : 0;

        // Bırakma/Çekilme
        var totalWithdrawn = await db.CourseRegistrations
            .Where(cr => cr.CourseSection.CourseId == courseId && cr.Status == Domain.Enums.RegistrationStatus.Withdrawn)
            .CountAsync();

        var totalDropped = await db.CourseRegistrations
            .Where(cr => cr.CourseSection.CourseId == courseId && cr.Status == Domain.Enums.RegistrationStatus.Dropped)
            .CountAsync();

        // Dönem bazlı trend (son 5 dönem)
        var termIds = await db.CourseSections
            .Where(cs => cs.CourseId == courseId)
            .Select(cs => cs.TermId)
            .Distinct()
            .OrderByDescending(id => id)
            .Take(5)
            .ToListAsync();

        var termTrends = new List<TermTrendDto>();
        if (termIds.Count > 0)
        {
            var terms = await db.AcademicTerms
                .Where(t => termIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name);

            var sectionsByTerm = await db.CourseSections
                .Where(cs => cs.CourseId == courseId && termIds.Contains(cs.TermId))
                .GroupBy(cs => cs.TermId)
                .Select(g => new
                {
                    TermId = g.Key,
                    SectionCount = g.Count(),
                    Enrolled = g.Sum(s => s.EnrolledCount),
                    Capacity = g.Sum(s => s.Capacity)
                })
                .ToListAsync();

            termTrends = termIds
                .OrderBy(id => id)
                .Select(tid =>
                {
                    var s = sectionsByTerm.FirstOrDefault(x => x.TermId == tid);
                    return new TermTrendDto(
                        terms.GetValueOrDefault(tid, "?"),
                        s?.SectionCount ?? 0,
                        s?.Enrolled ?? 0,
                        s?.Capacity ?? 0
                    );
                })
                .ToList();
        }

        return new CourseStatsDto(
            TotalTimesOpened: totalTimesOpened,
            TotalStudentsAllTime: totalStudentsAllTime,
            PrerequisiteForCount: prerequisiteForCount,
            LastTermName: lastTerm?.Name,
            LastTermSectionCount: lastTermSections.Count,
            LastTermTotalEnrolled: lastTermEnrolled,
            LastTermTotalCapacity: lastTermCapacity,
            LastTermFillRate: Math.Round(fillRate, 1),
            GradeDistribution: gradeDistribution,
            PassRate: Math.Round(passRate, 1),
            TotalWithdrawn: totalWithdrawn,
            TotalDropped: totalDropped,
            TermTrends: termTrends
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
        // Madde 10: Yeni derslerde sahip birim zorunlu — ortak dersler Koordinatörlük'e atanır
        if (!dto.DepartmentId.HasValue)
            throw new InvalidOperationException("Ders sahipliği zorunludur. Bölüm veya koordinatörlük seçin.");
        if (!await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId.Value))
            throw new KeyNotFoundException("Bölüm bulunamadı.");
        // Ulusal Kredi: YÖKSİS raporlama için zorunlu, makul aralıkta olmalı.
        if (dto.NationalCredit < 0 || dto.NationalCredit > 20)
            throw new InvalidOperationException("Ulusal Kredi 0-20 aralığında olmalıdır.");
        if (dto.Ects < 1 || dto.Ects > 30)
            throw new InvalidOperationException("AKTS 1-30 aralığında olmalıdır.");

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
            DepartmentId = dto.DepartmentId,
            Language = dto.Language,
            TeachingMethod = dto.TeachingMethod,
            Status = dto.Status,
            Description = dto.Description,
            Level = dto.Level,
            NationalCredit = dto.NationalCredit,
            MinCreditsRequired = dto.MinCreditsRequired,
            MinEctsRequired = dto.MinEctsRequired,
            RequiresNoFailedLowerCourses = dto.RequiresNoFailedLowerCourses,
            CanBeTakenStandalone = dto.CanBeTakenStandalone,
            // Phase 2 — Proliz B "Genel Bilgiler"
            AlternativeName = dto.AlternativeName,
            ShowInInfoPackage = dto.ShowInInfoPackage,
            ApplicationHours = dto.ApplicationHours,
            OpenType = dto.OpenType,
            // Plan 10 — Derslik tipi tercihleri
            TheoryClassroomType = dto.TheoryClassroomType,
            LabClassroomType = dto.LabClassroomType
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
        // Madde 10: Güncelleme sırasında da sahiplik korunur
        if (!dto.DepartmentId.HasValue)
            throw new InvalidOperationException("Ders sahipliği zorunludur. Bölüm veya koordinatörlük seçin.");
        if (!await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId.Value))
            throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (dto.NationalCredit < 0 || dto.NationalCredit > 20)
            throw new InvalidOperationException("Ulusal Kredi 0-20 aralığında olmalıdır.");
        if (dto.Ects < 1 || dto.Ects > 30)
            throw new InvalidOperationException("AKTS 1-30 aralığında olmalıdır.");

        course.Code = dto.Code;
        course.Name = dto.Name;
        course.EnglishName = dto.EnglishName;
        course.Ects = dto.Ects;
        course.TheoryHours = dto.TheoryHours;
        course.LabHours = dto.LabHours;
        course.IsNonGpa = dto.IsNonGpa;
        course.ExamType = dto.ExamType;
        course.DepartmentId = dto.DepartmentId;
        course.Language = dto.Language;
        course.TeachingMethod = dto.TeachingMethod;
        course.Status = dto.Status;
        course.Description = dto.Description;
        course.Level = dto.Level;
        course.NationalCredit = dto.NationalCredit;
        course.MinCreditsRequired = dto.MinCreditsRequired;
        course.MinEctsRequired = dto.MinEctsRequired;
        course.RequiresNoFailedLowerCourses = dto.RequiresNoFailedLowerCourses;
        course.CanBeTakenStandalone = dto.CanBeTakenStandalone;
        // Phase 2 — Proliz B "Genel Bilgiler"
        course.AlternativeName = dto.AlternativeName;
        course.ShowInInfoPackage = dto.ShowInInfoPackage;
        course.ApplicationHours = dto.ApplicationHours;
        course.OpenType = dto.OpenType;
        // Plan 10 — Derslik tipi tercihleri
        course.TheoryClassroomType = dto.TheoryClassroomType;
        course.LabClassroomType = dto.LabClassroomType;
        await db.SaveChangesAsync();
        return (await GetCourseByIdAsync(id))!;
    }

    public async Task DeleteCourseAsync(int id)
    {
        var reasons = new List<string>();

        // Aktif şubeler
        var sectionTerms = await db.CourseSections
            .Where(s => s.CourseId == id)
            .Include(s => s.Term)
            .Select(s => s.Term.Name)
            .Distinct()
            .ToListAsync();
        if (sectionTerms.Count > 0)
            reasons.Add($"Açıldığı dönemler: {string.Join(", ", sectionTerms)}");

        // Kayıtlar
        var registrationCount = await db.CourseRegistrations
            .Where(r => r.CourseSection.CourseId == id)
            .CountAsync();
        if (registrationCount > 0)
            reasons.Add($"{registrationCount} öğrenci kaydı mevcut");

        // Müfredatlar
        var curricula = await db.CurriculumCourses
            .Where(cc => cc.CourseId == id)
            .Include(cc => cc.Curriculum).ThenInclude(c => c.Program)
            .Select(cc => cc.Curriculum.Program.Name + " (v" + cc.Curriculum.Version + ")")
            .Distinct()
            .ToListAsync();
        if (curricula.Count > 0)
            reasons.Add($"Müfredatlar: {string.Join(", ", curricula)}");

        // Önkoşul bağımlılıkları
        var dependentCourses = await db.CoursePrerequisites
            .Where(cp => cp.RequiredCourseId == id)
            .Include(cp => cp.Course)
            .Select(cp => cp.Course.Code)
            .ToListAsync();
        if (dependentCourses.Count > 0)
            reasons.Add($"Önkoşul olduğu dersler: {string.Join(", ", dependentCourses)}");

        if (reasons.Count > 0)
            throw new InvalidOperationException(
                $"Bu ders silinemez. Kullanıldığı yerler:\n{string.Join("\n", reasons.Select(r => "• " + r))}");

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

    // Proliz "Diğer Ön Koşul Tanımları" — sadece course-level kural alanlarını günceller.
    public async Task<CourseDetailDto> UpdateCoursePrereqRulesAsync(int courseId, UpdateCoursePrereqRulesDto dto)
    {
        var course = await db.Courses.FindAsync(courseId)
            ?? throw new KeyNotFoundException("Ders bulunamadı.");

        // Negatif değer engellenir; null = "kural tanımlı değil" anlamına gelir.
        if (dto.MinCreditsRequired.HasValue && dto.MinCreditsRequired.Value < 0)
            throw new InvalidOperationException("Min kredi negatif olamaz.");
        if (dto.MinEctsRequired.HasValue && dto.MinEctsRequired.Value < 0)
            throw new InvalidOperationException("Min AKTS negatif olamaz.");

        course.MinCreditsRequired = dto.MinCreditsRequired;
        course.MinEctsRequired = dto.MinEctsRequired;
        course.RequiresNoFailedLowerCourses = dto.RequiresNoFailedLowerCourses;
        course.CanBeTakenStandalone = dto.CanBeTakenStandalone;
        await db.SaveChangesAsync();
        return (await GetCourseByIdAsync(courseId))!;
    }

}
