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

        var opts = await LoadOptionsAsync(ct);
        var amphiThreshold = opts.AmphiThreshold;

        // Haftalık slot sayısını TimeSlot grid'inden oku (hardcoded 30 yerine).
        // Her dönemde TimeSlot grid'i ayrı; admin slot ekleyip/çıkarabilir, hesap dinamik kalır.
        var availableWeeklySlotsPerClassroom = await _context.TimeSlots
            .Where(ts => ts.TermId == termId)
            .CountAsync(ct);
        if (availableWeeklySlotsPerClassroom <= 0)
            availableWeeklySlotsPerClassroom = 30; // TimeSlot grid'i henüz oluşturulmamışsa fallback

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

        // Tip-bazlı ortalama kapasite (section sayısı tahmini için)
        var lectureAvgCapacity = classrooms
            .Where(c => c.Type == ClassroomType.Lecture || c.Type == ClassroomType.Seminar || c.Type == ClassroomType.Amphitheater)
            .Select(c => c.Capacity)
            .DefaultIfEmpty(50)
            .Average();
        var labAvgCapacity = classrooms
            .Where(c => c.Type == ClassroomType.Lab || c.Type == ClassroomType.Computer)
            .Select(c => c.Capacity)
            .DefaultIfEmpty(30)
            .Average();

        // Önceki dönem (tarihsel analiz için) — aynı TermType (Güz→Güz, Bahar→Bahar)
        // ve kronolojik olarak önce gelen (TermStart). Id sırası ≠ takvim sırası olabilir;
        // ayrıca parite eşleşmezse Güz için bir önceki Bahar baz alınıp tahmin bozulurdu.
        var previousTermId = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.TermType == term.TermType && t.TermStart < term.TermStart)
            .OrderByDescending(t => t.TermStart)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(ct);

        // Dönemin yarıyıl paritesi (Güz→tek, Bahar→çift, Yaz→tümü)
        var termType = term.TermType;
        var semesterParity = termType == TermType.Spring ? 0 : 1;
        var isSummer = termType == TermType.Summer;

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
            // Fakülte toplam (istatistik için)
            var studentCount = await _context.Students
                .Where(s => s.Status == StudentStatus.Active
                    && s.StudentPrograms.Any(sp => sp.IsActive
                        && sp.Type == StudentProgramType.Main
                        && departmentIds.Contains(sp.Program.DepartmentId)))
                .CountAsync(ct);

            // Bölüm bazlı öğrenci sayısı (ders talep tahmini için)
            var studentCountByDept = new Dictionary<int, int>();
            foreach (var deptId in departmentIds)
            {
                studentCountByDept[deptId] = await _context.Students
                    .Where(s => s.Status == StudentStatus.Active
                        && s.StudentPrograms.Any(sp => sp.IsActive
                            && sp.Type == StudentProgramType.Main
                            && sp.Program.DepartmentId == deptId))
                    .CountAsync(ct);
            }

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
            int amphiCourseCount = 0;        // Kaç ders büyük derslik gerektiriyor
            int amphiWeeklyHours = 0;        // Bu derslerin toplam haftalık saati
            int estimatedTotalSections = 0;

            // yearCount'u döngü dışında bir kez hesapla
            var yearCount = Math.Max(1, curriculumCourses
                .Select(cc => (cc.SuggestedSemester + 1) / 2)
                .DefaultIfEmpty(1)
                .Max());

            foreach (var uc in uniqueCourses)
            {
                var course = uc.Course;

                // Bu ders için tahmini öğrenci sayısı:
                // Dersin bölümündeki öğrenci sayısını kullan (tüm fakülte değil)
                var courseStudentBase = course.DepartmentId.HasValue
                    && studentCountByDept.TryGetValue(course.DepartmentId.Value, out var deptCount)
                    ? deptCount
                    : studentCount; // cross-departmental dersler için fakülte toplamını kullan

                int estimatedDemand;
                if (uc.IsCompulsory)
                {
                    estimatedDemand = Math.Max(1, courseStudentBase / yearCount);
                }
                else
                {
                    estimatedDemand = Math.Max(1, (int)(courseStudentBase / yearCount * opts.ElectiveDemandRatio));
                }

                // Teori section sayısı → derslik/amfi kapasitesine göre
                int theorySectionsNeeded = course.TheoryHours > 0
                    ? Math.Max(1, (int)Math.Ceiling(estimatedDemand / lectureAvgCapacity))
                    : 0;

                // Lab section sayısı → lab derslik kapasitesine göre (lablar daha küçük)
                int labSectionsNeeded = course.LabHours > 0
                    ? Math.Max(1, (int)Math.Ceiling(estimatedDemand / labAvgCapacity))
                    : 0;

                estimatedTotalSections += Math.Max(theorySectionsNeeded, labSectionsNeeded);

                // Teori saatleri
                if (course.TheoryHours > 0)
                    totalWeeklyTheoryHours += course.TheoryHours * theorySectionsNeeded;

                // Lab saatleri
                if (course.LabHours > 0)
                {
                    totalWeeklyLabHours += course.LabHours * labSectionsNeeded;
                    labCourseCount++;
                }

                // Amfi ihtiyacı: tahmini talep >= eşik → bu ders büyük derslikte verilmeli
                if (estimatedDemand >= amphiThreshold)
                {
                    amphiCourseCount++;
                    // Bu ders tek section olarak amfide verilecek, haftalık saatleri
                    amphiWeeklyHours += course.TheoryHours;
                }
            }

            // Paralel amfi ihtiyacı: amfi gerektiren derslerin haftalık saatlerini
            // zaman dilimlerine bölerek aynı anda kaç amfi gerektiğini hesapla
            int parallelAmphiNeed = amphiWeeklyHours > 0
                ? (int)Math.Ceiling((double)amphiWeeklyHours / availableWeeklySlotsPerClassroom)
                : 0;
            // En az 1 amfi (amfi dersi varsa) ama ders sayısından fazla olamaz
            if (amphiCourseCount > 0)
                parallelAmphiNeed = Math.Max(1, Math.Min(parallelAmphiNeed, amphiCourseCount));

            // ── 2d. Paralel derslik ihtiyacı ─────────────────────────────
            // Haftada kaç saat derslik kullanılacak (teori + lab ayrı tip)
            int totalWeeklySlotDemand = totalWeeklyTheoryHours + totalWeeklyLabHours;

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
            int historicalClassroomUsage = 0;
            int historicalLabClassroomUsage = 0;
            if (previousTermId > 0)
            {
                var prevSections = await _context.CourseSections
                    .AsNoTracking()
                    .Include(cs => cs.Course)
                    .Where(cs => cs.TermId == previousTermId && cs.Status == CourseSectionStatus.Active
                        && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
                    .ToListAsync(ct);

                historicalSectionCount = prevSections.Count;

                var prevSectionIds = prevSections.Select(ps => ps.Id).ToList();

                // Kaç farklı derslik kullanılmış?
                historicalClassroomUsage = await _context.Set<SectionSchedule>()
                    .AsNoTracking()
                    .Where(ss => prevSectionIds.Contains(ss.CourseSectionId))
                    .Select(ss => ss.ClassroomId)
                    .Distinct()
                    .CountAsync(ct);

                // Kaç farklı lab dersliği kullanılmış?
                var labSectionIds = prevSections
                    .Where(s => s.Course.LabHours > 0)
                    .Select(s => s.Id)
                    .ToList();

                if (labSectionIds.Count > 0)
                {
                    historicalLabClassroomUsage = await _context.Set<SectionSchedule>()
                        .AsNoTracking()
                        .Where(ss => labSectionIds.Contains(ss.CourseSectionId))
                        .Join(_context.Classrooms,
                            ss => ss.ClassroomId,
                            c => c.Id,
                            (ss, c) => c)
                        .Where(c => c.Type == ClassroomType.Lab || c.Type == ClassroomType.Computer)
                        .Select(c => c.Id)
                        .Distinct()
                        .CountAsync(ct);
                }
            }

            // ── 2f. Final ihtiyaç hesabı ─────────────────────────────────
            // Tarihsel veriyle tahminleri harmanlama (varsa).
            // Ağırlıklar SystemConfig'ten: HistoricalWeight = tarihsel veriye verilen pay.
            var historicalWeight = (double)opts.HistoricalWeight;
            var predictionWeight = 1.0 - historicalWeight;
            var anomalyCap = (double)opts.HistoricalAnomalyCap;

            int finalClassroomNeed;
            if (historicalClassroomUsage > 0)
            {
                var blendedClassroom = (int)Math.Ceiling(
                    historicalClassroomUsage * historicalWeight + parallelClassroomNeed * predictionWeight);
                // Tarihsel veri, tahminin anomalyCap katını geçemez (anomali koruması)
                finalClassroomNeed = Math.Min(blendedClassroom, (int)Math.Ceiling(parallelClassroomNeed * anomalyCap));
            }
            else
            {
                finalClassroomNeed = parallelClassroomNeed;
            }

            // Lab derslik ihtiyacı: tarihsel (kaç farklı lab dersliği kullanılmış) + tahmin
            int finalLabNeed;
            if (historicalLabClassroomUsage > 0)
            {
                var blendedLab = (int)Math.Ceiling(
                    historicalLabClassroomUsage * historicalWeight + parallelLabNeed * predictionWeight);
                finalLabNeed = Math.Min(blendedLab, (int)Math.Ceiling(parallelLabNeed * anomalyCap));
            }
            else
            {
                finalLabNeed = Math.Max(parallelLabNeed, labCourseCount > 0 ? 1 : 0);
            }

            var analysis = new FacultyDemandAnalysis
            {
                FacultyId = faculty.Id,
                FacultyName = faculty.Name,
                StudentCount = studentCount,
                CurriculumCourseCount = uniqueCourses.Count,
                CompulsoryCourseCount = uniqueCourses.Count(c => c.IsCompulsory),
                LabCourseCount = labCourseCount,
                AmphiCourseCount = amphiCourseCount,
                ParallelAmphiNeed = parallelAmphiNeed,
                TotalWeeklyTheoryHours = totalWeeklyTheoryHours,
                TotalWeeklyLabHours = totalWeeklyLabHours,
                EstimatedSectionCount = estimatedTotalSections,
                ParallelClassroomNeed = finalClassroomNeed,
                ParallelLabNeed = finalLabNeed,
                HistoricalSectionCount = historicalSectionCount,
                HistoricalClassroomUsage = historicalClassroomUsage,
            };
            analysis.ComputeDemandScore(opts);
            facultyDemands.Add(analysis);
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

        // ── Faz arası feedback için fakülte başına toplam atama sayacı ──
        // Amfi ve lab fazlarında atanan dersliklerin toplamı; Aşama 3c'de
        // (normal sınıflar) DemandScore'a feedback olarak yansıtılır.
        var totalAssignedPerFaculty = rankedFaculties.ToDictionary(f => f.FacultyId, _ => 0);

        // Soft threshold bandının alt sınırı — bu bandda olan amfiler "soft"
        // işaretiyle önerilir (admin kararı için gri bölgeyi gösterir).
        var softAmphiLowerBound = (int)Math.Round(
            amphiThreshold * (1m - opts.SoftThresholdBandPercent / 100m));

        // ── 3a. Amfiler → amfi ihtiyacı en yüksek fakülteye ─────────────
        // Sert eşik + soft band: kapasite >= amphiThreshold zorunlu amfi,
        // [softLower, amphiThreshold) arasındaki büyük sınıflar "soft amphi".
        var amphitheaters = remaining
            .Where(c => c.Type == ClassroomType.Amphitheater || c.Capacity >= amphiThreshold)
            .OrderByDescending(c => c.Capacity)
            .ToList();

        var amphiFaculties = rankedFaculties
            .Where(f => f.ParallelAmphiNeed > 0)
            .OrderByDescending(f => f.ParallelAmphiNeed)
            .ThenByDescending(f => f.StudentCount)
            .ToList();

        // Round-robin ile dağıt — bir fakülteye tüm amfileri yığmak yerine
        // sırayla 1'er amfi ver, böylece kıt kaynak adil paylaşılır.
        DistributeRoundRobin(
            classroomsToAssign: amphitheaters,
            faculties: amphiFaculties,
            getNeeded: f => f.ParallelAmphiNeed,
            buildSuggestion: (cr, faculty) => new ClassroomAssignmentItemDto
            {
                ClassroomId = cr.Id,
                ClassroomName = cr.Name,
                ClassroomCapacity = cr.Capacity,
                ClassroomType = cr.Type.ToString(),
                FacultyId = faculty.FacultyId,
                FacultyName = faculty.FacultyName,
                Reason = $"{faculty.AmphiCourseCount} ders {amphiThreshold}+ öğrenciye sahip, haftalık programa göre {faculty.ParallelAmphiNeed} amfi aynı anda gerekli",
                Priority = "high",
            },
            sink: cr =>
            {
                suggestions.Add(cr.suggestion);
                remaining.Remove(cr.classroom);
                amphitheaters.Remove(cr.classroom);
                totalAssignedPerFaculty[cr.facultyId]++;
            });

        // Kalan amfiler → en yoğun fakülteye
        foreach (var amfi in amphitheaters.ToList())
        {
            var target = rankedFaculties.FirstOrDefault();
            if (target == null) break;

            // Soft threshold işareti: kapasite < amphiThreshold ama yine
            // amfi sınıfı olarak değerlendirilen sınıflar için gerekçeyi farklılaştır.
            var isSoftAmphi = amfi.Type != ClassroomType.Amphitheater
                && amfi.Capacity < amphiThreshold
                && amfi.Capacity >= softAmphiLowerBound;
            var reasonSuffix = isSoftAmphi
                ? $" [soft-amfi: kapasite {amfi.Capacity}, eşik {amphiThreshold} altında ama yakın]"
                : "";

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = amfi.Id,
                ClassroomName = amfi.Name,
                ClassroomCapacity = amfi.Capacity,
                ClassroomType = amfi.Type.ToString(),
                FacultyId = target.FacultyId,
                FacultyName = target.FacultyName,
                Reason = $"Kalan amfi → en yoğun fakülte ({target.StudentCount} öğrenci, {target.EstimatedSectionCount} tahmini section){reasonSuffix}",
                Priority = "medium"
            });
            remaining.Remove(amfi);
            totalAssignedPerFaculty[target.FacultyId]++;
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

        // Lab atama sayısını fakülte bazında takip et
        var labAssignedCount = rankedFaculties.ToDictionary(f => f.FacultyId, _ => 0);

        // Lab dağıtımı da round-robin — eşit ihtiyaçlı fakülteler dönüşümlü alır
        DistributeRoundRobin(
            classroomsToAssign: labClassrooms,
            faculties: labFaculties,
            getNeeded: f => f.ParallelLabNeed,
            buildSuggestion: (cr, faculty) => new ClassroomAssignmentItemDto
            {
                ClassroomId = cr.Id,
                ClassroomName = cr.Name,
                ClassroomCapacity = cr.Capacity,
                ClassroomType = cr.Type.ToString(),
                FacultyId = faculty.FacultyId,
                FacultyName = faculty.FacultyName,
                Reason = $"Lab ihtiyacı: {faculty.LabCourseCount} lab dersi, tahmini {faculty.ParallelLabNeed} paralel lab dersliği gerekli",
                Priority = "high",
            },
            sink: cr =>
            {
                suggestions.Add(cr.suggestion);
                remaining.Remove(cr.classroom);
                labClassrooms.Remove(cr.classroom);
                labAssignedCount[cr.facultyId]++;
                totalAssignedPerFaculty[cr.facultyId]++;
            });

        // Kalan lab derslikler → en çok lab eksiği olan fakülteye (deficit-based dağıtım)
        foreach (var lab in labClassrooms.ToList())
        {
            var target = rankedFaculties
                .Where(f => f.LabCourseCount > 0)
                .OrderByDescending(f => f.ParallelLabNeed - labAssignedCount.GetValueOrDefault(f.FacultyId))
                .ThenByDescending(f => f.LabCourseCount)
                .FirstOrDefault()
                ?? rankedFaculties.FirstOrDefault();
            if (target == null) break;

            labAssignedCount[target.FacultyId] = labAssignedCount.GetValueOrDefault(target.FacultyId) + 1;
            totalAssignedPerFaculty[target.FacultyId]++;

            suggestions.Add(new ClassroomAssignmentItemDto
            {
                ClassroomId = lab.Id,
                ClassroomName = lab.Name,
                ClassroomCapacity = lab.Capacity,
                ClassroomType = lab.Type.ToString(),
                FacultyId = target.FacultyId,
                FacultyName = target.FacultyName,
                Reason = $"Kalan lab derslik → en çok lab eksiği olan fakülte ({target.LabCourseCount} lab dersi, {labAssignedCount[target.FacultyId]} atanmış)",
                Priority = "medium"
            });
            remaining.Remove(lab);
        }

        // ── 3c. Normal sınıflar → oransal dağıtım (faz feedback + fairness) ──
        var normalClassrooms = remaining
            .Where(c => c.Type == ClassroomType.Lecture || c.Type == ClassroomType.Seminar)
            .OrderByDescending(c => c.Capacity)
            .ToList();

        // Faz arası FEEDBACK: amfi/lab fazında çok atama almış fakültenin
        // DemandScore'unu kısmen düşür ki normal sınıf payı şişmesin.
        // feedbackWeight = 1.0 → tam düş; 0.0 → hiç düşme; 0.5 → yarısı (dengeli)
        var feedbackWeight = (double)opts.PhaseFeedbackWeight;
        double EffectiveDemand(FacultyDemandAnalysis f)
        {
            if (!opts.PhaseFeedbackEnabled) return f.DemandScore;
            // Her atanan derslik DemandScore'dan düşülür (avg=1 atama puanı eşdeğeri)
            // — fakat tam düşmek aşırı agresif olur, weight ile yumuşat
            var penalty = totalAssignedPerFaculty.GetValueOrDefault(f.FacultyId) * feedbackWeight;
            // Talep puanının yarısının altına inmesin (lower bound)
            return Math.Max(f.DemandScore * 0.5, f.DemandScore - penalty * (f.DemandScore / Math.Max(1, f.CurriculumCourseCount + f.StudentCount / 100.0)));
        }

        // Yeni efektif talep payları
        var effectiveDemands = rankedFaculties.ToDictionary(f => f.FacultyId, EffectiveDemand);
        var totalEffective = effectiveDemands.Values.Sum();
        if (totalEffective <= 0) totalEffective = 1;

        // Her fakültenin yeniden hesaplanmış payı + MIN FAIRNESS garantisi
        var facultyShares = rankedFaculties.Select(f =>
        {
            var rawShare = effectiveDemands[f.FacultyId] / totalEffective;
            var rawTarget = (int)Math.Round(rawShare * normalClassrooms.Count);
            // Minimum fairness: her aktif fakülte en az MinFairnessGuarantee derslik alır
            // (yeterli derslik varsa)
            var withGuarantee = Math.Max(rawTarget, opts.MinFairnessGuarantee);
            return new { Faculty = f, Share = rawShare, TargetCount = withGuarantee };
        }).ToList();

        // Toplam hedef > mevcut → en yoğun fakültelerden eksilt (fairness'ı koru)
        var totalTarget = facultyShares.Sum(s => s.TargetCount);
        if (totalTarget > normalClassrooms.Count)
        {
            // Yoğun fakültelerden 1'er azalt — fairness garantisini bozmadan
            var excess = totalTarget - normalClassrooms.Count;
            var sortedDesc = facultyShares
                .Where(s => s.TargetCount > opts.MinFairnessGuarantee)
                .OrderByDescending(s => s.TargetCount)
                .Select(s => s.Faculty.FacultyId)
                .ToList();
            var adjusted = facultyShares.ToDictionary(s => s.Faculty.FacultyId, s => s.TargetCount);
            var idx = 0;
            while (excess > 0 && sortedDesc.Count > 0)
            {
                var fid = sortedDesc[idx % sortedDesc.Count];
                if (adjusted[fid] > opts.MinFairnessGuarantee)
                {
                    adjusted[fid]--;
                    excess--;
                }
                idx++;
                if (idx > sortedDesc.Count * 100) break; // safety
            }
            facultyShares = facultyShares.Select(s => new
            {
                s.Faculty,
                s.Share,
                TargetCount = adjusted[s.Faculty.FacultyId],
            }).ToList();
        }
        else if (totalTarget < normalClassrooms.Count)
        {
            // Yuvarlama eksiği var → en yoğun fakülteye ekle
            var diff = normalClassrooms.Count - totalTarget;
            var topFid = facultyShares.OrderByDescending(s => s.Share).First().Faculty.FacultyId;
            facultyShares = facultyShares.Select(s => new
            {
                s.Faculty,
                s.Share,
                TargetCount = s.Faculty.FacultyId == topFid ? s.TargetCount + diff : s.TargetCount,
            }).ToList();
        }

        var assignedPerFaculty = facultyShares.ToDictionary(s => s.Faculty.FacultyId, _ => 0);

        foreach (var classroom in normalClassrooms)
        {
            // En çok eksiği olan fakülteyi seç — tie durumunda round-robin için
            // toplam atama sayısı en düşük olan tercih edilir (eşit puanlı fakültelere
            // dönüşümlü dağılım sağlar).
            var target = facultyShares
                .OrderByDescending(s => s.TargetCount - assignedPerFaculty[s.Faculty.FacultyId])
                .ThenBy(s => totalAssignedPerFaculty.GetValueOrDefault(s.Faculty.FacultyId))
                .ThenByDescending(s => s.Faculty.DemandScore)
                .FirstOrDefault();

            if (target == null) break;

            assignedPerFaculty[target.Faculty.FacultyId]++;
            totalAssignedPerFaculty[target.Faculty.FacultyId]++;

            var sharePercent = (int)Math.Round(target.Share * 100);
            var feedbackNote = opts.PhaseFeedbackEnabled && totalAssignedPerFaculty[target.Faculty.FacultyId] > assignedPerFaculty[target.Faculty.FacultyId]
                ? " (önceki fazlardaki atamalar dikkate alındı)"
                : "";

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
                    + $"haftalık {target.Faculty.TotalWeeklyTheoryHours + target.Faculty.TotalWeeklyLabHours} saat ders yükü{feedbackNote}",
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
                AmphiCourseCount = f.AmphiCourseCount,
                ParallelAmphiNeed = f.ParallelAmphiNeed,
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
        public int AmphiCourseCount { get; set; }
        public int ParallelAmphiNeed { get; set; }
        public int TotalWeeklyTheoryHours { get; set; }
        public int TotalWeeklyLabHours { get; set; }
        public int EstimatedSectionCount { get; set; }
        public int ParallelClassroomNeed { get; set; }
        public int ParallelLabNeed { get; set; }
        public int HistoricalSectionCount { get; set; }
        public int HistoricalClassroomUsage { get; set; }

        /// <summary>
        /// Talep puanı: sadece bağımsız (birbirinden türetilmemiş) metrikler.
        /// EstimatedSectionCount ve WeeklyHours zaten StudentCount'tan türüyor → dahil edilmez.
        /// Katsayılar SystemConfig'ten geliyor (admin paneli üzerinden ayarlanabilir);
        /// analiz oluşturulurken bir kez hesaplanıp burada saklanır.
        /// </summary>
        public double DemandScore { get; set; }

        public void ComputeDemandScore(SuggestionOptions opts) =>
            DemandScore = StudentCount * (double)opts.DemandWeightStudent
                + CurriculumCourseCount * (double)opts.DemandWeightCurriculum
                + LabCourseCount        * (double)opts.DemandWeightLab;

        public static FacultyDemandAnalysis Empty(int facultyId, string facultyName) => new()
        {
            FacultyId = facultyId,
            FacultyName = facultyName
        };
    }

    /// <summary>
    /// Servisin SystemConfig'ten okuduğu ayarların kapsayıcısı.
    /// Tek bir SuggestAssignmentsAsync çağrısının başında bir kez yüklenir,
    /// böylece her hesapta tekrar tekrar DB sorgusu yapılmaz.
    /// </summary>
    private sealed class SuggestionOptions
    {
        public int AmphiThreshold { get; init; }
        public decimal ElectiveDemandRatio { get; init; }
        public decimal HistoricalWeight { get; init; }
        public decimal HistoricalAnomalyCap { get; init; }
        public decimal DemandWeightStudent { get; init; }
        public decimal DemandWeightCurriculum { get; init; }
        public decimal DemandWeightLab { get; init; }

        // ── İyileştirilmiş Greedy parametreleri ───────────────────────────
        /// <summary>Her aktif fakülteye garanti minimum derslik (0 = garanti yok).</summary>
        public int MinFairnessGuarantee { get; init; }
        /// <summary>Amfi eşiğinin ±%X bandı — bu banttaki dersler "soft" işaretli.</summary>
        public decimal SoftThresholdBandPercent { get; init; }
        /// <summary>Açıksa amfi/lab dağıtımı sonrası DemandScore güncellenir.</summary>
        public bool PhaseFeedbackEnabled { get; init; }
        /// <summary>Faz feedback gücü (0-1). 1=tam düş, 0.5=yarısını düş.</summary>
        public decimal PhaseFeedbackWeight { get; init; }
    }

    /// <summary>
    /// Round-robin dağıtım yardımcısı: bir fakülteye ihtiyacı kadar derslik
    /// peşpeşe vermek yerine, sırayla 1'er derslik dolaşır. Böylece kıt
    /// kaynaklarda (örn. 3 amfi, 5 fakülte istiyor) hiçbir fakülte sıfırlanmaz.
    /// </summary>
    private static void DistributeRoundRobin(
        List<Classroom> classroomsToAssign,
        List<FacultyDemandAnalysis> faculties,
        Func<FacultyDemandAnalysis, int> getNeeded,
        Func<Classroom, FacultyDemandAnalysis, ClassroomAssignmentItemDto> buildSuggestion,
        Action<(Classroom classroom, int facultyId, ClassroomAssignmentItemDto suggestion)> sink)
    {
        if (classroomsToAssign.Count == 0 || faculties.Count == 0) return;

        var assigned = faculties.ToDictionary(f => f.FacultyId, _ => 0);
        var active = faculties.ToList();
        // Snapshot — döngü sırasında listeyi modifiye etmemek için
        var pool = classroomsToAssign.ToList();

        while (pool.Count > 0 && active.Count > 0)
        {
            // Bu round'da ihtiyacı olan fakültelerin sırasıyla dolaş
            var roundActive = active.Where(f => assigned[f.FacultyId] < getNeeded(f)).ToList();
            if (roundActive.Count == 0) break;

            foreach (var faculty in roundActive)
            {
                if (pool.Count == 0) break;
                if (assigned[faculty.FacultyId] >= getNeeded(faculty)) continue;

                var cr = pool[0];
                pool.RemoveAt(0);
                assigned[faculty.FacultyId]++;
                sink((cr, faculty.FacultyId, buildSuggestion(cr, faculty)));
            }

            // İhtiyacı doymuş fakülteleri çıkar
            active = active.Where(f => assigned[f.FacultyId] < getNeeded(f)).ToList();
        }
    }

    private async Task<SuggestionOptions> LoadOptionsAsync(CancellationToken ct) => new()
    {
        AmphiThreshold           = await _config.GetIntAsync("Classroom.AmphiThreshold", 100, ct),
        ElectiveDemandRatio      = await _config.GetDecimalAsync("Suggestion.ElectiveDemandRatio", 0.35m, ct),
        HistoricalWeight         = await _config.GetDecimalAsync("Suggestion.HistoricalWeight", 0.6m, ct),
        HistoricalAnomalyCap     = await _config.GetDecimalAsync("Suggestion.HistoricalAnomalyCap", 2.0m, ct),
        DemandWeightStudent      = await _config.GetDecimalAsync("Suggestion.DemandWeight.Student", 1.0m, ct),
        DemandWeightCurriculum   = await _config.GetDecimalAsync("Suggestion.DemandWeight.Curriculum", 3.0m, ct),
        DemandWeightLab          = await _config.GetDecimalAsync("Suggestion.DemandWeight.Lab", 2.0m, ct),
        MinFairnessGuarantee     = await _config.GetIntAsync("Suggestion.MinFairnessGuarantee", 1, ct),
        SoftThresholdBandPercent = await _config.GetDecimalAsync("Suggestion.SoftThresholdBandPercent", 10m, ct),
        PhaseFeedbackEnabled     = await _config.GetBoolAsync("Suggestion.PhaseFeedbackEnabled", true, ct),
        PhaseFeedbackWeight      = await _config.GetDecimalAsync("Suggestion.PhaseFeedbackWeight", 0.5m, ct),
    };
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
    public int AmphiCourseCount { get; set; }
    public int ParallelAmphiNeed { get; set; }
    public int EstimatedSectionCount { get; set; }
    public int TotalWeeklyHours { get; set; }
    public int ParallelClassroomNeed { get; set; }
    public int ParallelLabNeed { get; set; }
    public int HistoricalSectionCount { get; set; }
    public int HistoricalClassroomUsage { get; set; }
    public double LoadPercentage { get; set; }
}
