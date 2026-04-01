using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Fakültelere derslik atama önerisi sunar.
/// Draft aşamasında çalışır — section'lar henüz açılmamıştır.
/// Müfredat analizi, öğrenci yoğunluğu, derslik tipi ihtiyacı ve tarihsel veriye dayanır.
/// </summary>
public class ClassroomSuggestionService
{
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _config;

    public ClassroomSuggestionService(AppDbContext context, ISystemConfigProvider config)
    {
        _context = context;
        _config = config;
    }

    public async Task<ClassroomAssignmentSuggestionDto> SuggestAssignmentsAsync(int termId, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {termId}");

        var amphiThreshold = await _config.GetIntAsync("Classroom.AmphiThreshold", 100, ct);

        // ── 1. Temel veriler ──────────────────────────────────────────────
        var faculties = await _context.Faculties
            .AsNoTracking()
            .Include(f => f.Departments)
            .ToListAsync(ct);

        var classrooms = await _context.Classrooms
            .AsNoTracking()
            .OrderByDescending(c => c.Capacity)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        var existingAssignments = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == termId)
            .Select(cta => cta.ClassroomId)
            .ToListAsync(ct);

        var unassignedClassrooms = classrooms
            .Where(c => !existingAssignments.Contains(c.Id))
            .ToList();

        // Ortalama derslik kapasitesi (section sayısı tahmini için)
        var avgCapacity = classrooms.Count > 0
            ? (int)classrooms.Average(c => c.Capacity)
            : 50;

