using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class CourseOpeningService(AppDbContext context) : ICourseOpeningService
{
    private readonly AppDbContext _context = context;

    public async Task<OpenedCourseListDto> GetOpenedCoursesAsync(int departmentId, int termId, CancellationToken ct)
    {
        var department = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == departmentId, ct)
            ?? throw new InvalidOperationException("Bölüm bulunamadı.");
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == termId && cs.Course.DepartmentId == departmentId)
            .OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.Id)
            .ToListAsync(ct);

        // Load instructor user names
        var instructorIds = sections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct().ToList();
        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, ct);

        // Load registered counts (Status != Dropped && Status != Draft)
        var sectionIds = sections.Select(s => s.Id).ToList();
        var registeredCounts = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r => sectionIds.Contains(r.CourseSectionId) && r.Status != RegistrationStatus.Dropped && r.Status != RegistrationStatus.Draft)
            .GroupBy(r => r.CourseSectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

        // Load suggested semester from curriculum
        var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var suggestedSemesters = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId) && cc.Curriculum.Program.DepartmentId == departmentId && cc.Curriculum.IsActive)
            .GroupBy(cc => cc.CourseId)
            .Select(g => new { CourseId = g.Key, Semester = g.Min(cc => cc.SuggestedSemester) })
            .ToDictionaryAsync(x => x.CourseId, x => x.Semester, ct);

        // Compute section numbers per course
        var sectionNumberMap = new Dictionary<int, int>();
        var courseGroupCounters = new Dictionary<int, int>();
        foreach (var s in sections)
        {
            if (!courseGroupCounters.TryGetValue(s.CourseId, out var counter))
                counter = 0;
            counter++;
            courseGroupCounters[s.CourseId] = counter;
            sectionNumberMap[s.Id] = counter;
        }

        var dtos = sections.Select(s =>
        {
            instructorNameMap.TryGetValue(s.InstructorId ?? 0, out var instrName);
            registeredCounts.TryGetValue(s.Id, out var regCount);
            suggestedSemesters.TryGetValue(s.CourseId, out var sugSem);

            return new OpenedCourseSectionDto
            {
                SectionId = s.Id,
                SectionNumber = sectionNumberMap[s.Id],
                CourseId = s.CourseId,
                CourseCode = s.Course.Code,
                CourseName = s.Course.Name,
                TheoryHours = s.Course.TheoryHours,
                LabHours = s.Course.LabHours,
                Credits = s.Course.TheoryHours + s.Course.LabHours,
                Ects = s.Course.Ects,
                SuggestedSemester = sugSem,
                Capacity = s.Capacity,
                EnrolledCount = s.EnrolledCount,
                RegisteredCount = regCount,
                InstructorId = s.InstructorId,
                InstructorName = instrName,
                IsActive = s.IsActive,
            };
        }).ToList();

        return new OpenedCourseListDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TermId = termId,
            TermName = term.Name,
            TotalSections = dtos.Count,
            Sections = dtos,
        };
    }

    public async Task<List<CurriculumCourseForOpeningDto>> GetCurriculumCoursesForOpeningAsync(int departmentId, int termId, bool showAll = false, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct);
        var semesterParity = term?.Semester == SemesterType.Spring ? 0 : 1; // Spring→even(2,4,6,8), Fall→odd(1,3,5,7)

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Where(cc => cc.Curriculum.IsActive && cc.Curriculum.Program.DepartmentId == departmentId)
            .Where(cc => showAll || term == null || term.Semester == SemesterType.Summer || cc.SuggestedSemester % 2 == semesterParity)
            .OrderBy(cc => cc.SuggestedSemester).ThenBy(cc => cc.Course.Code)
            .ToListAsync(ct);

        var courseIds = curriculumCourses.Select(cc => cc.CourseId).Distinct().ToList();
        var existingSectionCounts = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == termId && courseIds.Contains(cs.CourseId))
            .GroupBy(cs => cs.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct);

        return curriculumCourses
            .GroupBy(cc => cc.CourseId)
            .Select(g =>
            {
                var first = g.First();
                existingSectionCounts.TryGetValue(first.CourseId, out var sectionCount);
                return new CurriculumCourseForOpeningDto
                {
                    CourseId = first.CourseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    TheoryHours = first.Course.TheoryHours,
                    LabHours = first.Course.LabHours,
                    Ects = first.Course.Ects,
                    SuggestedSemester = first.SuggestedSemester,
                    IsCompulsory = first.IsCompulsory,
                    AlreadyOpened = sectionCount > 0,
                    ExistingSectionCount = sectionCount,
                };
            })
            .OrderBy(c => c.SuggestedSemester).ThenBy(c => c.CourseCode)
            .ToList();
    }

    public async Task<List<InstructorOptionDto>> GetDepartmentInstructorsAsync(int departmentId, CancellationToken ct)
    {
        var instructors = await _context.Instructors
            .AsNoTracking()
            .Where(i => i.DepartmentId == departmentId)
            .Select(i => new { i.Id, i.Title, i.UserId })
            .ToListAsync(ct);

        var userIds = instructors.Select(i => i.UserId).Distinct().ToList();
        var userMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FirstName, u.LastName }, ct);

        return instructors.Select(i =>
        {
            var name = userMap.TryGetValue(i.UserId, out var u) ? $"{i.Title} {u.FirstName} {u.LastName}" : $"{i.Title} (#{i.Id})";
            return new InstructorOptionDto { InstructorId = i.Id, FullName = name };
        })
        .OrderBy(i => i.FullName)
        .ToList();
    }

    public async Task<List<OpenedCourseSectionDto>> OpenCourseAsync(int departmentId, int termId, OpenCourseRequest request, CancellationToken ct)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (term.Status != TermStatus.Planning)
            throw new InvalidOperationException(
                $"Ders açmak için dönem 'Planlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, ct)
            ?? throw new InvalidOperationException($"Ders bulunamadı (ID: {request.CourseId}).");

        if (course.DepartmentId.HasValue && course.DepartmentId != departmentId)
            throw new InvalidOperationException($"Ders '{course.Code}' bu bölüme ait değil.");

        // Kapasite validasyonu
        if (request.CapacityPerSection <= 0)
            throw new InvalidOperationException("Kontenjan 0'dan büyük olmalıdır.");

        // Şube sayısı üst sınırı
        if (request.SectionCount <= 0 || request.SectionCount > 20)
            throw new InvalidOperationException("Şube sayısı 1 ile 20 arasında olmalıdır.");

        // Duplikat kontrol: bu dönemde bu ders zaten açılmış mı?
        var existingCount = await _context.CourseSections
            .CountAsync(cs => cs.CourseId == request.CourseId && cs.TermId == termId && cs.IsActive, ct);
        if (existingCount > 0)
            throw new InvalidOperationException(
                $"'{course.Code}' dersi bu dönemde zaten {existingCount} şube ile açılmış. Yeni şube eklemek için mevcut şubeyi güncelleyin.");

        // InstructorId validasyonu
        if (request.InstructorId.HasValue)
        {
            var instructor = await _context.Instructors.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.InstructorId.Value, ct);
            if (instructor == null)
                throw new InvalidOperationException("Atanan öğretim üyesi bulunamadı.");
            // Ortak ders olmadığı sürece, hocanın bölümünü kontrol et
            if (course.DepartmentId.HasValue && instructor.DepartmentId != departmentId)
                throw new InvalidOperationException("Öğretim üyesi bu bölüme ait değil.");
        }

        var created = new List<CourseSection>();
        for (var i = 0; i < request.SectionCount; i++)
        {
            var section = new CourseSection
            {
                CourseId = request.CourseId,
                TermId = termId,
                Capacity = request.CapacityPerSection,
                InstructorId = request.InstructorId,
                EnrolledCount = 0,
                IsActive = true,
                IsFinalized = false,
                ScheduleJson = "[]",
            };
            _context.CourseSections.Add(section);
            created.Add(section);
        }
        await _context.SaveChangesAsync(ct);

        // Resolve instructor name if set
        string? instructorName = null;
        if (request.InstructorId.HasValue)
        {
            var nameMap = await LoadInstructorNameMapAsync([request.InstructorId.Value], ct);
            nameMap.TryGetValue(request.InstructorId.Value, out instructorName);
        }

        // Get suggested semester
        var sugSem = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CourseId == request.CourseId && cc.Curriculum.IsActive && cc.Curriculum.Program.DepartmentId == departmentId)
            .Select(cc => (int?)cc.SuggestedSemester)
            .MinAsync(ct) ?? 0;

        // Get existing section count to compute section numbers
        var existingSections = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == termId && cs.CourseId == request.CourseId)
            .OrderBy(cs => cs.Id)
            .Select(cs => cs.Id)
            .ToListAsync(ct);

        return created.Select(s =>
        {
            var sectionNumber = existingSections.IndexOf(s.Id) + 1;
            return new OpenedCourseSectionDto
            {
                SectionId = s.Id,
                SectionNumber = sectionNumber > 0 ? sectionNumber : 1,
                CourseId = s.CourseId,
                CourseCode = course.Code,
                CourseName = course.Name,
                TheoryHours = course.TheoryHours,
                LabHours = course.LabHours,
                Credits = course.TheoryHours + course.LabHours,
                Ects = course.Ects,
                SuggestedSemester = sugSem,
                Capacity = s.Capacity,
                EnrolledCount = 0,
                RegisteredCount = 0,
                InstructorId = s.InstructorId,
                InstructorName = instructorName,
                IsActive = true,
            };
        }).ToList();
    }

    public async Task<BulkOpenCoursesResultDto> BulkOpenCoursesAsync(int departmentId, int termId, BulkOpenCoursesRequest request, CancellationToken ct)
    {
        var result = new BulkOpenCoursesResultDto();

        foreach (var courseReq in request.Courses)
        {
            try
            {
                var created = await OpenCourseAsync(departmentId, termId, courseReq, ct);
                result.TotalCoursesOpened++;
                result.TotalSectionsCreated += created.Count;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Ders ID {courseReq.CourseId}: {ex.Message}");
            }
        }

        return result;
    }

    public async Task<OpenedCourseSectionDto> UpdateSectionAsync(UpdateOpenedSectionRequest request, CancellationToken ct)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct)
            ?? throw new InvalidOperationException("Şube bulunamadı.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.Status != TermStatus.Planning && term.Status != TermStatus.Scheduling)
            throw new InvalidOperationException(
                $"Şube güncellemek için dönem 'Planlama' veya 'Programlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        if (!section.IsActive)
            throw new InvalidOperationException("Pasif şube güncellenemez.");

        if (request.InstructorId.HasValue)
            section.InstructorId = request.InstructorId.Value;

        if (request.Capacity.HasValue)
        {
            if (request.Capacity.Value <= 0)
                throw new InvalidOperationException("Kontenjan 0'dan büyük olmalıdır.");
            if (request.Capacity.Value < section.EnrolledCount)
                throw new InvalidOperationException(
                    $"Kontenjan mevcut kayıtlı öğrenci sayısından ({section.EnrolledCount}) az olamaz.");
            section.Capacity = request.Capacity.Value;
        }

        await _context.SaveChangesAsync(ct);

        // Build response
        string? instructorName = null;
        if (section.InstructorId.HasValue)
        {
            var nameMap = await LoadInstructorNameMapAsync([section.InstructorId.Value], ct);
            nameMap.TryGetValue(section.InstructorId.Value, out instructorName);
        }

        var registeredCount = await _context.CourseRegistrations
            .CountAsync(r => r.CourseSectionId == section.Id && r.Status != RegistrationStatus.Dropped && r.Status != RegistrationStatus.Draft, ct);

        var sectionNumber = await _context.CourseSections
            .Where(cs => cs.TermId == section.TermId && cs.CourseId == section.CourseId && cs.Id <= section.Id)
            .CountAsync(ct);

        var sugSem = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CourseId == section.CourseId && cc.Curriculum.IsActive)
            .Select(cc => (int?)cc.SuggestedSemester)
            .MinAsync(ct) ?? 0;

        return new OpenedCourseSectionDto
        {
            SectionId = section.Id,
            SectionNumber = sectionNumber,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            Credits = section.Course.TheoryHours + section.Course.LabHours,
            Ects = section.Course.Ects,
            SuggestedSemester = sugSem,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            RegisteredCount = registeredCount,
            InstructorId = section.InstructorId,
            InstructorName = instructorName,
            IsActive = section.IsActive,
        };
    }

    public async Task CloseSectionAsync(int sectionId, CancellationToken ct)
    {
        var section = await _context.CourseSections.FirstOrDefaultAsync(cs => cs.Id == sectionId, ct)
            ?? throw new InvalidOperationException("Şube bulunamadı.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.Status != TermStatus.Planning && term.Status != TermStatus.Scheduling)
            throw new InvalidOperationException(
                $"Şube kapatmak için dönem 'Planlama' veya 'Programlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        // Draft/PendingAdvisorApproval kayıtlarını temizle
        var pendingRegistrations = await _context.CourseRegistrations
            .Where(cr => cr.CourseSectionId == sectionId
                && (cr.Status == RegistrationStatus.Draft || cr.Status == RegistrationStatus.PendingAdvisorApproval))
            .ToListAsync(ct);

        foreach (var reg in pendingRegistrations)
        {
            reg.Status = RegistrationStatus.Dropped;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        // Aktif kayıtlı öğrenci varsa kapatılamaz
        var activeCount = await _context.CourseRegistrations
            .CountAsync(cr => cr.CourseSectionId == sectionId && cr.Status == RegistrationStatus.Active, ct);
        if (activeCount > 0)
            throw new InvalidOperationException($"Aktif kayıtlı {activeCount} öğrenci bulunan şube kapatılamaz.");

        section.IsActive = false;
        section.EnrolledCount = 0;
        await _context.SaveChangesAsync(ct);
    }

    private static string GetTermStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Draft            => "Taslak",
        TermStatus.Planning         => "Planlama",
        TermStatus.Scheduling       => "Programlama",
        TermStatus.Published        => "Yayınlandı",
        TermStatus.RegistrationOpen => "Kayıt Açık",
        TermStatus.AddDrop          => "Ders Ekle/Bırak",
        TermStatus.WithdrawalPeriod => "Çekilme Dönemi",
        TermStatus.GradeEntry       => "Not Girişi",
        TermStatus.Closed           => "Kapalı",
        TermStatus.Archived         => "Arşivlendi",
        _                           => s.ToString()
    };

    private async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(IEnumerable<int> instructorIds, CancellationToken ct)
    {
        var ids = instructorIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var instructors = await _context.Instructors
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.Title, i.UserId })
            .ToListAsync(ct);

        var userIds = instructors.Select(i => i.UserId).Distinct().ToList();
        var userMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FirstName, u.LastName }, ct);

        return instructors.ToDictionary(
            i => i.Id,
            i => userMap.TryGetValue(i.UserId, out var u) ? $"{i.Title} {u.FirstName} {u.LastName}" : $"{i.Title} (#{i.Id})");
    }
}
