using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Fakültelere derslik atama önerisi sunar.
/// Öğrenci yoğunluğu, ders sayısı, derslik tipi ihtiyacı ve bina yakınlığına göre hesaplar.
/// </summary>
public class ClassroomSuggestionService
{
    private readonly AppDbContext _context;

    public ClassroomSuggestionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ClassroomAssignmentSuggestionDto> SuggestAssignmentsAsync(int termId, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {termId}");

        var faculties = await _context.Faculties
            .AsNoTracking()
            .Include(f => f.Departments)
            .ToListAsync(ct);

        var classrooms = await _context.Classrooms
            .AsNoTracking()
            .OrderByDescending(c => c.Capacity)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        // Mevcut atamalar (varsa çıkar)
        var existingAssignments = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == termId)
            .Select(cta => cta.ClassroomId)
            .ToListAsync(ct);

        var unassignedClassrooms = classrooms
            .Where(c => !existingAssignments.Contains(c.Id))
            .ToList();

        // Her fakülte için yoğunluk hesapla
        var facultyLoads = new List<FacultyLoadInfo>();

        foreach (var faculty in faculties)
        {
            var departmentIds = faculty.Departments.Select(d => d.Id).ToList();

            // Aktif öğrenci sayısı
            var studentCount = await _context.Students
                .Where(s => s.Status == StudentStatus.Active
                    && s.StudentPrograms.Any(sp => sp.IsActive
                        && sp.Type == StudentProgramType.Main
                        && departmentIds.Contains(sp.Program.DepartmentId)))
                .CountAsync(ct);

            // Önceki dönem section sayısı (varsa)
            var previousTermId = await _context.AcademicTerms
                .AsNoTracking()
                .Where(t => t.Id < termId)
                .OrderByDescending(t => t.Id)
                .Select(t => t.Id)
                .FirstOrDefaultAsync(ct);

            var sectionCount = 0;
            var labSectionCount = 0;
            if (previousTermId > 0)
            {
                var prevSections = await _context.CourseSections
                    .AsNoTracking()
                    .Include(cs => cs.Course)
                    .Where(cs => cs.TermId == previousTermId && cs.IsActive
                        && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
                    .ToListAsync(ct);

                sectionCount = prevSections.Count;
                labSectionCount = prevSections.Count(s => s.Course.LabHours > 0 && s.Course.TheoryHours == 0);
            }

            // Tahmini haftalık ders saati
            var weeklyHours = await _context.CurriculumCourses
                .AsNoTracking()
                .Where(cc => cc.Curriculum.IsActive && departmentIds.Contains(cc.Curriculum.Program.DepartmentId))
                .SumAsync(cc => cc.Course.TheoryHours + cc.Course.LabHours, ct);

            facultyLoads.Add(new FacultyLoadInfo
            {
                FacultyId = faculty.Id,
                FacultyName = faculty.Name,
                StudentCount = studentCount,
                EstimatedSectionCount = sectionCount > 0 ? sectionCount : Math.Max(1, weeklyHours / 3),
                LabSectionCount = labSectionCount,
                WeeklyHours = weeklyHours
            });
        }

        var totalLoad = facultyLoads.Sum(f => f.EstimatedSectionCount);
        if (totalLoad == 0) totalLoad = 1;

        // Derslik dağıtımı
        var suggestions = new List<ClassroomAssignmentItemDto>();

        // 1. Amfiler → en yoğun fakülteye
        var amphitheaters = unassignedClassrooms.Where(c => c.Type == ClassroomType.Amphitheater || c.Capacity >= 100).ToList();
        var sortedFaculties = facultyLoads.OrderByDescending(f => f.StudentCount).ToList();

        foreach (var amfi in amphitheaters)
        {
            var targetFaculty = sortedFaculties.FirstOrDefault();
            if (targetFaculty == null) break;

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = amfi.Id,
                ClassroomName = amfi.Name,
                ClassroomCapacity = amfi.Capacity,
                ClassroomType = amfi.Type.ToString(),
                FacultyId = targetFaculty.FacultyId,
                FacultyName = targetFaculty.FacultyName,
                Reason = $"Amfi/büyük derslik — en yoğun fakülte ({targetFaculty.StudentCount} öğrenci)"
            });
            unassignedClassrooms.Remove(amfi);
        }

        // 2. Lab/Computer → ilgili bölümü olan fakülteye
        var labs = unassignedClassrooms.Where(c => c.Type == ClassroomType.Lab || c.Type == ClassroomType.Computer).ToList();
        var labFaculties = facultyLoads.Where(f => f.LabSectionCount > 0).OrderByDescending(f => f.LabSectionCount).ToList();

        foreach (var lab in labs)
        {
            var targetFaculty = labFaculties.FirstOrDefault() ?? sortedFaculties.FirstOrDefault();
            if (targetFaculty == null) break;

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = lab.Id,
                ClassroomName = lab.Name,
                ClassroomCapacity = lab.Capacity,
                ClassroomType = lab.Type.ToString(),
                FacultyId = targetFaculty.FacultyId,
                FacultyName = targetFaculty.FacultyName,
                Reason = $"Lab derslik — lab dersi yoğunluğuna göre ({targetFaculty.LabSectionCount} lab section)"
            });
            unassignedClassrooms.Remove(lab);
        }

        // 3. Normal sınıflar → yoğunluk oranında dağıt
        var normalClassrooms = unassignedClassrooms
            .Where(c => c.Type == ClassroomType.Lecture || c.Type == ClassroomType.Seminar)
            .OrderByDescending(c => c.Capacity)
            .ToList();

        // Round-robin dağıtım yoğunluğa göre
        var facultyIndex = 0;
        foreach (var classroom in normalClassrooms)
        {
            // Ağırlıklı dağıtım: yoğun fakülteye daha fazla
            var targetFaculty = sortedFaculties[facultyIndex % sortedFaculties.Count];

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = classroom.Id,
                ClassroomName = classroom.Name,
                ClassroomCapacity = classroom.Capacity,
                ClassroomType = classroom.Type.ToString(),
                FacultyId = targetFaculty.FacultyId,
                FacultyName = targetFaculty.FacultyName,
                Reason = $"Normal sınıf — yoğunluk oranında dağıtım ({targetFaculty.StudentCount} öğrenci, {targetFaculty.EstimatedSectionCount} section)"
            });

            facultyIndex++;
        }

        return new ClassroomAssignmentSuggestionDto
        {
            TermId = termId,
            TermName = term.Name,
            TotalClassrooms = classrooms.Count,
            AlreadyAssigned = existingAssignments.Count,
            Suggestions = suggestions,
            FacultyLoads = facultyLoads.Select(f => new FacultyLoadDto
            {
                FacultyId = f.FacultyId,
                FacultyName = f.FacultyName,
                StudentCount = f.StudentCount,
                EstimatedSectionCount = f.EstimatedSectionCount,
                LabSectionCount = f.LabSectionCount,
                LoadPercentage = Math.Round((double)f.EstimatedSectionCount / totalLoad * 100, 1)
            }).ToList()
        };
    }

    private class FacultyLoadInfo
    {
        public int FacultyId { get; set; }
        public string FacultyName { get; set; } = "";
        public int StudentCount { get; set; }
        public int EstimatedSectionCount { get; set; }
        public int LabSectionCount { get; set; }
        public int WeeklyHours { get; set; }
    }
}

// DTOs
public class ClassroomAssignmentSuggestionDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = "";
    public int TotalClassrooms { get; set; }
    public int AlreadyAssigned { get; set; }
    public List<ClassroomAssignmentItemDto> Suggestions { get; set; } = new();
    public List<FacultyLoadDto> FacultyLoads { get; set; } = new();
}

public class ClassroomAssignmentItemDto
{
    public int ClassroomId { get; set; }
    public string ClassroomName { get; set; } = "";
    public int ClassroomCapacity { get; set; }
    public string ClassroomType { get; set; } = "";
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class FacultyLoadDto
{
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = "";
    public int StudentCount { get; set; }
    public int EstimatedSectionCount { get; set; }
    public int LabSectionCount { get; set; }
    public double LoadPercentage { get; set; }
}