        // Önceki dönem (tarihsel analiz için)
        var previousTermId = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.Id < termId)
            .OrderByDescending(t => t.Id)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(ct);

        // Dönemin yarıyıl paritesi (Güz→tek, Bahar→çift, Yaz→tümü)
        var termSemester = term.Semester;
        var semesterParity = termSemester == SemesterType.Spring ? 0 : 1;
        var isSummer = termSemester == SemesterType.Summer;

        // ── 2. Her fakülte için talep analizi ─────────────────────────────
        var facultyDemands = new List<FacultyDemandAnalysis>();

        foreach (var faculty in faculties)
        {
            var departmentIds = faculty.Departments.Select(d => d.Id).ToList();
            if (departmentIds.Count == 0)
            {
                facultyDemands.Add(FacultyDemandAnalysis.Empty(faculty.Id, faculty.Name));
                continue;
            }

            // ── 2a. Aktif öğrenci sayısı ──────────────────────────────────
            var studentCount = await _context.Students
                .Where(s => s.Status == StudentStatus.Active
                    && s.StudentPrograms.Any(sp => sp.IsActive
                        && sp.Type == StudentProgramType.Main
                        && departmentIds.Contains(sp.Program.DepartmentId)))
                .CountAsync(ct);

            // ── 2b. Müfredat dersleri (dönem paritesine göre) ─────────────
            var curriculumCoursesQuery = _context.CurriculumCourses
                .AsNoTracking()
                .Include(cc => cc.Course)
                .Where(cc => cc.Curriculum.IsActive
                    && departmentIds.Contains(cc.Curriculum.Program.DepartmentId));

            if (!isSummer)
                curriculumCoursesQuery = curriculumCoursesQuery
                    .Where(cc => cc.SuggestedSemester % 2 == semesterParity);

            var curriculumCourses = await curriculumCoursesQuery.ToListAsync(ct);

            // Benzersiz dersler (aynı ders birden fazla müfredatta olabilir)
            var uniqueCourses = curriculumCourses
                .GroupBy(cc => cc.CourseId)
                .Select(g => new
                {
                    CourseId = g.Key,
                    Course = g.First().Course,
                    IsCompulsory = g.Any(cc => cc.IsCompulsory),
                    SuggestedSemesters = g.Select(cc => cc.SuggestedSemester).Distinct().ToList()
                })
                .ToList();

            // ── 2c. Haftalık ders saati ihtiyacı ─────────────────────────
            int totalWeeklyTheoryHours = 0;
            int totalWeeklyLabHours = 0;
            int labCourseCount = 0;
            int amphiNeedCount = 0;
            int estimatedTotalSections = 0;

            foreach (var uc in uniqueCourses)
            {
                var course = uc.Course;

                // Bu ders için tahmini öğrenci sayısı:
                // Zorunlu → tüm sınıf, Seçmeli → %30-40 oranında
                int estimatedDemand;
                if (uc.IsCompulsory)
                {
                    // Zorunlu ders: ilgili sınıf seviyesindeki öğrenci sayısı
                    // Basitleştirme: toplam öğrenci / toplam yıl sayısı (4 yıl varsayılan)
                    var yearCount = Math.Max(1, curriculumCourses
                        .Select(cc => (cc.SuggestedSemester + 1) / 2)
                        .DefaultIfEmpty(1)
                        .Max());
                    estimatedDemand = Math.Max(1, studentCount / yearCount);
                }
                else
                {
                    // Seçmeli ders: tahmini talep = zorunlunun %35'i
                    var yearCount = Math.Max(1, curriculumCourses
                        .Select(cc => (cc.SuggestedSemester + 1) / 2)
                        .DefaultIfEmpty(1)
                        .Max());
                    estimatedDemand = Math.Max(1, (int)(studentCount / yearCount * 0.35));
                }

                // Kaç section gerekecek?
                int sectionsNeeded = Math.Max(1, (int)Math.Ceiling((double)estimatedDemand / avgCapacity));
                estimatedTotalSections += sectionsNeeded;

                // Teori saatleri
                if (course.TheoryHours > 0)
                    totalWeeklyTheoryHours += course.TheoryHours * sectionsNeeded;

                // Lab saatleri
                if (course.LabHours > 0)
                {
                    totalWeeklyLabHours += course.LabHours * sectionsNeeded;
                    labCourseCount++;
                }

                // Amfi ihtiyacı: tahmini talep >= eşik ve tek section olarak verilecekse
                if (estimatedDemand >= amphiThreshold)
                    amphiNeedCount++;
            }

            // ── 2d. Paralel derslik ihtiyacı ─────────────────────────────
            // Haftada kaç saat derslik kullanılacak (teori + lab ayrı tip)
            int totalWeeklySlotDemand = totalWeeklyTheoryHours + totalWeeklyLabHours;

            // Haftada kullanılabilir ders saati sayısı (tipik: 5 gün × 6 slot = 30)
            // Bu değer aslında TimeSlot grid'inden hesaplanabilir ama
            // derslik ataması aşamasında grid henüz oluşmamış olabilir
            const int availableWeeklySlotsPerClassroom = 30;

            // Aynı anda kaç derslik gerekli (paralel ihtiyaç)
            int parallelClassroomNeed = totalWeeklySlotDemand > 0
                ? (int)Math.Ceiling((double)totalWeeklySlotDemand / availableWeeklySlotsPerClassroom)
                : 0;

            // Lab derslik ihtiyacı
            int parallelLabNeed = totalWeeklyLabHours > 0
                ? (int)Math.Ceiling((double)totalWeeklyLabHours / availableWeeklySlotsPerClassroom)
                : 0;

            // ── 2e. Tarihsel analiz (önceki dönem) ───────────────────────
            int historicalSectionCount = 0;
            int historicalLabSectionCount = 0;
            int historicalClassroomUsage = 0;
            if (previousTermId > 0)
            {
                var prevSections = await _context.CourseSections
                    .AsNoTracking()
                    .Include(cs => cs.Course)
                    .Where(cs => cs.TermId == previousTermId && cs.IsActive
                        && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
                    .ToListAsync(ct);

                historicalSectionCount = prevSections.Count;
                historicalLabSectionCount = prevSections.Count(s =>
                    s.Course.LabHours > 0 && s.Course.TheoryHours == 0);

                // Kaç farklı derslik kullanılmış?
                historicalClassroomUsage = await _context.Set<SectionSchedule>()
                    .AsNoTracking()
                    .Where(ss => prevSections.Select(ps => ps.Id).Contains(ss.CourseSectionId))
                    .Select(ss => ss.ClassroomId)
                    .Distinct()
                    .CountAsync(ct);
            }

            // ── 2f. Final ihtiyaç hesabı ─────────────────────────────────
            // Tarihsel veriyle tahminleri harmanlama (varsa)
            int finalClassroomNeed;
            if (historicalClassroomUsage > 0)
            {
                // Tarihsel veri: %60 ağırlık, müfredat tahmini: %40 ağırlık
                finalClassroomNeed = (int)Math.Ceiling(
                    historicalClassroomUsage * 0.6 + parallelClassroomNeed * 0.4);
            }
            else
            {
                finalClassroomNeed = parallelClassroomNeed;
            }

            // Lab derslik ihtiyacı: tarihsel + tahmin
            int finalLabNeed;
            if (historicalLabSectionCount > 0)
            {
                finalLabNeed = (int)Math.Ceiling(
                    historicalLabSectionCount * 0.6 + parallelLabNeed * 0.4);
            }
            else
            {
                finalLabNeed = Math.Max(parallelLabNeed, labCourseCount > 0 ? 1 : 0);
            }

            facultyDemands.Add(new FacultyDemandAnalysis
            {
                FacultyId = faculty.Id,
                FacultyName = faculty.Name,
                StudentCount = studentCount,
                CurriculumCourseCount = uniqueCourses.Count,
                CompulsoryCourseCount = uniqueCourses.Count(c => c.IsCompulsory),
                LabCourseCount = labCourseCount,
                AmphiNeedCount = amphiNeedCount,
                TotalWeeklyTheoryHours = totalWeeklyTheoryHours,
                TotalWeeklyLabHours = totalWeeklyLabHours,
                EstimatedSectionCount = estimatedTotalSections,
                ParallelClassroomNeed = finalClassroomNeed,
                ParallelLabNeed = finalLabNeed,
                HistoricalSectionCount = historicalSectionCount,
                HistoricalClassroomUsage = historicalClassroomUsage,
            });
        }

        // ── 3. Derslik dağıtım algoritması ───────────────────────────────
        var suggestions = new List<ClassroomAssignmentItemDto>();
        var remaining = new List<Classroom>(unassignedClassrooms);

        // Toplam talep (ağırlıklı puan)
        var totalDemandScore = facultyDemands.Sum(f => f.DemandScore);
        if (totalDemandScore <= 0) totalDemandScore = 1;

        // Fakülte sıralaması: toplam talep puanına göre
        var rankedFaculties = facultyDemands
            .Where(f => f.DemandScore > 0)
            .OrderByDescending(f => f.DemandScore)
            .ToList();

        if (rankedFaculties.Count == 0)
        {
            return BuildResult(term, classrooms, existingAssignments, suggestions, facultyDemands, totalDemandScore);
        }

        // ── 3a. Amfiler → amfi ihtiyacı en yüksek fakülteye ─────────────
        var amphitheaters = remaining
            .Where(c => c.Type == ClassroomType.Amphitheater || c.Capacity >= amphiThreshold)
            .OrderByDescending(c => c.Capacity)
            .ToList();

        var amphiFaculties = rankedFaculties
            .Where(f => f.AmphiNeedCount > 0)
            .OrderByDescending(f => f.AmphiNeedCount)
            .ThenByDescending(f => f.StudentCount)
            .ToList();

        // Her fakülteye amfi ihtiyacı kadar dağıt
        foreach (var faculty in amphiFaculties)
        {
            var needed = faculty.AmphiNeedCount;
            var assigned = 0;
            foreach (var amfi in amphitheaters.ToList())
            {
                if (assigned >= needed) break;

                suggestions.Add(new ClassroomAssignmentItemDto
                {
                    ClassroomId = amfi.Id,
                    ClassroomName = amfi.Name,
                    ClassroomCapacity = amfi.Capacity,
                    ClassroomType = amfi.Type.ToString(),
                    FacultyId = faculty.FacultyId,
                    FacultyName = faculty.FacultyName,
                    Reason = $"Amfi ihtiyacı: {faculty.AmphiNeedCount} ders {amphiThreshold}+ öğrenci bekleniyor ({faculty.StudentCount} aktif öğrenci)",
                    Priority = "high"
                });
                remaining.Remove(amfi);
                amphitheaters.Remove(amfi);
                assigned++;
            }
        }

        // Kalan amfiler → en yoğun fakülteye
        foreach (var amfi in amphitheaters.ToList())
        {
            var target = rankedFaculties.FirstOrDefault();
            if (target == null) break;

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = amfi.Id,
                ClassroomName = amfi.Name,
                ClassroomCapacity = amfi.Capacity,
                ClassroomType = amfi.Type.ToString(),
                FacultyId = target.FacultyId,
                FacultyName = target.FacultyName,
                Reason = $"Kalan amfi → en yoğun fakülte ({target.StudentCount} öğrenci, {target.EstimatedSectionCount} tahmini section)",
                Priority = "medium"
            });
            remaining.Remove(amfi);
        }

        // ── 3b. Lab/Bilgisayar derslikler → lab dersi olan fakültelere ──
        var labClassrooms = remaining
            .Where(c => c.Type == ClassroomType.Lab || c.Type == ClassroomType.Computer)
            .OrderByDescending(c => c.Capacity)
            .ToList();

        var labFaculties = rankedFaculties
            .Where(f => f.ParallelLabNeed > 0)
            .OrderByDescending(f => f.ParallelLabNeed)
            .ThenByDescending(f => f.LabCourseCount)
            .ToList();

        foreach (var faculty in labFaculties)
        {
            var needed = faculty.ParallelLabNeed;
            var assigned = 0;
            foreach (var lab in labClassrooms.ToList())
            {
                if (assigned >= needed) break;

                suggestions.Add(new ClassroomAssignmentItemDto
                {
                    ClassroomId = lab.Id,
                    ClassroomName = lab.Name,
                    ClassroomCapacity = lab.Capacity,
                    ClassroomType = lab.Type.ToString(),
                    FacultyId = faculty.FacultyId,
                    FacultyName = faculty.FacultyName,
                    Reason = $"Lab ihtiyacı: {faculty.LabCourseCount} lab dersi, tahmini {faculty.ParallelLabNeed} paralel lab dersliği gerekli",
                    Priority = "high"
                });
                remaining.Remove(lab);
                labClassrooms.Remove(lab);
                assigned++;
            }
        }

        // Kalan lab derslikler → lab dersi olan ama daha az ihtiyacı olan fakültelere
        foreach (var lab in labClassrooms.ToList())
        {
            var target = rankedFaculties.FirstOrDefault(f => f.LabCourseCount > 0)
                ?? rankedFaculties.FirstOrDefault();
            if (target == null) break;

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = lab.Id,
                ClassroomName = lab.Name,
                ClassroomCapacity = lab.Capacity,
                ClassroomType = lab.Type.ToString(),
                FacultyId = target.FacultyId,
                FacultyName = target.FacultyName,
                Reason = $"Kalan lab derslik → lab dersi bulunan fakülte ({target.LabCourseCount} lab dersi)",
                Priority = "medium"
            });
            remaining.Remove(lab);
        }

        // ── 3c. Normal sınıflar → oransal dağıtım ───────────────────────
        var normalClassrooms = remaining
            .Where(c => c.Type == ClassroomType.Lecture || c.Type == ClassroomType.Seminar)
            .OrderByDescending(c => c.Capacity)
            .ToList();

        // Her fakültenin payını hesapla
        var facultyShares = rankedFaculties.Select(f => new
        {
            Faculty = f,
            Share = (double)f.DemandScore / totalDemandScore,
            TargetCount = (int)Math.Round((double)f.DemandScore / totalDemandScore * normalClassrooms.Count),
            Assigned = 0
        }).ToList();

        // Yuvarlama farkını düzelt
        var totalTarget = facultyShares.Sum(s => s.TargetCount);
        var diff = normalClassrooms.Count - totalTarget;
        // Fark varsa en yoğun fakülteye ekle
        var adjustedShares = facultyShares.Select((s, i) => new
        {
            s.Faculty,
            s.Share,
            TargetCount = i == 0 ? s.TargetCount + diff : s.TargetCount,
            s.Assigned
        }).ToList();

        var assignedPerFaculty = adjustedShares.ToDictionary(s => s.Faculty.FacultyId, s => 0);

        foreach (var classroom in normalClassrooms)
        {
            // En çok eksiği olan fakülteyi seç
            var target = adjustedShares
                .OrderByDescending(s => s.TargetCount - assignedPerFaculty[s.Faculty.FacultyId])
                .ThenByDescending(s => s.Faculty.DemandScore)
                .FirstOrDefault();

            if (target == null) break;

            assignedPerFaculty[target.Faculty.FacultyId]++;

            var currentAssigned = assignedPerFaculty[target.Faculty.FacultyId];
            var sharePercent = (int)Math.Round(target.Share * 100);

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = classroom.Id,
                ClassroomName = classroom.Name,
                ClassroomCapacity = classroom.Capacity,
                ClassroomType = classroom.Type.ToString(),
                FacultyId = target.Faculty.FacultyId,
                FacultyName = target.Faculty.FacultyName,
                Reason = $"Oransal dağıtım (%{sharePercent} pay): {target.Faculty.StudentCount} öğrenci, "
                    + $"~{target.Faculty.EstimatedSectionCount} section, "
                    + $"haftalık {target.Faculty.TotalWeeklyTheoryHours + target.Faculty.TotalWeeklyLabHours} saat ders yükü",
                Priority = "normal"
            });
            remaining.Remove(classroom);
        }

        // ── 3d. Kalan tipler (varsa) ─────────────────────────────────────
        foreach (var classroom in remaining.ToList())
        {
            var target = rankedFaculties.FirstOrDefault();
            if (target == null) break;

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = classroom.Id,
                ClassroomName = classroom.Name,
                ClassroomCapacity = classroom.Capacity,
                ClassroomType = classroom.Type.ToString(),
                FacultyId = target.FacultyId,
                FacultyName = target.FacultyName,
                Reason = $"Genel havuz → en yoğun fakülte ({target.StudentCount} öğrenci)",
                Priority = "low"
            });
        }

        return BuildResult(term, classrooms, existingAssignments, suggestions, facultyDemands, totalDemandScore);
    }

    private static ClassroomAssignmentSuggestionDto BuildResult(
        AcademicTerm term,
        List<Classroom> classrooms,
        List<int> existingAssignments,
        List<ClassroomAssignmentItemDto> suggestions,
        List<FacultyDemandAnalysis> facultyDemands,
        double totalDemandScore)
    {
        return new ClassroomAssignmentSuggestionDto
        {
            TermId = term.Id,
            TermName = term.Name,
            TotalClassrooms = classrooms.Count,
            AlreadyAssigned = existingAssignments.Count,
            Suggestions = suggestions,
            FacultyLoads = facultyDemands.Select(f => new FacultyLoadDto
            {
                FacultyId = f.FacultyId,
                FacultyName = f.FacultyName,
                StudentCount = f.StudentCount,
                CurriculumCourseCount = f.CurriculumCourseCount,
                CompulsoryCourseCount = f.CompulsoryCourseCount,
                LabCourseCount = f.LabCourseCount,
                AmphiNeedCount = f.AmphiNeedCount,
                EstimatedSectionCount = f.EstimatedSectionCount,
                TotalWeeklyHours = f.TotalWeeklyTheoryHours + f.TotalWeeklyLabHours,
                ParallelClassroomNeed = f.ParallelClassroomNeed,
                ParallelLabNeed = f.ParallelLabNeed,
                HistoricalSectionCount = f.HistoricalSectionCount,
                HistoricalClassroomUsage = f.HistoricalClassroomUsage,
                LoadPercentage = totalDemandScore > 0
                    ? Math.Round(f.DemandScore / totalDemandScore * 100, 1)
                    : 0
            }).ToList()
        };
    }

    private class FacultyDemandAnalysis
    {
        public int FacultyId { get; set; }
        public string FacultyName { get; set; } = "";
        public int StudentCount { get; set; }
        public int CurriculumCourseCount { get; set; }
        public int CompulsoryCourseCount { get; set; }
        public int LabCourseCount { get; set; }
        public int AmphiNeedCount { get; set; }
        public int TotalWeeklyTheoryHours { get; set; }
        public int TotalWeeklyLabHours { get; set; }
        public int EstimatedSectionCount { get; set; }
        public int ParallelClassroomNeed { get; set; }
        public int ParallelLabNeed { get; set; }
        public int HistoricalSectionCount { get; set; }
        public int HistoricalClassroomUsage { get; set; }

        /// <summary>
        /// Talep puanı: öğrenci + section + haftalık saat ağırlıklı
        /// </summary>
        public double DemandScore =>
            StudentCount * 1.0
            + EstimatedSectionCount * 5.0
            + (TotalWeeklyTheoryHours + TotalWeeklyLabHours) * 2.0;

        public static FacultyDemandAnalysis Empty(int facultyId, string facultyName) => new()
        {
            FacultyId = facultyId,
            FacultyName = facultyName
        };
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────

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
    public string Priority { get; set; } = "normal";
}

public class FacultyLoadDto
{
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = "";
    public int StudentCount { get; set; }
    public int CurriculumCourseCount { get; set; }
    public int CompulsoryCourseCount { get; set; }
    public int LabCourseCount { get; set; }
    public int AmphiNeedCount { get; set; }
    public int EstimatedSectionCount { get; set; }
    public int TotalWeeklyHours { get; set; }
    public int ParallelClassroomNeed { get; set; }
    public int ParallelLabNeed { get; set; }
    public int HistoricalSectionCount { get; set; }
    public int HistoricalClassroomUsage { get; set; }
    public double LoadPercentage { get; set; }
}
