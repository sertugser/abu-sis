using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class RegistrationValidationService : IRegistrationValidationService
{
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _config;
    private readonly IDateTimeProvider _clock;
    private readonly IWebModuleControlService _webModuleControls;
    private readonly IEctsLimitResolver _ectsLimitResolver;
    private readonly IRegistrationGate? _registrationGate;
    private readonly bool _useCalendarFallbackForRegistrationWindow;

    public RegistrationValidationService(
        AppDbContext context,
        IDateTimeProvider clock,
        ISystemConfigProvider? config = null,
        IWebModuleControlService? webModuleControls = null,
        IEctsLimitResolver? ectsLimitResolver = null,
        IRegistrationGate? registrationGate = null)
    {
        _context = context;
        _clock = clock;
        _config = config ?? new NullSystemConfigProvider();
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
        _ectsLimitResolver = ectsLimitResolver ?? new EctsLimitResolver(_config);
        _registrationGate = registrationGate;
        _useCalendarFallbackForRegistrationWindow = webModuleControls is null;
    }

    public async Task<ValidationResultDto> ValidateRegistrationAsync(int studentId, List<int> courseSectionIds, bool skipGateChecks = false)
    {
        var result = new ValidationResultDto { IsValid = true };
        var errors = new List<ValidationError>();

        // Load student with programs
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
        {
            errors.Add(new ValidationError { Rule = "StudentExists", Message = "Ogrenci bulunamadi." });
            result.IsValid = false;
            result.Errors = errors;
            return result;
        }

        // Load requested sections with courses and prerequisites
        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
                .ThenInclude(c => c.Prerequisites)
            .Include(cs => cs.Registrations)
            .Where(cs => courseSectionIds.Contains(cs.Id))
            .ToListAsync();
        var requestedSectionIds = sections.Select(section => section.Id).ToHashSet();

        // Load active term
        var now = DateTime.UtcNow;
        var activeTerm = await _context.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .ActiveTerm()
            .FirstOrDefaultAsync();

        // Load student's existing active registrations for this term
        // Madde 9: Draft kayıtlar da AKTS sınır hesabına dahil edilir.
        // Aksi takdirde öğrenci limit üstü dersleri sepete atar, gönderide sürpriz hata yaşar.
        // Faz 4: OpeningReasonSnapshot include — IsCreditFree, BypassMandatoryAttendance flag'leri için.
        var existingRegistrations = activeTerm == null
            ? new List<Domain.Entities.CourseRegistration>()
            : await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                    .ThenInclude(cs => cs.Course)
                .Include(cr => cr.OpeningReasonSnapshot)
                .Where(cr => cr.StudentId == studentId
                    && cr.CourseSection.TermId == activeTerm.Id
                    && (cr.Status == RegistrationStatus.Active
                        || cr.Status == RegistrationStatus.PendingAdvisorApproval
                        || cr.Status == RegistrationStatus.Draft))
                .ToListAsync();
        // Talep edilen section'lar "requested" tarafında sayılır; aynı kayıtların mevcut tarafta
        // tekrar toplanması (çift sayım) önlenir. Onay akışında talep = onaylanan PendingAdvisorApproval
        // kayıtları olduğundan bu, AKTS toplamının iki kez sayılmasını engeller.
        var existingRegistrationsOutsideRequest = existingRegistrations
            .Where(registration => !requestedSectionIds.Contains(registration.CourseSectionId))
            .ToList();

        // Load student's all past registrations (for prerequisite and grade checks)
        var allPastRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId && cr.Grade != null)
            .ToListAsync();

        // "Kapı" kontrolleri (durum / finansal / kayıt dönemi). Danışman onayında (skipGateChecks)
        // atlanır — bunlar onay akışında ayrıca uygulanır; burada amaç akademik seçim kontrolü.
        if (!skipGateChecks)
        {
            // === CHECK 1: Student Status (FR-13/FR-14) ===
            if (student.Status != StudentStatus.Active)
            {
                errors.Add(new ValidationError
                {
                    Rule = "StudentStatus",
                    Message = $"Ogrenci durumu '{student.Status}' oldugu icin kayit yapilamaz. Sadece 'Active' durumundaki ogrenciler kayit yapabilir."
                });
            }

            // === CHECK 2: Financial Eligibility (FR-49) ===
            if (student.HasFinancialBlock)
            {
                errors.Add(new ValidationError
                {
                    Rule = "FinancialEligibility",
                    Message = "Ogrencinin odenmemis borcu bulunmaktadir. Borc temizlenene kadar kayit yapilamaz."
                });
            }

            // === CHECK 3: Registration Period (FR-47) ===
            if (activeTerm == null)
            {
                errors.Add(new ValidationError
                {
                    Rule = "RegistrationPeriod",
                    Message = "Aktif akademik donem bulunamadi."
                });
            }
            else
            {
                var registrationControlCode = ResolveStudentRegistrationControlCode(student);
                var registrationAccess = _useCalendarFallbackForRegistrationWindow
                    ? BuildFallbackRegistrationAccess(activeTerm)
                    : await _webModuleControls.EvaluateAsync(registrationControlCode, activeTerm.Id);
                if (!registrationAccess.IsOpen)
                {
                    errors.Add(new ValidationError
                    {
                        Rule = "RegistrationPeriod",
                        Message = registrationAccess.Message ?? $"Kayıt dönemi açık değil. Dönem durumu: {activeTerm.GetStatus(_clock.UtcNow)}"
                    });
                }
            }
        }

        // If blocking errors so far, return early
        if (errors.Any())
        {
            result.IsValid = false;
            result.Errors = errors;
            return result;
        }

        // Build set of currently failed course IDs (for use in multiple checks)
        var failedCourseIds = allPastRegistrations
            .Where(r => RegistrationRulePolicy.IsFailedGrade(r.Grade))
            .Select(r => r.CourseSection.Course.Id)
            .Distinct()
            .ToList();

        var passedCourseIds = allPastRegistrations
            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
            .Select(r => r.CourseSection.Course.Id)
            .Distinct()
            .ToList();

        var currentlyFailedCourseIds = failedCourseIds.Except(passedCourseIds).ToList();

        // Tamamlanan kredi/AKTS — course-level Min* kontrolü için.
        // Snapshot varsa onu, yoksa katalog değerini kullanırız.
        var passedEcts = allPastRegistrations
            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
            .Sum(r => r.EctsSnapshot ?? r.CourseSection.Course.Ects);
        var passedCredits = allPastRegistrations
            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
            .Sum(r => r.CourseSection.Course.NationalCredit);

        // === Yayın kapısı (publish gate) — tek kaynak IRegistrationGate ===
        // Öğrenci yalnızca Active + fakülte-onaylı + suspend-değil section seçebilir. validate ve
        // submit yolunda uygulanır; onay (skipGateChecks=true) yolunda atlanır — orada ayrıca
        // değerlendirilip gate-fail kayıtlar otomatik reddedilir.
        if (!skipGateChecks && _registrationGate is not null && sections.Count > 0)
        {
            var gateResults = await _registrationGate.EvaluateSectionsAsync(sections);
            foreach (var section in sections)
            {
                if (gateResults.TryGetValue(section.Id, out var gate) && !gate.IsOpen)
                {
                    errors.Add(new ValidationError
                    {
                        Rule = gate.Rule ?? "PublishGate",
                        Message = $"'{section.Course.Code} {section.Course.Name}' {gate.Message}",
                        CourseSectionId = section.Id
                    });
                }
            }
        }

        // ── Önkoşul/eş-koşul değerlendirme verisi (tek kaynak: PrerequisiteEvaluator) ──
        var passedSet = passedCourseIds.ToHashSet();

        // İntibak (PreviousCourse) haritası — öğrencinin aktif müfredatından.
        var studentCurriculumId = await _context.StudentPrograms
            .Where(sp => sp.StudentId == studentId && sp.Type == StudentProgramType.Main && sp.IsActive)
            .Select(sp => (int?)sp.CurriculumId)
            .FirstOrDefaultAsync();
        var previousMap = studentCurriculumId is null
            ? new Dictionary<int, int>()
            : PreviousCourseResolver.BuildMap(await _context.CurriculumCourses
                .Where(cc => cc.CurriculumId == studentCurriculumId && cc.PreviousCourseId != null)
                .ToListAsync());

        // İlgili önkoşul ders id'leri için filtreli (tüm tabloyu değil) simetrik eşdeğerlik haritası.
        var requiredCourseIds = sections
            .SelectMany(s => s.Course.Prerequisites.Select(p => p.RequiredCourseId))
            .Distinct()
            .ToList();
        var equivalencyMap = requiredCourseIds.Count == 0
            ? new Dictionary<int, HashSet<int>>()
            : PrerequisiteEvaluator.BuildEquivalencyMap(await _context.CourseEquivalencies
                .Where(ce => requiredCourseIds.Contains(ce.CourseId) || requiredCourseIds.Contains(ce.EquivalentCourseId))
                .ToListAsync());

        // Eş-koşul için "aynı dönem birlikte alınan" ders kümesi: talep edilenler + mevcut aktif/pending/draft.
        var concurrentCourseIds = sections.Select(s => s.Course.Id)
            .Concat(existingRegistrations.Select(r => r.CourseSection.Course.Id))
            .ToHashSet();

        // Per-section checks
        foreach (var section in sections)
        {
            var course = section.Course;

            // === CHECK 4: Önkoşul / Eş-koşul (FR-29/FR-45) — tek kaynak: PrerequisiteEvaluator ===
            // Öğrenci dersi zaten geçtiyse (not yükseltme amaçlı tekrar) önkoşul aranmaz.
            if (course.Prerequisites.Any() && !passedSet.Contains(course.Id))
            {
                foreach (var prereq in course.Prerequisites)
                {
                    var met = PrerequisiteEvaluator.IsSatisfied(
                        prereq.RequiredCourseId,
                        prereq.IsCorequisite,
                        passedSet,
                        previousMap,
                        equivalencyMap,
                        concurrentCourseIds);

                    if (!met)
                    {
                        var reqCourse = await _context.Courses.FindAsync(prereq.RequiredCourseId);
                        errors.Add(new ValidationError
                        {
                            Rule = "Prerequisite",
                            Message = prereq.IsCorequisite
                                ? $"'{course.Code} {course.Name}' dersi icin es kosul '{reqCourse?.Code} {reqCourse?.Name}' ayni donemde alinmali veya gecilmis olmali."
                                : $"'{course.Code} {course.Name}' dersi icin on kosul '{reqCourse?.Code} {reqCourse?.Name}' tamamlanmamis.",
                            CourseSectionId = section.Id
                        });
                    }
                }
            }

            // === CHECK 4A: Min Kredi Ön Koşul (Proliz "Diğer Ön Koşul") ===
            if (course.MinCreditsRequired.HasValue && passedCredits < course.MinCreditsRequired.Value)
            {
                errors.Add(new ValidationError
                {
                    Rule = "MinCreditsNotMet",
                    Message = $"'{course.Code} {course.Name}' dersi icin en az {course.MinCreditsRequired.Value} kredi tamamlanmis olmali (mevcut: {passedCredits}).",
                    CourseSectionId = section.Id
                });
            }

            // === CHECK 4B: Min AKTS Ön Koşul (Proliz "Diğer Ön Koşul") ===
            if (course.MinEctsRequired.HasValue && passedEcts < course.MinEctsRequired.Value)
            {
                errors.Add(new ValidationError
                {
                    Rule = "MinEctsNotMet",
                    Message = $"'{course.Code} {course.Name}' dersi icin en az {course.MinEctsRequired.Value} AKTS tamamlanmis olmali (mevcut: {passedEcts}).",
                    CourseSectionId = section.Id
                });
            }

            // === CHECK 4C: Alttan Ders Kuralı (Proliz "Alttan Dersi Olmayanlar Alabilir") ===
            // Öğrencinin geçmemiş başarısız (FF/NA/U) dersi varsa engellenir.
            if (course.RequiresNoFailedLowerCourses && currentlyFailedCourseIds.Any())
            {
                errors.Add(new ValidationError
                {
                    Rule = "HasFailedLowerCourses",
                    Message = $"'{course.Code} {course.Name}' dersi sadece basarisiz (alttan) dersi olmayan ogrenciler tarafindan alinabilir.",
                    CourseSectionId = section.Id
                });
            }

            // === CHECK 4D: Tekil Alma Kuralı (Proliz "Bu Ders Tek Başına Alınabilir") ===
            // İşaretliyse: bu dersle birlikte başka ders alınamaz (talep edilen + mevcut aktif kayıtlar).
            if (course.CanBeTakenStandalone)
            {
                var otherRequested = sections.Any(s => s.Id != section.Id);
                var otherExisting = existingRegistrationsOutsideRequest.Any(r => r.CourseSection.CourseId != course.Id);
                if (otherRequested || otherExisting)
                {
                    errors.Add(new ValidationError
                    {
                        Rule = "MustBeTakenStandalone",
                        Message = $"'{course.Code} {course.Name}' dersi sadece tek basina alinabilir, baska derslerle birlikte kayit yapilamaz.",
                        CourseSectionId = section.Id
                    });
                }
            }

            // === CHECK 9: Capacity Check (FR-40) ===
            var ownDraftHoldCount = existingRegistrations.Count(registration =>
                registration.Status == RegistrationStatus.Draft
                && registration.CourseSectionId == section.Id);
            var effectiveEnrolledCount = Math.Max(0, section.EnrolledCount - ownDraftHoldCount);
            if (effectiveEnrolledCount >= section.Capacity)
            {
                errors.Add(new ValidationError
                {
                    Rule = "Capacity",
                    Message = $"'{course.Code} {course.Name}' dersinin kontenjani dolu ({effectiveEnrolledCount}/{section.Capacity}).",
                    CourseSectionId = section.Id
                });
            }

            // === CHECK 10: Duplicate Registration (FR-50) ===
            var alreadyRegistered = existingRegistrationsOutsideRequest.Any(r =>
                r.CourseSection.Course.Id == course.Id);

            if (alreadyRegistered)
            {
                errors.Add(new ValidationError
                {
                    Rule = "DuplicateRegistration",
                    Message = $"'{course.Code} {course.Name}' dersine zaten kayitlisiniz.",
                    CourseSectionId = section.Id
                });
            }
        }

        // === CHECK 5: Failed Courses Priority (FR-55) ===
        if (currentlyFailedCourseIds.Any())
        {
            var registeredForFailedCourses = sections
                .Where(s => currentlyFailedCourseIds.Contains(s.Course.Id))
                .Select(s => s.Course.Id)
                .ToList();

            var unregisteredFailedCourses = currentlyFailedCourseIds.Except(registeredForFailedCourses).ToList();

            if (unregisteredFailedCourses.Any())
            {
                var availableSections = await _context.CourseSections
                    .Where(cs => cs.TermId == activeTerm!.Id && cs.Status == CourseSectionStatus.Active && unregisteredFailedCourses.Contains(cs.CourseId))
                    .Select(cs => cs.CourseId)
                    .Distinct()
                    .ToListAsync();

                var missingFailedCourses = unregisteredFailedCourses.Intersect(availableSections).ToList();

                if (missingFailedCourses.Any())
                {
                    foreach (var courseId in missingFailedCourses)
                    {
                        var course = await _context.Courses.FindAsync(courseId);
                        errors.Add(new ValidationError
                        {
                            Rule = "FailedCoursePriority",
                            Message = $"'{course?.Code} {course?.Name}' dersinden basarisiz oldunuz. Yeni ders almadan once bu dersi secmelisiniz."
                        });
                    }
                }
            }
        }

        // === Faz 4 — Talep edilen yeni section'lar için Açma Nedeni flag map'i ===
        // Kayıt anında öğrencinin müfredatındaki CurriculumCourse'dan canlı okunur (henüz snapshot yok).
        // IsCreditFree (CHECK 6), BypassScheduleConflict (CHECK 8), BypassMandatoryAttendance (Madde 7),
        // RequiresApproval (CourseRegistrationService) için kullanılır.
        var requestedSectionCourseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var requestedReasonByCourseId = new Dictionary<int, CurriculumCourseOpeningReason>();
        if (requestedSectionCourseIds.Count > 0)
        {
            var mainProgramForFlag = student.StudentPrograms
                .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
            if (mainProgramForFlag != null)
            {
                var reasonHits = await _context.CurriculumCourses
                    .AsNoTracking()
                    .Include(cc => cc.OpeningReason)
                    .Where(cc => cc.CurriculumId == mainProgramForFlag.CurriculumId
                              && requestedSectionCourseIds.Contains(cc.CourseId)
                              && cc.OpeningReason != null)
                    .Select(cc => new { cc.CourseId, cc.OpeningReason })
                    .ToListAsync();
                foreach (var hit in reasonHits)
                {
                    if (hit.OpeningReason != null)
                        requestedReasonByCourseId[hit.CourseId] = hit.OpeningReason;
                }
            }
        }
        bool HasFlag(int courseId, Func<CurriculumCourseOpeningReason, bool> selector) =>
            requestedReasonByCourseId.TryGetValue(courseId, out var r) && selector(r);

        // === CHECK 6: ECTS Cap (FR-56 + FR-56A) ===
        // Madde 7: Mevcut kayıtların AKTS'si snapshot'tan okunur (havuz kuralı uygulanmışsa).
        // Talep edilen dersler için henüz kayıt yok, katalog değeri kullanılır (kayıt anında snapshot'a dönüşür).
        // Faz 4: Açma Nedeni'nde IsCreditFree=true olan dersler dönem AKTS sınırı hesabından çıkarılır.

        // Mevcut kayıtlar — snapshot'taki flag okunur (donmuş)
        var existingEcts = existingRegistrationsOutsideRequest
            .Where(r => r.OpeningReasonSnapshot?.IsCreditFree != true)
            .Sum(r => r.EctsSnapshot ?? r.CourseSection.Course.Ects);

        // Talep edilen yeni dersler — IsCreditFree=true olan section'ları AKTS toplamından düş
        var requestedEcts = sections
            .Where(s => !HasFlag(s.CourseId, r => r.IsCreditFree))
            .Sum(s => s.Course.Ects);

        var totalEcts = requestedEcts + existingEcts;

        // Müfredat-özel override'ları önce dene, null ise global SystemConfig default'una düş.
        // Öğrencinin aktif ana müfredatı override sağlıyorsa onu kullanır (Proliz "Müfredat Sınıf Dönem Tanımları" muadili).
        var mainProgramForEcts = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
        Curriculum? curriculumForEcts = mainProgramForEcts == null ? null
            : await _context.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == mainProgramForEcts.CurriculumId);

        var ectsLimit = await _ectsLimitResolver.ResolveAsync(student.Cgpa, curriculumForEcts);

        if (totalEcts > ectsLimit)
        {
            errors.Add(new ValidationError
            {
                Rule = "ECTSCap",
                Message = $"ECTS limiti asiliyor. Mevcut: {existingEcts}, Istenen: {requestedEcts}, Toplam: {totalEcts}, Limit: {ectsLimit} (CGPA: {student.Cgpa:F2})"
            });
        }

        // === CHECK 6B (Madde 16): Müfredat Dışı Ders Uyarısı ===
        // Öğrencinin aktif müfredatında (direkt veya havuzda) bulunmayan dersler warning olarak işaretlenir.
        // Engelleme değil — yaz okulu/özel konu dersleri için esneklik bırakıldı; danışman onayı zincirinde gözden geçirilir.
        var mainProgramForCurr = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
        if (mainProgramForCurr != null)
        {
            var allowNonCurriculumCourseSelection = await _config.GetBoolAsync("Registration.AllowNonCurriculumCourseSelection", true);
            var curriculumCourseIds = await _context.CurriculumCourses
                .Where(cc => cc.CurriculumId == mainProgramForCurr.CurriculumId)
                .Select(cc => cc.CourseId)
                .ToListAsync();
            var curriculumCourseSet = curriculumCourseIds.ToHashSet();

            // Havuz üyesi dersler de müfredatta sayılır
            var poolCourseIds = await _context.CurriculumElectivePools
                .Where(p => p.CurriculumId == mainProgramForCurr.CurriculumId)
                .SelectMany(p => p.ElectiveGroup.Courses.Select(egc => egc.CourseId))
                .Distinct()
                .ToListAsync();
            foreach (var pid in poolCourseIds) curriculumCourseSet.Add(pid);

            foreach (var section in sections)
            {
                if (!curriculumCourseSet.Contains(section.CourseId))
                {
                    var item = new ValidationError
                    {
                        Rule = "NonCurriculumCourse",
                        Message = allowNonCurriculumCourseSelection
                            ? $"'{section.Course.Code} {section.Course.Name}' dersi mevcut müfredatınızda bulunmuyor. Danışman onayı gerektirir."
                            : $"'{section.Course.Code} {section.Course.Name}' dersi mevcut müfredatınızda bulunmadığı için sistem konfigürasyonuna göre seçilemez.",
                        CourseSectionId = section.Id
                    };

                    if (allowNonCurriculumCourseSelection)
                        result.Warnings.Add(item);
                    else
                        errors.Add(item);
                }
            }
        }

        // === CHECK 7: CGPA Restriction (FR-56B) ===
        var retentionThreshold = await _config.GetDecimalAsync("GPA.RetentionThreshold", 1.70m);
        if (RegistrationRulePolicy.HasLowCgpaRestriction(student.Cgpa, student.CompletedSemesters, retentionThreshold))
        {
            var studentProgram = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);

            if (studentProgram != null)
            {
                var curriculumCourses = await _context.CurriculumCourses
                    .Include(cc => cc.Course)
                    .Where(cc => cc.CurriculumId == studentProgram.CurriculumId)
                    .ToListAsync();

                foreach (var section in sections)
                {
                    var courseId = section.Course.Id;

                    // Check if it's a failed course (FF/NA)
                    var isFailed = currentlyFailedCourseIds.Contains(courseId);

                    // Check if it's a DD/DC course for grade improvement
                    var isDDDC = allPastRegistrations.Any(r =>
                        r.CourseSection.Course.Id == courseId &&
                        (r.Grade == GradeCode.DD || r.Grade == GradeCode.DC));

                    var currCourse = curriculumCourses.FirstOrDefault(cc => cc.CourseId == courseId);

                    // Check if it's a non-departmental course (different department)
                    var isAllowed = RegistrationRulePolicy.IsAllowedUnderLowCgpaRestriction(
                        section.Course,
                        currCourse?.SuggestedSemester,
                        student.CompletedSemesters,
                        studentProgram.Program?.DepartmentId,
                        isFailed,
                        isDDDC);

                    if (!isAllowed)
                    {
                        errors.Add(new ValidationError
                        {
                            Rule = "CGPARestriction",
                            Message = $"CGPA {student.Cgpa:F2} ile sinifta kaldiniz. '{section.Course.Code} {section.Course.Name}' dersini alamazsiniz. Sadece basarisiz dersler, DD/DC not iyilestirme, bulundugunuz sinif ve alti dersler veya bolum disi secmeliler alinabilir.",
                            CourseSectionId = section.Id
                        });
                    }
                }
            }
        }

        // === CHECK 8: Schedule Conflict (FR-41) ===
        // Load SectionSchedule records for all relevant sections (new + existing)
        var allSectionIds = sections.Select(s => s.Id)
            .Union(existingRegistrationsOutsideRequest.Select(r => r.CourseSectionId))
            .Distinct()
            .ToList();

        var sectionSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => allSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync();

        var schedulesBySectionId = sectionSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.ToTimeBlocks(g));

        for (int i = 0; i < sections.Count; i++)
        {
            var newSection = sections[i];
            var newSchedule = schedulesBySectionId.GetValueOrDefault(newSection.Id) ?? new();

            // Faz 4 — Yeni dersin Açma Nedeni'nde BypassScheduleConflict=true ise tüm çakışma kontrolü atlanır
            bool newSectionBypass = HasFlag(newSection.CourseId, r => r.BypassScheduleConflict);

            // Check against existing registrations
            foreach (var existing in existingRegistrationsOutsideRequest)
            {
                // Faz 4 — Yeni veya mevcut dersten biri "BypassScheduleConflict" ise çakışma kontrolünü atla
                bool existingBypass = existing.OpeningReasonSnapshot?.BypassScheduleConflict == true;
                if (newSectionBypass || existingBypass)
                    continue;

                var existingSchedule = schedulesBySectionId.GetValueOrDefault(existing.CourseSectionId) ?? new();

                var overlapType = RegistrationRulePolicy.CheckScheduleOverlap(newSchedule, existingSchedule);

                if (overlapType != ConflictOverlapType.None)
                {
                    bool isNewCourseRepeated = currentlyFailedCourseIds.Contains(newSection.Course.Id);
                    bool isExistingCourseRepeated = currentlyFailedCourseIds.Contains(existing.CourseSection.Course.Id);
                    bool isRepeat = isNewCourseRepeated || isExistingCourseRepeated;

                    if (overlapType == ConflictOverlapType.Full && !isRepeat)
                    {
                        // FR-41a: Full overlap for first-time courses — CATEGORICALLY BLOCKED
                        errors.Add(new ValidationError
                        {
                            Rule = "ScheduleConflict_FullBlock",
                            Message = $"'{newSection.Course.Code}' ve '{existing.CourseSection.Course.Code}' dersleri tam cakisiyor. Ilk kez alinan derslerde tam cakisma kesinlikle engellenir.",
                            CourseSectionId = newSection.Id
                        });
                    }
                    else
                    {
                        // FR-41b/c: Partial overlap or repeat course overlap — needs advisor + dept manager approval
                        string conflictType = overlapType == ConflictOverlapType.Full ? "tam" : "kismi";
                        string repeatNote = isRepeat ? " (tekrar ders)" : "";
                        errors.Add(new ValidationError
                        {
                            Rule = "ScheduleConflict_NeedsApproval",
                            Message = $"'{newSection.Course.Code}' ve '{existing.CourseSection.Course.Code}' dersleri arasinda {conflictType} cakisma{repeatNote} var. Danisman ve Bolum Muduru onayi gereklidir.",
                            CourseSectionId = newSection.Id
                        });
                    }
                }
            }

            // Check against other new sections being registered simultaneously
            for (int j = i + 1; j < sections.Count; j++)
            {
                var otherSection = sections[j];

                // Faz 4 — İki yeni dersten biri BypassScheduleConflict ise çakışma kontrolünü atla
                bool otherBypass = HasFlag(otherSection.CourseId, r => r.BypassScheduleConflict);
                if (newSectionBypass || otherBypass)
                    continue;

                var otherSchedule = schedulesBySectionId.GetValueOrDefault(otherSection.Id) ?? new();

                var overlapType = RegistrationRulePolicy.CheckScheduleOverlap(newSchedule, otherSchedule);
                if (overlapType != ConflictOverlapType.None)
                {
                    bool isRepeat = currentlyFailedCourseIds.Contains(newSection.Course.Id) || currentlyFailedCourseIds.Contains(otherSection.Course.Id);

                    if (overlapType == ConflictOverlapType.Full && !isRepeat)
                    {
                        errors.Add(new ValidationError
                        {
                            Rule = "ScheduleConflict_FullBlock",
                            Message = $"'{newSection.Course.Code}' ve '{otherSection.Course.Code}' dersleri tam cakisiyor.",
                            CourseSectionId = newSection.Id
                        });
                    }
                    else
                    {
                        string conflictType = overlapType == ConflictOverlapType.Full ? "tam" : "kismi";
                        errors.Add(new ValidationError
                        {
                            Rule = "ScheduleConflict_NeedsApproval",
                            Message = $"'{newSection.Course.Code}' ve '{otherSection.Course.Code}' dersleri arasinda {conflictType} cakisma var. Onay gereklidir.",
                            CourseSectionId = newSection.Id
                        });
                    }
                }
            }
        }

        // "NeedsApproval" errors should NOT block the registration, only warn
        result.IsValid = !errors.Any(e => e.Rule != "ScheduleConflict_NeedsApproval");
        result.Errors = errors;
        return result;
    }

    private WebModuleAccessResult BuildFallbackRegistrationAccess(AcademicTerm activeTerm)
    {
        if (activeTerm.AcademicCalendar is null)
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: WebModuleControlModes.AcademicCalendarRegistration,
                SelectedModeLabel: "Ak.Takvime Bağlı",
                EffectiveStateLabel: "Beklemede",
                Message: "Aktif dönemin akademik takvimi tanımlı olmadığı için ders kayıt işlemleri açılamıyor.");
        }

        if (!activeTerm.IsRegistrationOpen(_clock.UtcNow))
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: WebModuleControlModes.AcademicCalendarRegistration,
                SelectedModeLabel: "Ak.Takvime Bağlı",
                EffectiveStateLabel: "Beklemede",
                Message: $"Kayıt dönemi açık değil. Dönem durumu: {activeTerm.GetStatus(_clock.UtcNow)}");
        }

        return new WebModuleAccessResult(
            IsEnabled: true,
            IsOpen: true,
            SelectedMode: WebModuleControlModes.AcademicCalendarRegistration,
            SelectedModeLabel: "Ak.Takvime Bağlı",
            EffectiveStateLabel: "Açık",
            Message: null);
    }

    private static string ResolveStudentRegistrationControlCode(Student student)
    {
        if (student.CompletedSemesters == 0)
            return "student-new-course-registration";

        var activeProgram = student.StudentPrograms
            .FirstOrDefault(program => program.Type == StudentProgramType.Main && program.IsActive);

        if (activeProgram?.Program != null)
        {
            var normalDurationSemesters = activeProgram.Program.DegreeLevel switch
            {
                DegreeLevel.Associate => 4,
                DegreeLevel.Bachelor => 8,
                DegreeLevel.Master => 4,
                DegreeLevel.Doctorate => 8,
                _ => 8
            };

            if (student.CompletedSemesters >= normalDurationSemesters)
                return "student-extended-course-registration";
        }

        return "student-course-registration";
    }

}
